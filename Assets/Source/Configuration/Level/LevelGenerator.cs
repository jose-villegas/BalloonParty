using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Slots.Actor.Archetype;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Builds a level's mix from <see cref="LevelGenerationSettings" />. Pure and seeded: the same level
    ///     and seed always give the same mix.
    /// </summary>
    /// <remarks>
    ///     Staples (cost 0) and pieces still in their featured levels are always in. Each role's budget then
    ///     buys a pick of its other unlocked pieces. Threats and aids have separate budgets so that depth
    ///     reliably means more threats, not a random mix of threats and help. Picks are weighted by cost,
    ///     so the dearer threats aren't the ones a tight budget always leaves out.
    /// </remarks>
    internal static class LevelGenerator
    {
        private const int BalloonKind = 0;
        private const int ItemKind = 1;
        private const int GridActorKind = 2;

        internal static RangedLevelParameters Generate(LevelGenerationSettings settings, int level, int seed)
        {
            var rng = new System.Random(seed);
            var included = new HashSet<int>();
            SelectPieces(settings, ContentRole.Threat, level, rng, included);
            SelectPieces(settings, ContentRole.Aid, level, rng, included);

            var balloons = new List<BalloonTypeWeight>();
            var items = new List<ItemTypeWeight>();
            var gates = new List<GridActorTypeGate>();

            // Catalogue order, not pick order, so a level's lists read the same way the catalogue does.
            for (var i = 0; i < settings.Balloons.Count; i++)
            {
                if (included.Contains(Key(BalloonKind, i)))
                {
                    balloons.Add(settings.Balloons[i].ToWeight(level, rng));
                }
            }

            for (var i = 0; i < settings.Items.Count; i++)
            {
                if (included.Contains(Key(ItemKind, i)))
                {
                    items.Add(settings.Items[i].ToWeight(level, rng));
                }
            }

            // A meadow clears its puff clouds halfway through (ScenarioStretch.PuffCloudShare). Scaled after the
            // pick, so the rest of the level's mix and its rng stream are the same as they'd be under full clouds.
            var puffShare = ScenarioSchedule.StretchAt(settings.Scenario, level).PuffCloudShare;
            for (var i = 0; i < settings.GridActors.Count; i++)
            {
                if (!included.Contains(Key(GridActorKind, i)))
                {
                    continue;
                }

                var gate = settings.GridActors[i].ToGate(level);
                if (gate.Type == GridActorType.Puff)
                {
                    if (puffShare <= 0f)
                    {
                        continue;
                    }

                    gate = Scaled(gate, puffShare);
                }

                gates.Add(gate);
            }

            // The validator warns when no balloon is a staple; this keeps a misauthored catalogue playable.
            if (balloons.Count == 0)
            {
                balloons.Add(new BalloonTypeWeight(BalloonType.Simple, 1f));
            }

            var itemCounts = settings.ItemCountsForLevel(level);
            var cadence = settings.ItemCadence;

            return new RangedLevelParameters(
                settings.BoardLinesForLevel(level),
                new RangedInt(cadence, cadence),
                itemCounts.InitialItemCountWeights,
                itemCounts.ItemCountWeights,
                balloons.ToArray(),
                items.ToArray(),
                gates.ToArray(),
                (1 << settings.ColorsForLevel(level)) - 1);
        }

        private static void SelectPieces(LevelGenerationSettings settings, ContentRole role, int level,
            System.Random rng, HashSet<int> included)
        {
            var optional = new List<(int Key, int Cost)>();
            var budget = settings.BudgetForLevel(role, level);
            var featured = settings.FeaturedLevels;

            Partition(settings.Balloons, BalloonKind, role, level, featured, included, optional, ref budget);
            Partition(settings.Items, ItemKind, role, level, featured, included, optional, ref budget);
            Partition(settings.GridActors, GridActorKind, role, level, featured, included, optional, ref budget);

            while (TryPickAffordable(optional, budget, rng, out var picked))
            {
                included.Add(optional[picked].Key);
                budget -= optional[picked].Cost;
                optional.RemoveAt(picked);
            }
        }

        // A cost-weighted pick among the pieces the budget still covers.
        private static bool TryPickAffordable(List<(int Key, int Cost)> optional, float budget, System.Random rng,
            out int picked)
        {
            var total = 0;
            foreach (var piece in optional)
            {
                if (piece.Cost <= budget)
                {
                    total += piece.Cost;
                }
            }

            picked = -1;
            if (total == 0)
            {
                return false;
            }

            var roll = rng.Next(total);
            for (var i = 0; i < optional.Count; i++)
            {
                if (optional[i].Cost > budget)
                {
                    continue;
                }

                roll -= optional[i].Cost;
                if (roll < 0)
                {
                    picked = i;
                    break;
                }
            }

            return true;
        }

        // Puts each unlocked piece of the role straight in (a staple, or still featured) or among the optional
        // ones. Featured pieces are paid for first, so they can leave nothing for the optional picks.
        private static void Partition<T>(IReadOnlyList<T> pieces, int kind, ContentRole role, int level,
            int featuredLevels, HashSet<int> included, List<(int Key, int Cost)> optional, ref float budget)
            where T : IContentPiece
        {
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece.Withheld || piece.Role != role || piece.UnlockLevel > level)
                {
                    continue;
                }

                if (piece.Cost == 0 || level < piece.UnlockLevel + featuredLevels)
                {
                    included.Add(Key(kind, i));
                    budget -= piece.Cost;
                }
                else
                {
                    optional.Add((Key(kind, i), piece.Cost));
                }
            }
        }

        // At least one slot at the top of the range, so a scaled-down gate still places something.
        private static GridActorTypeGate Scaled(GridActorTypeGate gate, float share)
        {
            if (share >= 1f)
            {
                return gate;
            }

            var max = Math.Max(1, (int)Math.Round(gate.Count.Max * share, MidpointRounding.AwayFromZero));
            var min = Math.Min(max, (int)Math.Round(gate.Count.Min * share, MidpointRounding.AwayFromZero));
            return new GridActorTypeGate(gate.Type, new RangedInt(min, max, gate.Count.Mode), gate.MaxPerCluster);
        }

        private static int Key(int kind, int index)
        {
            return kind << 16 | index;
        }
    }
}

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

        // runSeed only moves which levels are on the ground (ScenarioSchedule); seed rolls everything else.
        internal static RangedLevelParameters Generate(LevelGenerationSettings settings, int level, int seed,
            int runSeed = 0)
        {
            var rng = new System.Random(seed);
            var included = new HashSet<int>();
            var stretch = ScenarioSchedule.StretchAt(settings.Scenario, level, runSeed);
            SelectPieces(settings, ContentRole.Threat, level, stretch.Kind, rng, included);
            SelectPieces(settings, ContentRole.Aid, level, stretch.Kind, rng, included);

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

            // A meadow trades its puff clouds for bushes as it goes (ScenarioStretch.PuffCloudShare / BushShare).
            // Applied after the pick, so the rest of the level's mix and its rng stream are the same as they'd be
            // under full clouds.
            if (stretch.IsClearingMeadow)
            {
                IncludeUnlockedBushes(settings, level, included);
            }

            for (var i = 0; i < settings.GridActors.Count; i++)
            {
                if (!included.Contains(Key(GridActorKind, i)))
                {
                    continue;
                }

                var gate = settings.GridActors[i].ToGate(level);
                var share = gate.Type switch
                {
                    GridActorType.Puff => stretch.PuffCloudShare,
                    GridActorType.Bush => stretch.BushShare,
                    _ => 1f,
                };
                if (share > 0f)
                {
                    gates.Add(Scaled(gate, share));
                }
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
            ScenarioKind scenario, System.Random rng, HashSet<int> included)
        {
            var optional = new List<(int Key, int Cost)>();
            var budget = settings.BudgetForLevel(role, level);
            var slot = new PieceSlot(role, level, settings.FeaturedLevels, scenario);

            Partition(settings.Balloons, BalloonKind, slot, included, optional, ref budget);
            Partition(settings.Items, ItemKind, slot, included, optional, ref budget);
            Partition(settings.GridActors, GridActorKind, slot, included, optional, ref budget);

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
        // ones. Featured pieces are paid for first, so they can leave nothing for the optional picks. A sky level
        // leaves ground-only pieces out before any budget is spent, so a bush can't eat a sky level's budget.
        private static void Partition<T>(IReadOnlyList<T> pieces, int kind, PieceSlot slot, HashSet<int> included,
            List<(int Key, int Cost)> optional, ref float budget)
            where T : IContentPiece
        {
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece.Withheld || piece.Role != slot.Role || piece.UnlockLevel > slot.Level)
                {
                    continue;
                }

                if (piece.RequiresGround && slot.Scenario != ScenarioKind.Ground)
                {
                    continue;
                }

                if (piece.Cost == 0 || slot.Level < piece.UnlockLevel + slot.FeaturedLevels)
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

        // The budget the clearing clouds free buys the bushes, so a meadow that has started clearing always has
        // them once they're unlocked, whatever the threat budget picked.
        private static void IncludeUnlockedBushes(LevelGenerationSettings settings, int level, HashSet<int> included)
        {
            for (var i = 0; i < settings.GridActors.Count; i++)
            {
                var actor = settings.GridActors[i];
                if (actor.Type == GridActorType.Bush && actor.UnlockLevel <= level)
                {
                    included.Add(Key(GridActorKind, i));
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

        // What decides whether a catalogue piece is in play for one role's pick.
        private readonly struct PieceSlot
        {
            public readonly ContentRole Role;
            public readonly int Level;
            public readonly int FeaturedLevels;
            public readonly ScenarioKind Scenario;

            public PieceSlot(ContentRole role, int level, int featuredLevels, ScenarioKind scenario)
            {
                Role = role;
                Level = level;
                FeaturedLevels = featuredLevels;
                Scenario = scenario;
            }
        }
    }
}

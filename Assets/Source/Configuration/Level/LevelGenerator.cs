using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Slots.Actor;
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
    ///     <para>
    ///         A piece its scenario doesn't allow (<see cref="IContentPiece.AllowedOn" />) is left out before any
    ///         budget is spent. A meadow level also gets its mushrooms (<see cref="MushroomWallsFor" />), which no
    ///         budget pays for, and lets its unbreakables form only the snake.
    ///     </para>
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

            // Dropped after the pick, and their weights still rolled below, so the aids, the items and their
            // weights are the ones the level would have anyway.
            var extraColor = settings.ExtraColor.IsExtraColorLevel(level, runSeed);

            var balloons = new List<BalloonTypeWeight>();
            var items = new List<ItemTypeWeight>();

            // Catalogue order, not pick order, so a level's lists read the same way the catalogue does.
            for (var i = 0; i < settings.Balloons.Count; i++)
            {
                if (included.Contains(Key(BalloonKind, i)))
                {
                    var weight = settings.Balloons[i].ToWeight(level, rng);
                    if (!extraColor || !IsDroppedThreat(settings.Balloons[i]))
                    {
                        balloons.Add(weight);
                    }
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
            if (stretch.IsClearingMeadow && !extraColor)
            {
                IncludeUnlockedBushes(settings, level, included);
            }

            var gates = GridActorGates(settings, level, stretch, included, extraColor);

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
                gates,
                (1 << Math.Min(settings.ColorsForLevel(level) + (extraColor ? 1 : 0), 31)) - 1,
                extraColor ? settings.ExtraColor.SpawnIntervalScale : 1f,
                extraColor,
                extraColor ? settings.ExtraColor.ExtraItemChance : 0f,
                stretch.Kind == ScenarioKind.Ground ? FormationShapes.Snake : FormationShapes.All);
        }

        /// <summary>
        ///     How many walls of mushroom caps a level has: none off the meadow; on it, the fewest of
        ///     <paramref name="limits" /> on the stretch's first level, growing evenly to the most on its last. A
        ///     stretch of one level has the fewest. A wall is one to three caps; how many caps the level gets is
        ///     the layout's, dealt when the level is (<see cref="MushroomPlacementLimits.MaxCapsFor" /> is the most).
        /// </summary>
        internal static int MushroomWallsFor(ScenarioStretch stretch, MushroomPlacementLimits limits)
        {
            if (stretch.Kind != ScenarioKind.Ground)
            {
                return 0;
            }

            var fewest = Math.Max(0, limits.MinCount);
            var steps = stretch.Length - 1;
            if (steps <= 0 || limits.MaxCount <= fewest)
            {
                return fewest;
            }

            // Whole numbers, a half rounding up: the count can't land a wall short on a float's rounding.
            var position = Math.Min(Math.Max(stretch.Position, 0), steps);
            return fewest + (2 * (limits.MaxCount - fewest) * position + steps) / (2 * steps);
        }

        // The level's grid actors in catalogue order, each at the share of its count the stretch leaves it, then the
        // meadow's mushrooms. The mushrooms come from the stretch, not from a pick, so no budget pays for them, and
        // the bushes get the slots the mushrooms leave.
        private static GridActorTypeGate[] GridActorGates(LevelGenerationSettings settings, int level,
            ScenarioStretch stretch, HashSet<int> included, bool extraColor)
        {
            var gates = new List<GridActorTypeGate>();

            // An extra-colour level has no grid actors at all, so none of the meadow's mushrooms either. The
            // shared limit counts caps, and the most the level's walls may hold is what the bushes go without.
            var placement = settings.Mushrooms.Placement;
            var walls = extraColor ? 0 : MushroomWallsFor(stretch, placement);
            var bushRoom = settings.Mushrooms.PermanentSlotLimit - placement.MaxCapsFor(walls);

            for (var i = 0; i < settings.GridActors.Count; i++)
            {
                if (!included.Contains(Key(GridActorKind, i))
                    || (extraColor && settings.GridActors[i].Role == ContentRole.Threat))
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
                if (share <= 0f)
                {
                    continue;
                }

                gate = Scaled(gate, share);
                if (gate.Type != GridActorType.Bush || TryFitBushes(ref gate, bushRoom))
                {
                    gates.Add(gate);
                }
            }

            // Fixed, so resolving it rolls nothing and the level's other counts come out as they would without it.
            // The mushroom gate counts walls: the spawner asks its strategy for that many and places their caps.
            if (walls > 0)
            {
                gates.Add(new GridActorTypeGate(GridActorType.Mushroom, new RangedInt(walls, walls)));
            }

            return gates.ToArray();
        }

        // Every threat but the staple balloons, which are the level itself: an extra-colour level has no armour
        // and no unbreakables.
        private static bool IsDroppedThreat(BalloonContentEntry entry)
        {
            return entry.Role == ContentRole.Threat && entry.Cost > 0;
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
        // ones. Featured pieces are paid for first, so they can leave nothing for the optional picks. A piece the
        // level's scenario doesn't allow is left out before any budget is spent, so the budget buys other threats:
        // a bush can't eat a sky level's budget, nor the clouds, bushes and shot-bouncing pieces a water level's, nor
        // the toughs a meadow's.
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

                if (!piece.AllowedOn(slot.Scenario))
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

        // The bushes give way to the mushrooms, never the other way: room is what the mushrooms leave of the slots
        // the two share (IMushroomSettings.PermanentSlotLimit). False when no bush fits.
        private static bool TryFitBushes(ref GridActorTypeGate gate, int room)
        {
            if (room <= 0)
            {
                return false;
            }

            if (gate.Count.Min > room || gate.Count.Max > room)
            {
                var count = new RangedInt(Math.Min(gate.Count.Min, room), Math.Min(gate.Count.Max, room), gate.Count.Mode);
                gate = new GridActorTypeGate(gate.Type, count, gate.MaxPerCluster);
            }

            return true;
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

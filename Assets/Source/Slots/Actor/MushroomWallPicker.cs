using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>What <see cref="MushroomWallPicker" /> picks a layout for.</summary>
    internal struct MushroomWallPick
    {
        /// <summary>The slots a cap may take: the ones nothing stands in.</summary>
        public HashSet<Vector2Int> FreeSlots;

        public Vector2Int GridSize;

        /// <summary>How many walls the level asks for.</summary>
        public int Walls;

        /// <summary>The level's roll: the same seed and board give the same layout.</summary>
        public int Seed;

        public MushroomPlacementLimits Limits;
    }

    /// <summary>
    ///     Picks a level's mushroom layout from the baked catalogue (<see cref="MushroomWallCatalogue" />): the
    ///     seed chooses where in the catalogue to start, and the first layout from there whose slots are all free
    ///     and allowed is the one. It flies nothing, so a deal costs a walk over a few dozen entries.
    /// </summary>
    internal static class MushroomWallPicker
    {
        private const string LogTag = "Mushrooms";

        /// <summary>
        ///     The layout for <paramref name="input" />, or null when none fits. When no layout of the walls asked
        ///     for fits the board, one wall fewer is tried, down to the limits' fewest; then none, with a warning.
        ///     Fewer walls asked for than the fewest places none.
        /// </summary>
        internal static MushroomWallLayout Pick(in MushroomWallPick input)
        {
            var limits = input.Limits;
            for (var walls = Mathf.Min(input.Walls, limits.MaxCount); walls >= Mathf.Max(1, limits.MinCount); walls--)
            {
                var layouts = MushroomWallCatalogue.For(walls);
                if (layouts.Count == 0)
                {
                    continue;
                }

                // Each count draws its own start, so a board that takes one wall fewer is not stuck on one entry.
                var start = new XorShift32(input.Seed + walls).Next(layouts.Count);
                for (var step = 0; step < layouts.Count; step++)
                {
                    var layout = layouts[(start + step) % layouts.Count];
                    if (Fits(layout, input))
                    {
                        return layout;
                    }
                }
            }

            if (input.Walls >= limits.MinCount)
            {
                Log.Warn(LogTag, $"No layout of {input.Walls} walls or fewer fits the board: placing no mushrooms.");
            }

            return null;
        }

        private static bool Fits(MushroomWallLayout layout, in MushroomWallPick input)
        {
            if (layout.Caps.Count > input.Limits.MaxCaps)
            {
                return false;
            }

            for (var i = 0; i < layout.Caps.Count; i++)
            {
                var slot = layout.Caps[i].Slot;
                if (!input.Limits.Allows(slot, input.GridSize) || !input.FreeSlots.Contains(slot))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

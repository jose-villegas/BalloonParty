using System;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>Pure arithmetic behind the initial fill's vertical heavy-type layering.</summary>
    internal static class InitialLayerPlan
    {
        /// <summary>
        /// Per-line heavy-entry counts for the initial fill. Heavy types are round-robined onto the
        /// bottom line of each <paramref name="spacing"/>-tall segment, deepest line first so an uneven
        /// count settles lower; the total banded is capped at (layer lines × <paramref name="columns"/>).
        /// Returns an all-zero array — the caller then falls back to the plain lightest-first gradient —
        /// when <paramref name="spacing"/> &lt; 2, there are no heavies, or the board is too short to hold
        /// a full segment.
        /// </summary>
        public static int[] HeavyPerLine(int heavyCount, int lineCount, int columns, int spacing)
        {
            var allocation = new int[Math.Max(0, lineCount)];
            if (heavyCount <= 0 || spacing < 2 || columns <= 0)
            {
                return allocation;
            }

            var layerCount = 0;
            for (var line = spacing - 1; line < lineCount; line += spacing)
            {
                layerCount++;
            }

            if (layerCount == 0)
            {
                return allocation;
            }

            var banded = Math.Min(heavyCount, layerCount * columns);
            for (var k = 0; k < banded; k++)
            {
                var segmentFromBottom = k % layerCount;
                var line = (spacing - 1) + (layerCount - 1 - segmentFromBottom) * spacing;
                allocation[line]++;
            }

            return allocation;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Capabilities
{
    /// <summary>Builders honouring <see cref="ScoreAttribution"/>'s one-entry-per-colour contract.</summary>
    public static class ScoreAttributions
    {
        // Shared scratch for the per-colour tally — main-thread only, grown on demand, never shrunk.
        private static int[] _counts = new int[8];

        /// <summary>
        ///     Splits <paramref name="points"/> across randomly picked colours and emits ONE aggregated
        ///     entry per colour that received any. Never one entry per point — that fans a single pop
        ///     into many 1-point score groups downstream, which starves the shape decomposition and
        ///     publishes a message per point instead of per colour.
        /// </summary>
        public static void AddRandomPerColor(
            IList<ScoreAttribution> results, IReadOnlyList<string> colors, int points, bool breaksStreak)
        {
            if (colors == null || colors.Count == 0 || points <= 0)
            {
                return;
            }

            if (_counts.Length < colors.Count)
            {
                _counts = new int[Mathf.NextPowerOfTwo(colors.Count)];
            }

            for (var i = 0; i < colors.Count; i++)
            {
                _counts[i] = 0;
            }

            for (var i = 0; i < points; i++)
            {
                _counts[Random.Range(0, colors.Count)]++;
            }

            for (var i = 0; i < colors.Count; i++)
            {
                if (_counts[i] > 0)
                {
                    results.Add(new ScoreAttribution(colors[i], _counts[i], breaksStreak));
                }
            }
        }
    }
}

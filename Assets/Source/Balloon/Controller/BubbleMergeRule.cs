using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     A pair of neighbouring bubble clusters one nudge brought closer: <see cref="Absorbed" /> flows into
    ///     <see cref="Survivor" />, and <see cref="Approach" /> is how much closer the nudge took them (world units).
    /// </summary>
    internal readonly struct BubbleMergeCandidate
    {
        public readonly Vector2Int Absorbed;
        public readonly Vector2Int Survivor;
        public readonly float Approach;

        public BubbleMergeCandidate(Vector2Int absorbed, Vector2Int survivor, float approach)
        {
            Absorbed = absorbed;
            Survivor = survivor;
            Approach = approach;
        }
    }

    /// <summary>
    ///     When two neighbouring bubble clusters merge: only when a nudge moves them closer together, measured at
    ///     the nudge's peak (each cluster's rest position plus its push; a cluster the nudge didn't touch has no
    ///     push), and only when their bubbles fit in one cluster. Sitting side by side, a spawn, a push apart or a
    ///     push that slides them past each other at the same distance never merges.
    /// </summary>
    internal static class BubbleMergeRule
    {
        // World units the nudge must close the gap by: float noise on a sideways push is not "closer".
        internal const float MinApproach = 1e-3f;

        private static readonly ApproachComparer ByApproachDescending = new();

        /// <summary>How much closer the nudge brings the two at its peak: above 0 closer, 0 or below apart or level.</summary>
        public static float Approach(Vector2 a, Vector2 pushA, Vector2 b, Vector2 pushB)
        {
            return Vector2.Distance(a, b) - Vector2.Distance(a + pushA, b + pushB);
        }

        public static bool Merges(Vector2 a, Vector2 pushA, Vector2 b, Vector2 pushB, int bubblesA, int bubblesB,
            int maxBubbles)
        {
            return bubblesA + bubblesB <= maxBubbles && Approach(a, pushA, b, pushB) > MinApproach;
        }

        /// <summary>
        ///     Whether <paramref name="pushA" /> carries a toward b more than <paramref name="pushB" /> carries b toward
        ///     a: the one pushed toward the other is the one that flows into it. A tie goes to the lower slot, so the
        ///     pair is decided once.
        /// </summary>
        public static bool IsAbsorbed(Vector2Int slotA, Vector2 a, Vector2 pushA, Vector2Int slotB, Vector2 b,
            Vector2 pushB)
        {
            var toward = (b - a).normalized;
            var aIn = Vector2.Dot(pushA, toward);
            var bIn = Vector2.Dot(pushB, -toward);
            if (!Mathf.Approximately(aIn, bIn))
            {
                return aIn > bIn;
            }

            return slotA.x != slotB.x ? slotA.x < slotB.x : slotA.y < slotB.y;
        }

        /// <summary>
        ///     Picks the merges one nudge makes from its candidates, each cluster in at most one: the pairs the nudge
        ///     closed most go first, so a cluster pushed toward two neighbours flows into the one it approached most,
        ///     and a merged cluster waits for a later nudge to merge again. Sorts <paramref name="candidates" />.
        /// </summary>
        public static void Choose(List<BubbleMergeCandidate> candidates, List<BubbleMergeCandidate> chosen,
            HashSet<Vector2Int> used)
        {
            chosen.Clear();
            used.Clear();
            candidates.Sort(ByApproachDescending);
            foreach (var candidate in candidates)
            {
                if (used.Contains(candidate.Absorbed) || used.Contains(candidate.Survivor))
                {
                    continue;
                }

                used.Add(candidate.Absorbed);
                used.Add(candidate.Survivor);
                chosen.Add(candidate);
            }
        }

        // A class comparer, so List.Sort takes no per-call delegate.
        private sealed class ApproachComparer : IComparer<BubbleMergeCandidate>
        {
            public int Compare(BubbleMergeCandidate x, BubbleMergeCandidate y)
            {
                return y.Approach.CompareTo(x.Approach);
            }
        }
    }
}

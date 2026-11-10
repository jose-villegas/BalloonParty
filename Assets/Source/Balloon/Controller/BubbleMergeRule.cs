using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     A pair of neighbouring bubble clusters that can merge: <see cref="Absorbed" /> flows into
    ///     <see cref="Survivor" />, which then holds <see cref="Bubbles" />.
    /// </summary>
    internal readonly struct BubbleMergeCandidate
    {
        public readonly Vector2Int Absorbed;
        public readonly Vector2Int Survivor;
        public readonly int Bubbles;

        public BubbleMergeCandidate(Vector2Int absorbed, Vector2Int survivor, int bubbles)
        {
            Absorbed = absorbed;
            Survivor = survivor;
            Bubbles = bubbles;
        }
    }

    /// <summary>
    ///     When two bubble clusters merge: whenever they sit in neighbouring slots and their bubbles fit in one
    ///     cluster. The smaller flows into the larger.
    /// </summary>
    internal static class BubbleMergeRule
    {
        private static readonly CandidateComparer ByBubblesDescending = new();

        public static bool Merges(int bubblesA, int bubblesB, int maxBubbles)
        {
            return bubblesA > 0 && bubblesB > 0 && bubblesA + bubblesB <= maxBubbles;
        }

        /// <summary>
        ///     Whether a flows into b: the smaller cluster is absorbed. Between equals, the one in the higher slot
        ///     (column, then row) flows into the lower, so the pair is decided once.
        /// </summary>
        public static bool IsAbsorbed(Vector2Int slotA, int bubblesA, Vector2Int slotB, int bubblesB)
        {
            if (bubblesA != bubblesB)
            {
                return bubblesA < bubblesB;
            }

            return slotA.x != slotB.x ? slotA.x > slotB.x : slotA.y > slotB.y;
        }

        /// <summary>
        ///     Picks the merges one pass makes from its candidates, each cluster in at most one: the merges that make
        ///     the biggest clusters go first, so a cluster between two neighbours joins the one it makes biggest. A
        ///     merged cluster can merge again on a later pass. Sorts <paramref name="candidates" />.
        /// </summary>
        public static void Choose(List<BubbleMergeCandidate> candidates, List<BubbleMergeCandidate> chosen,
            HashSet<Vector2Int> used)
        {
            chosen.Clear();
            used.Clear();
            candidates.Sort(ByBubblesDescending);
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

        // A class comparer, so List.Sort takes no per-call delegate. Ties break on the slots, so the order (and with
        // it the merges) never depends on the unstable sort.
        private sealed class CandidateComparer : IComparer<BubbleMergeCandidate>
        {
            public int Compare(BubbleMergeCandidate x, BubbleMergeCandidate y)
            {
                var byBubbles = y.Bubbles.CompareTo(x.Bubbles);
                if (byBubbles != 0)
                {
                    return byBubbles;
                }

                var bySurvivor = Order(x.Survivor).CompareTo(Order(y.Survivor));
                return bySurvivor != 0 ? bySurvivor : Order(x.Absorbed).CompareTo(Order(y.Absorbed));
            }

            private static long Order(Vector2Int slot)
            {
                return ((long)slot.x << 32) | (uint)slot.y;
            }
        }
    }
}

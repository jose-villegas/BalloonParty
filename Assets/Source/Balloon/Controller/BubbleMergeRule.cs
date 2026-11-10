using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     A pair of neighbouring bubble clusters where <see cref="Giver" /> passes bubbles to
    ///     <see cref="Receiver" />, which then holds <see cref="Bubbles" />.
    /// </summary>
    internal readonly struct BubbleMergeCandidate
    {
        public readonly Vector2Int Giver;
        public readonly Vector2Int Receiver;
        public readonly int Bubbles;

        public BubbleMergeCandidate(Vector2Int giver, Vector2Int receiver, int bubbles)
        {
            Giver = giver;
            Receiver = receiver;
            Bubbles = bubbles;
        }
    }

    /// <summary>
    ///     How two bubble clusters in neighbouring slots merge: the smaller gives its bubbles to the larger, as many
    ///     as the larger has room for. A giver left with none is absorbed; one with bubbles left stays beside a full
    ///     cluster.
    /// </summary>
    internal static class BubbleMergeRule
    {
        private static readonly CandidateComparer ByBubblesDescending = new();

        /// <summary>
        ///     How many bubbles a giver passes to its receiver: all of them, or as many as fill the receiver to
        ///     <paramref name="maxBubbles" />. None when either has already popped.
        /// </summary>
        public static int Given(int giverBubbles, int receiverBubbles, int maxBubbles)
        {
            if (giverBubbles <= 0 || receiverBubbles <= 0)
            {
                return 0;
            }

            return Math.Clamp(maxBubbles - receiverBubbles, 0, giverBubbles);
        }

        /// <summary>
        ///     Whether a gives to b: the smaller cluster gives. Between equals, the one in the higher slot (column,
        ///     then row) gives to the lower, so the pair is decided once.
        /// </summary>
        public static bool IsGiver(Vector2Int slotA, int bubblesA, Vector2Int slotB, int bubblesB)
        {
            if (bubblesA != bubblesB)
            {
                return bubblesA < bubblesB;
            }

            return slotA.x != slotB.x ? slotA.x > slotB.x : slotA.y > slotB.y;
        }

        /// <summary>
        ///     Picks the merges one pass makes from its candidates, each cluster in at most one: the merges that make
        ///     the biggest clusters go first, so a cluster between two neighbours gives to the one it makes biggest.
        ///     A cluster can merge again on a later pass. Sorts <paramref name="candidates" />.
        /// </summary>
        public static void Choose(List<BubbleMergeCandidate> candidates, List<BubbleMergeCandidate> chosen,
            HashSet<Vector2Int> used)
        {
            chosen.Clear();
            used.Clear();
            candidates.Sort(ByBubblesDescending);
            foreach (var candidate in candidates)
            {
                if (used.Contains(candidate.Giver) || used.Contains(candidate.Receiver))
                {
                    continue;
                }

                used.Add(candidate.Giver);
                used.Add(candidate.Receiver);
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

                var byReceiver = Order(x.Receiver).CompareTo(Order(y.Receiver));
                return byReceiver != 0 ? byReceiver : Order(x.Giver).CompareTo(Order(y.Giver));
            }

            private static long Order(Vector2Int slot)
            {
                return ((long)slot.x << 32) | (uint)slot.y;
            }
        }
    }
}

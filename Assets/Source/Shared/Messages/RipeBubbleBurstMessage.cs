using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>How a ripe bubble cluster went: popped by the player in time, or left until its fuse ran out.</summary>
    public enum RipeBurstKind
    {
        Ripple,
        Foam
    }

    /// <summary>Where one bubble of a foam's hand-out went.</summary>
    public enum FoamGift
    {
        /// <summary>A coat on the plain balloon in its direction.</summary>
        Coat,

        /// <summary>One more bubble for the cluster in its direction.</summary>
        Cluster,

        /// <summary>No taker had room: it popped harmlessly.</summary>
        Leftover
    }

    /// <summary>
    ///     Published by <c>RipeBubbleService</c> when a ripe cluster bursts. <see cref="Neighbours" /> says which
    ///     neighbouring balloons the burst touched (a ripple's hits that landed, the foam's new coats), one bit per
    ///     direction in the order <c>HexCoordinates.HexNeighborIndices</c> lists the six neighbours of
    ///     <see cref="Slot" />, so a burst carries them without allocating. <see cref="Reached" /> is how many.
    ///     A foam also carries its hand-out (<see cref="Handout" />): where each of its bubbles went, in the order they
    ///     leave, one 4-bit entry per bubble (a direction, plus 8 for a cluster, or 15 for a leftover) and the count in
    ///     the seventh. Published before the hand-out changes the board, so the views still see it as it was.
    /// </summary>
    public readonly struct RipeBubbleBurstMessage
    {
        private const int EntryBits = 4;
        private const int EntryMask = 0xF;
        private const int ClusterEntry = 8;
        private const int LeftoverEntry = 15;
        private const int CountShift = EntryBits * MaxHandout;

        /// <summary>The most bubbles a hand-out carries: one per neighbour.</summary>
        public const int MaxHandout = 6;

        public RipeBurstKind Kind { get; }
        public Vector2Int Slot { get; }
        public Vector3 Position { get; }
        public int Neighbours { get; }
        public int Handout { get; }
        public int Reached => CountBits(Neighbours);

        /// <summary>How many bubbles the foam handed out, leftovers included; 0 for a ripple.</summary>
        public int Bubbles => HandoutCount(Handout);

        public RipeBubbleBurstMessage(RipeBurstKind kind, Vector2Int slot, Vector3 position, int neighbours,
            int handout = 0)
        {
            Kind = kind;
            Slot = slot;
            Position = position;
            Neighbours = neighbours;
            Handout = handout;
        }

        /// <summary>Whether the burst touched the neighbour in <paramref name="direction" /> (0 to 5).</summary>
        public bool Touched(int direction)
        {
            return (Neighbours & (1 << direction)) != 0;
        }

        /// <summary>Where the foam's bubble <paramref name="bubble" /> (0 first) went, and in which direction; a
        /// leftover's direction is -1.</summary>
        public FoamGift GiftOf(int bubble, out int direction)
        {
            return HandoutGift(Handout, bubble, out direction);
        }

        /// <summary><paramref name="handout" /> with one more bubble, the next to leave, going to
        /// <paramref name="gift" /> in <paramref name="direction" /> (ignored for a leftover). A full hand-out is
        /// returned as it is.</summary>
        public static int AddGift(int handout, FoamGift gift, int direction)
        {
            var count = HandoutCount(handout);
            if (count >= MaxHandout)
            {
                return handout;
            }

            var entry = gift switch
            {
                FoamGift.Coat => direction,
                FoamGift.Cluster => ClusterEntry + direction,
                _ => LeftoverEntry
            };
            var cleared = handout & ~(EntryMask << CountShift);
            return cleared | (entry << (EntryBits * count)) | ((count + 1) << CountShift);
        }

        public static int HandoutCount(int handout)
        {
            return (handout >> CountShift) & EntryMask;
        }

        public static FoamGift HandoutGift(int handout, int bubble, out int direction)
        {
            var entry = (handout >> (EntryBits * bubble)) & EntryMask;
            if (entry == LeftoverEntry)
            {
                direction = -1;
                return FoamGift.Leftover;
            }

            if (entry >= ClusterEntry)
            {
                direction = entry - ClusterEntry;
                return FoamGift.Cluster;
            }

            direction = entry;
            return FoamGift.Coat;
        }

        private static int CountBits(int mask)
        {
            var count = 0;
            for (var bits = mask; bits != 0; bits &= bits - 1)
            {
                count++;
            }

            return count;
        }
    }
}

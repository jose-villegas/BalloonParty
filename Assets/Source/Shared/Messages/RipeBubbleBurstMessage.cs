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
    ///     Published by <c>RipeBubbleService</c> when a ripe cluster bursts, built by <see cref="Ripple" /> or
    ///     <see cref="Foam" />. Either way <see cref="Reached" /> is how many balloons the burst touched, and the
    ///     message carries plain numbers only, so it allocates nothing.
    ///     A ripple is a blast: it hit every balloon it could within <see cref="Radius" /> world units of
    ///     <see cref="Position" />, and <see cref="Reached" /> counts the hits that landed.
    ///     A foam touches only the six neighbours of <see cref="Slot" />: <see cref="Neighbours" /> has one bit per
    ///     direction, in the order <c>HexCoordinates.HexNeighborIndices</c> lists them, for each neighbour it coated,
    ///     and <see cref="Reached" /> counts them. It also carries its hand-out (<see cref="Handout" />): where each
    ///     of its bubbles went, in the order they leave, one 4-bit entry per bubble (a direction, plus 8 for a
    ///     cluster, or 15 for a leftover) and the count in the seventh. It is published before the hand-out changes
    ///     the board, so the views still see it as it was.
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

        /// <summary>How far a ripple's blast reached from <see cref="Position" />, in world units; 0 for a foam.</summary>
        public float Radius { get; }

        /// <summary>The balloons the burst touched: a ripple's hits that landed, a foam's new coats.</summary>
        public int Reached { get; }

        /// <summary>The neighbours a foam coated, a bit per direction; 0 for a ripple, which is not bound to them.</summary>
        public int Neighbours { get; }

        public int Handout { get; }

        /// <summary>How many bubbles the foam handed out, leftovers included; 0 for a ripple.</summary>
        public int Bubbles => HandoutCount(Handout);

        private RipeBubbleBurstMessage(RipeBurstKind kind, Vector2Int slot, Vector3 position, float radius,
            int reached, int neighbours, int handout)
        {
            Kind = kind;
            Slot = slot;
            Position = position;
            Radius = radius;
            Reached = reached;
            Neighbours = neighbours;
            Handout = handout;
        }

        /// <summary>The player's burst: a blast of <paramref name="radius" /> world units round
        /// <paramref name="position" /> whose hits landed on <paramref name="reached" /> balloons.</summary>
        public static RipeBubbleBurstMessage Ripple(Vector2Int slot, Vector3 position, float radius, int reached)
        {
            return new RipeBubbleBurstMessage(RipeBurstKind.Ripple, slot, position, radius, reached, 0, 0);
        }

        /// <summary>The level's burst: the neighbours it coated (<paramref name="coated" />, a bit per direction)
        /// and its <paramref name="handout" />.</summary>
        public static RipeBubbleBurstMessage Foam(Vector2Int slot, Vector3 position, int coated, int handout = 0)
        {
            return new RipeBubbleBurstMessage(
                RipeBurstKind.Foam, slot, position, 0f, CountBits(coated), coated, handout);
        }

        /// <summary>Whether a foam coated the neighbour in <paramref name="direction" /> (0 to 5).</summary>
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

using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published by <c>BubbleMergeService</c> as one bubble cluster gives bubbles to its neighbour, just before the
    ///     two counts change, so a view can still read what each showed. <see cref="Absorbed" /> is set when the giver
    ///     gave everything and flows into the receiver; otherwise it stays in <see cref="GiverSlot" /> with fewer bubbles.
    /// </summary>
    public readonly struct BubbleMergedMessage
    {
        public Vector2Int GiverSlot { get; }
        public Vector2Int ReceiverSlot { get; }
        public Vector3 From { get; }
        public Vector3 To { get; }
        public int Given { get; }
        public bool Absorbed { get; }

        public BubbleMergedMessage(
            Vector2Int giverSlot, Vector2Int receiverSlot, Vector3 from, Vector3 to, int given, bool absorbed)
        {
            GiverSlot = giverSlot;
            ReceiverSlot = receiverSlot;
            From = from;
            To = to;
            Given = given;
            Absorbed = absorbed;
        }
    }
}

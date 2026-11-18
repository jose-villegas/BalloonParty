using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Published by <c>RipeBubbleService</c> when a bubble cluster reaches the merge cap and its fuse starts.</summary>
    public readonly struct BubbleRipenedMessage
    {
        public Vector2Int Slot { get; }
        public Vector3 Position { get; }

        public BubbleRipenedMessage(Vector2Int slot, Vector3 position)
        {
            Slot = slot;
            Position = position;
        }
    }
}

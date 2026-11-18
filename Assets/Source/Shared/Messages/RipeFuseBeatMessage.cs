using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published by <c>RipeBubbleService</c> on each of a ripe cluster's throbs: once a second, then the
    ///     half-second beats in the last <c>IRipeBubbleSettings.ThrobFastSeconds</c> of its fuse, which are
    ///     <see cref="Fast" />. Every beat is seen (soap spirals off the cluster); only the fast ones are heard.
    /// </summary>
    public readonly struct RipeFuseBeatMessage
    {
        public Vector2Int Slot { get; }
        public Vector3 Position { get; }
        public bool Fast { get; }

        public RipeFuseBeatMessage(Vector2Int slot, Vector3 position, bool fast)
        {
            Slot = slot;
            Position = position;
            Fast = fast;
        }
    }
}

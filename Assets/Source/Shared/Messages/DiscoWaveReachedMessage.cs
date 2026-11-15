using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published by the disco ball's wave for each balloon it paints, the paint-immune ones that take only the drip
    ///     included. <see cref="Reach" /> is the balloon's distance from the pop over the wave's full radius (0 to 1).
    /// </summary>
    public readonly struct DiscoWaveReachedMessage
    {
        public Vector3 Position { get; }
        public float Reach { get; }

        public DiscoWaveReachedMessage(Vector3 position, float reach)
        {
            Position = position;
            Reach = reach;
        }
    }
}

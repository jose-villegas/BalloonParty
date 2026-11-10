using UnityEngine;

namespace BalloonParty.Game.HeadsUp
{
    /// <summary>
    ///     Where a heads-up is written: a fixed world point (where something happened), or a
    ///     <c>TrailEndpointRegistry</c> key (the cannon, the heart, a colour's bar) that the line follows.
    /// </summary>
    internal readonly struct HeadsUpAnchor
    {
        public readonly Vector2 Position;

        /// <summary>The endpoint's key, or null for a fixed point.</summary>
        public readonly string EndpointKey;

        public bool IsEndpoint => EndpointKey != null;

        private HeadsUpAnchor(Vector2 position, string endpointKey)
        {
            Position = position;
            EndpointKey = endpointKey;
        }

        public static HeadsUpAnchor At(Vector2 position)
        {
            return new HeadsUpAnchor(position, null);
        }

        public static HeadsUpAnchor Endpoint(string key)
        {
            return new HeadsUpAnchor(Vector2.zero, key);
        }
    }
}

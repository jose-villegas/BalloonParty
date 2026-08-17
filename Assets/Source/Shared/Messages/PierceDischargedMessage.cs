using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published when a piercing shot discharges — shattering the toughs it plowed through. Carries
    ///     the centre of the plowed line, how many toughs it ate (the charge), whether the shot was
    ///     rainbow, and the shot's travel direction at the moment of discharge (already the post-bounce
    ///     direction — see <c>ProjectileHitResolver.DischargePending</c>). The discharge feel subscribes:
    ///     the rainbow bloom, and (later) lights/shockwave.
    /// </summary>
    public readonly struct PierceDischargedMessage
    {
        public Vector3 Center { get; }
        public int ToughCount { get; }
        public bool IsRainbow { get; }
        public Vector3 Direction { get; }

        public PierceDischargedMessage(Vector3 center, int toughCount, bool isRainbow, Vector3 direction = default)
        {
            Center = center;
            ToughCount = toughCount;
            IsRainbow = isRainbow;
            Direction = direction;
        }
    }
}

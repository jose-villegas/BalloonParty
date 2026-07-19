using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published when a piercing shot discharges — shattering the toughs it plowed through. Carries
    ///     the centre of the plowed line, how many toughs it ate (the charge), and whether the shot was
    ///     rainbow. The discharge feel subscribes: the rainbow bloom, and (later) lights/shockwave.
    /// </summary>
    public readonly struct PierceDischargedMessage
    {
        public Vector3 Center { get; }
        public int ToughCount { get; }
        public bool IsRainbow { get; }

        public PierceDischargedMessage(Vector3 center, int toughCount, bool isRainbow)
        {
            Center = center;
            ToughCount = toughCount;
            IsRainbow = isRainbow;
        }
    }
}

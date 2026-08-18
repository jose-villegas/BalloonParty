using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published when a piercing shot discharges — shattering the toughs it plowed through. Carries
    ///     the centre of the plowed line (<see cref="Center" />, used to shape the rainbow bloom's
    ///     radius), how many toughs it ate (the charge), whether the shot was rainbow, the direction the
    ///     shot is travelling as of that moment (already reflected off the wall it just discharged at —
    ///     see <c>ProjectileHitResolver.DischargePending</c>, called after the bounce), and where the shot
    ///     actually is right now (<see cref="Position" /> — the wall-contact point, NOT <see cref="Center"
    ///     />, which sits back along the plowed line the shot has already left behind). A rainbow bloom
    ///     must anchor its "don't paint what I'm about to re-enter" test on <see cref="Position" />: the
    ///     shot resumes travel from there, not from the plowed line's centre. <see cref="Direction" /> and
    ///     <see cref="Position" /> are both required (no defaulting) — an anti-exploit guard that silently
    ///     falls back to "exclude nothing" on a forgotten argument is worse than a compile error. The
    ///     discharge feel subscribes: the rainbow bloom, and (later) lights/shockwave.
    /// </summary>
    public readonly struct PierceDischargedMessage
    {
        public Vector3 Center { get; }
        public int ToughCount { get; }
        public bool IsRainbow { get; }
        public Vector3 Direction { get; }
        public Vector3 Position { get; }

        public PierceDischargedMessage(Vector3 center, int toughCount, bool isRainbow, Vector3 direction, Vector3 position)
        {
            Center = center;
            ToughCount = toughCount;
            IsRainbow = isRainbow;
            Direction = direction;
            Position = position;
        }
    }
}

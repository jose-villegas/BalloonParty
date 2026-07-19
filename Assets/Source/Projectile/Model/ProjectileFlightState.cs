using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    // A tough/unbreakable balloon the piercing shot plowed through but hasn't popped yet — held until
    // the discharge shatters it, with the world position it was struck (for the discharge VFX).
    public readonly struct PendingPierceHit
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 Position;

        public PendingPierceHit(IBalloonModel balloon, Vector3 position)
        {
            Balloon = balloon;
            Position = position;
        }
    }

    // The motion resolver's per-shot scratch state — bookkeeping the algorithm needs, kept off the
    // status/intent surface of IWriteableProjectileModel.
    public class ProjectileFlightState : IProjectileFlightState
    {
        // hits>1 balloons the piercing shot has plowed through (not yet popped), and their strike
        // positions — shattered together at the discharge. Count doubles as the rainbow charge.
        public List<PendingPierceHit> PendingPierceHits { get; } = new();

        // Set by the hit resolver on each tough plow so the motion resolver (re)starts the discharge
        // countdown — a run of toughs keeps re-arming it, so the discharge fires after the LAST one.
        public bool DischargeArmed { get; set; }

        // Counts down while > 0; when it reaches 0 the motion resolver fires the discharge. 0 = idle.
        public float DischargeCountdown { get; set; }

        // Wall bounces since the last balloon contact — the cruise detector's counter.
        public int ConsecutiveWallBounces { get; set; }

        // Shield count captured at cruise entry: the ramp's denominator, so speed climbs from base
        // toward the max as the remaining shields spend down from this snapshot.
        public int CruiseStartShields { get; set; }

        // Seconds since the last cruise speed change (entry or bounce) — drives the per-tap
        // freeze-then-pickup animation envelope.
        public float CruiseTapElapsed { get; set; }

        // World position where the current flight segment began (last reflect/deflect, or the
        // muzzle) — the origin the last-shield ease traverses from.
        public Vector3 SegmentStartPosition { get; set; }

        // Seconds elapsed on the current segment — the last-shield ease normalizes to it so the
        // doomed drift takes a fixed wall-clock time regardless of the segment's length.
        public float SegmentElapsed { get; set; }
    }
}

using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    // The motion resolver's per-shot scratch state — bookkeeping the algorithm needs, kept off the
    // status/intent surface of IWriteableProjectileModel.
    public class ProjectileFlightState : IProjectileFlightState
    {
        // Wall bounces since the last balloon contact — the cruise detector's counter.
        public int ConsecutiveWallBounces { get; set; }

        // Shield count captured at cruise entry: the ramp's denominator, so speed climbs from base
        // toward the max as the remaining shields spend down from this snapshot.
        public int CruiseStartShields { get; set; }

        // Seconds since the last cruise speed change (entry or bounce) — drives the per-tap
        // freeze-then-pickup animation envelope.
        public float CruiseTapElapsed { get; set; }

        // Multiplies the cruise speed, decaying as a PIERCING shot plows through tough (>1-hit)
        // actors — halved per such pierce, floored so total speed never drops below base, reset to 1
        // when a wall bounce ends the cruise. 1 = no decay.
        public float CruisePierceSpeedScale { get; set; } = 1f;

        // World position where the current flight segment began (last reflect/deflect, or the
        // muzzle) — the origin the last-shield ease traverses from.
        public Vector3 SegmentStartPosition { get; set; }

        // Seconds elapsed on the current segment — the last-shield ease normalizes to it so the
        // doomed drift takes a fixed wall-clock time regardless of the segment's length.
        public float SegmentElapsed { get; set; }
    }
}

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

        // Whether the shot was rainbow-buffed when it plowed a tough — captured at plow time because the
        // discharge ends the pierce (dropping the RainbowShield buff) BEFORE the discharge is resolved,
        // so HasBuff would already read false by then.
        public bool PierceWasRainbow { get; set; }

        // While a discharge is scheduled, counts down each tick; the discharge fires when it reaches 0.
        // The value alone can't say "scheduled" — a 0 delay is a legitimate fire-next-tick, so
        // DischargeScheduled is the authority (see below).
        public float DischargeCountdown { get; set; }

        // Whether a discharge is scheduled: set when the debounce (re)arms, cleared when it fires. This
        // is what gates the countdown — NOT DischargeCountdown > 0, which would swallow a 0-delay config
        // (the countdown would park at 0 and never tick down past it).
        public bool DischargeScheduled { get; set; }

        // Wall bounces since the last balloon contact — the cruise detector's counter.
        public int ConsecutiveWallBounces { get; set; }

        // Cruise-wall taps plus Sweep taps earned so far this shot — the shared piercing threshold.
        public int TotalCruiseTaps { get; set; }

        // Seconds since the last cruise speed change (entry or bounce) — drives the per-tap
        // freeze-then-pickup animation envelope.
        public float CruiseTapElapsed { get; set; }

        // Total sweeps detected (clear-corridor passes). Compared against SweepTapThreshold to gate
        // whether speed taps actually apply.
        public int TotalSweeps { get; set; }

        // Balloon pops since the last wall bounce — the Sweep gate on the current straight segment.
        public int SegmentPopCount { get; set; }

        // Starts true on each segment and is cleared by any contact that was not a 1HP one-shot pop,
        // so Sweep only rewards a full corridor clear of instant kills.
        public bool SegmentSweepValid { get; set; } = true;

        // World position where the last wall bounce happened (or the muzzle on the first leg) — the
        // Sweep back-trace origin.
        public Vector3 LastBouncePosition { get; set; }

        // World position where the current flight segment began (last reflect/deflect, or the
        // muzzle) — the origin the last-shield ease traverses from.
        public Vector3 SegmentStartPosition { get; set; }

        // Seconds elapsed on the current segment — the last-shield ease normalizes to it so the
        // doomed drift takes a fixed wall-clock time regardless of the segment's length.
        public float SegmentElapsed { get; set; }
    }
}

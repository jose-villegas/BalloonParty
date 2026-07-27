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

    /// <summary>
    ///     Which speed change is currently playing out. Both are the same eased blend toward the tap
    ///     count's target speed, differing only in where they start: a tap beat starts from a standstill
    ///     (the deliberate freeze-then-pickup punctuation of earning a tap), an arm ramp from the speed
    ///     the shot was already travelling at (so arming accelerates instead of snapping to top speed).
    /// </summary>
    public enum SpeedTransitionKind
    {
        None,
        TapBeat,
        ArmRamp,
    }

    // The motion resolver's per-shot scratch state — bookkeeping the algorithm needs, kept off the
    // status/intent surface of IWriteableProjectileModel.
    public class ProjectileFlightState : IProjectileFlightState
    {
        // hits>1 balloons the piercing shot has plowed through (not yet popped), and their strike
        // positions — shattered together at the wall-discharge. Count doubles as the rainbow charge.
        public List<PendingPierceHit> PendingPierceHits { get; } = new();

        // Whether the shot was rainbow-buffed when it plowed a tough — captured at plow time because the
        // discharge ends the pierce (dropping the RainbowShield buff) BEFORE the discharge is resolved,
        // so HasBuff would already read false by then.
        public bool PierceWasRainbow { get; set; }

        // Snipe pickups taken while the shot was ALREADY piercing. Two pierces can't overlap, so the
        // grant is saved instead of wasted: each charge re-arms the lance at the discharge that spends
        // the current pierce (see ProjectileModelExtensions.SpendPierce). Rainbow charges are counted
        // separately and spent first, since a rainbow host's grant is the stronger one.
        public int BankedPierceCharges { get; set; }

        public int BankedRainbowPierceCharges { get; set; }

        // Wall bounces since the last balloon contact — the cruise detector's counter.
        public int ConsecutiveWallBounces { get; set; }

        // Cruise-wall taps plus Sweep taps earned so far this shot — the shared piercing threshold.
        public int TotalCruiseTaps { get; set; }

        // Monotonic id of the surviving wall bounce being resolved, and the one a tap was last minted
        // at. A wall hit mints AT MOST ONE tap, whichever rule earned it (an empty-corridor cruise
        // bounce or a clean sweep) — comparing these two enforces that, where it used to rest on the
        // two grant sites happening to be mutually exclusive. Deflects don't bump the sequence: a
        // deflect isn't a wall hit. Monotonic, so there is never a reset to order correctly.
        public int WallHitSequence { get; set; }

        public int LastTapWallHit { get; set; } = -1;

        // The speed change in flight: which kind, the speed it blends FROM, and how far into it we are.
        // One mechanism covers both the per-tap beat and the arm ramp — they differ only by anchor
        // (see SpeedTransitionKind), so there is exactly one "how far into the current change" field
        // rather than one per mechanism that could disagree.
        public SpeedTransitionKind TransitionKind { get; set; }

        public float TransitionFromSpeed { get; set; }

        public float TransitionElapsed { get; set; }

        // The speed the shot is actually travelling this step — written by the motion resolver, so
        // feedback that scales with velocity reads the real thing instead of re-deriving it (or reading
        // the constant base speed).
        public float CurrentSpeed { get; set; }

        // Length of the CURRENT run of clean clearing passes — segments spent breezing through 1-HP
        // balloons with the corridor clear behind. SweepTapThreshold is the run length that earns taps, so
        // any wall reached without one resets this (mirroring how a balloon contact breaks cruise's own run
        // in ConsecutiveWallBounces).
        public int ConsecutiveSweeps { get; set; }

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

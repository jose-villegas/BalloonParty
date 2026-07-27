using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using UnityEngine;
using VContainer;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>What a sweep attempt did, so the caller can tell its editor visuals the same story.</summary>
    internal enum ProjectileSweepOutcome
    {
        /// <summary>The segment wasn't a clean clearing pass, so the run restarted.</summary>
        RunBroken,

        /// <summary>A clean pass, but the run is still short of <c>SweepTapThreshold</c>.</summary>
        Credited,

        /// <summary>A clean pass at or past the threshold — a tap was claimed for this wall.</summary>
        Paid,
    }

    /// <summary>
    ///     Owns the projectile's speed economy end to end: who may mint a tap, how many exist, and what
    ///     speed they buy. Per-shot state lives on <see cref="ProjectileFlightState" /> (this type is a
    ///     singleton, the shots are not), which is also what lets the "one tap per wall hit" rule be
    ///     enforced rather than inferred — both grant rules come through <see cref="TryGrantTap" />.
    ///     Physics never enters: the sweep's corridor probe arrives as a delegate, so the rule is
    ///     headless-testable and the solver can hand in its own analytic trace.
    /// </summary>
    internal sealed class ProjectileTapResolver
    {
        private readonly float _speedGainPerTap;
        private readonly float _maxSpeedMultiplier;
        private readonly float _cruiseTapEaseDuration;
        private readonly float _pierceArmRampDuration;
        private readonly int _piercingTapThreshold;
        private readonly bool _sweepEnabled;
        private readonly int _sweepTapThreshold;
        private readonly AnimationCurve _cruiseTapCurve;
        private readonly AnimationCurve _pierceArmRampCurve;

        /// <summary>
        ///     The fastest a shot can actually travel, as a multiple of base speed — what feedback scaling
        ///     with velocity normalizes against. Taps accrue for the whole flight (piercing included), so
        ///     the speed rail is the real ceiling; with the rail disabled there is no bound at all, and the
        ///     arming tap's multiplier is the best available reference point.
        /// </summary>
        internal float ReachableTopSpeedMultiplier => _maxSpeedMultiplier > 0f
            ? _maxSpeedMultiplier
            : Mathf.Max(TargetMultiplier(_piercingTapThreshold), 1f);

        [Inject]
        internal ProjectileTapResolver(IProjectileFlightConfig config)
        {
            _speedGainPerTap = config.SpeedGainPerTap;
            _maxSpeedMultiplier = config.MaxSpeedMultiplier;
            _cruiseTapEaseDuration = config.CruiseTapEaseDuration;
            _pierceArmRampDuration = config.PierceArmRampDuration;
            _piercingTapThreshold = config.CruisePiercingTapThreshold;
            _sweepEnabled = config.SweepEnabled;
            _sweepTapThreshold = config.SweepTapThreshold;
            _cruiseTapCurve = config.CruiseTapCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            // Blends between two real speeds (see ResolveSpeed), so an empty curve is harmless here — it
            // just holds the arming speed — but a linear fallback keeps it accelerating.
            _pierceArmRampCurve = config.PierceArmRampCurve is { length: > 0 }
                ? config.PierceArmRampCurve
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        /// <summary>
        ///     Mints a tap for the wall hit in progress — the single funnel both grant rules come through,
        ///     so a wall hit pays at most one whichever earned it. Returns whether a tap was minted; also
        ///     owns what a tap DOES, so the two callers can't drift apart on it.
        /// </summary>
        internal bool TryGrantTap(IWriteableProjectileModel model)
        {
            var flight = model.Flight;

            // One tap per wall hit, whichever rule gets there first — and while piercing BOTH genuinely
            // can. A pop doesn't end the cruise for an armed shot (ProjectileHitResolver's `!isPiercing`
            // guard), so an armed shot that pops 1-HP balloons reaches the wall with a cruise bounce AND a
            // clean sweep to its name; unarmed, the pop would have cancelled the cruise and only the sweep
            // could claim it. Refusing the second claim is what keeps that from paying twice — which is
            // exactly what an armed cruising shot used to do, compounding from there. Both claims are worth
            // the same one tap, so first-come costs the player nothing.
            if (flight.LastTapWallHit == flight.WallHitSequence)
            {
                return false;
            }

            flight.LastTapWallHit = flight.WallHitSequence;
            flight.TotalCruiseTaps++;

            // A long-enough run ARMS the shot: from this tap on it pierces everything it touches
            // (unbreakables included) for the rest of its life. Taps keep accruing past that point — a wall
            // hit is a wall hit — but an armed shot rides the RAMP rather than the beat: it accelerates
            // from the speed it is already travelling into the new target, instead of dipping to a
            // standstill first. Re-arming is idempotent, so each further tap simply re-anchors that ramp;
            // only the speed rail bounds where this ends up.
            if (_piercingTapThreshold > 0 && flight.TotalCruiseTaps >= _piercingTapThreshold)
            {
                model.ArmPierce();
            }
            else
            {
                model.BeginTapBeat();
            }

            return true;
        }

        /// <summary>
        ///     The Sweep rule at a wall: a RUN of <c>SweepTapThreshold</c> consecutive segments spent
        ///     breezing through 1-HP balloons. Any wall reached without a clean clearing pass — no pops at
        ///     all (empty, which is cruise's business), a tougher contact, or a corridor still occupied —
        ///     breaks the run and starts the count over, symmetric with cruise's own run, which any balloon
        ///     contact breaks. <paramref name="corridorBlocked" /> probes the just-flown span; the caller
        ///     supplies it so no physics dependency reaches in here.
        /// </summary>
        internal ProjectileSweepOutcome TryAwardSweepTap(
            IWriteableProjectileModel model, Vector3 wallHitPosition, Vector3 travelDirection,
            PathTrace.SegmentBlocked corridorBlocked)
        {
            if (!ClearedTheSegment(model, wallHitPosition, travelDirection, corridorBlocked))
            {
                model.Flight.ConsecutiveSweeps = 0;
                return ProjectileSweepOutcome.RunBroken;
            }

            model.Flight.ConsecutiveSweeps++;

            if (_sweepTapThreshold > 0 && model.Flight.ConsecutiveSweeps < _sweepTapThreshold)
            {
                return ProjectileSweepOutcome.Credited;
            }

            TryGrantTap(model);
            return ProjectileSweepOutcome.Paid;
        }

        /// <summary>
        ///     The flight speed for this step: the tap count sets the target, and whichever speed transition
        ///     is in flight eases toward it from its own anchor. A tap beat anchors at a standstill and an
        ///     arm ramp at the speed the shot armed with, which is the only difference between them — for a
        ///     beat, <c>Lerp(0, target, curve)</c> is exactly the old "target × curve" envelope. Publishes
        ///     the result on the model so velocity-scaled feedback reads the real speed.
        /// </summary>
        internal float ResolveSpeed(IWriteableProjectileModel model, float deltaTime)
        {
            var flight = model.Flight;
            var target = model.ComputeBuffedValue(ProjectileBuffId.Speed, model.Speed)
                         * TargetMultiplier(flight.TotalCruiseTaps);

            // The safety rail, applied to the FINAL speed: one fixed step has to stay short enough that the
            // shot can't skip straight past a balloon (or the play area) between steps. It clamps against
            // the UNBUFFED base so it's an absolute ceiling — a speed buff multiplies the target and would
            // otherwise sail through a cap that only wrapped the tap part.
            if (_maxSpeedMultiplier > 0f)
            {
                target = Mathf.Min(target, model.Speed * _maxSpeedMultiplier);
            }

            // With no taps banked there is nothing above base speed to blend toward, so an unspent
            // transition (cruise entry before its first tap) stays inert rather than dipping the shot.
            if (flight.TotalCruiseTaps <= 0)
            {
                flight.CurrentSpeed = target;
                return target;
            }

            var duration = TransitionDuration(flight.TransitionKind);
            if (duration <= 1e-4f)
            {
                flight.CurrentSpeed = target;
                return target;
            }

            var curve = flight.TransitionKind == SpeedTransitionKind.ArmRamp
                ? _pierceArmRampCurve
                : _cruiseTapCurve;
            var progress = Mathf.Clamp01(flight.TransitionElapsed / duration);
            flight.TransitionElapsed += deltaTime;
            flight.CurrentSpeed = Mathf.Lerp(
                flight.TransitionFromSpeed, target, Mathf.Clamp01(curve.Evaluate(progress)));
            return flight.CurrentSpeed;
        }

        /// <summary>Whether a tap's freeze-then-pickup beat is currently playing — feedback ducks on it.</summary>
        internal bool IsInTapBeat(IProjectileModel model)
        {
            return model.Flight.TransitionKind == SpeedTransitionKind.TapBeat
                   && model.Flight.TransitionElapsed < _cruiseTapEaseDuration;
        }

        // Did the segment just finished qualify as a clean clearing pass? It has to have popped something,
        // every contact on it has to have been a one-shot kill (SegmentSweepValid), and the corridor it flew
        // has to be clear NOW — probed backward from the wall over the span it covered.
        private bool ClearedTheSegment(
            IProjectileModel model, Vector3 wallHitPosition, Vector3 travelDirection,
            PathTrace.SegmentBlocked corridorBlocked)
        {
            if (!_sweepEnabled || model.Flight.SegmentPopCount <= 0 || !model.Flight.SegmentSweepValid)
            {
                return false;
            }

            var segmentLength = Vector3.Distance(model.Flight.LastBouncePosition, wallHitPosition);
            if (segmentLength <= 0f || travelDirection.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            var backward = -((Vector2)travelDirection).normalized;
            return !corridorBlocked(wallHitPosition, backward, segmentLength);
        }

        // Cumulative: every tap is worth SpeedGainPerTap of base speed. Unclamped — the speed rail is
        // applied to the final speed in ResolveSpeed, so buffs fall inside it too.
        private float TargetMultiplier(int taps)
        {
            return taps <= 0 ? 1f : 1f + (_speedGainPerTap * taps);
        }

        private float TransitionDuration(SpeedTransitionKind kind)
        {
            return kind switch
            {
                SpeedTransitionKind.TapBeat => _cruiseTapEaseDuration,
                SpeedTransitionKind.ArmRamp => _pierceArmRampDuration,
                _ => 0f,
            };
        }
    }
}

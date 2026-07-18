using System;
using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>One board actor as the solver sees it — enough to reproduce pop/deflect/score rules
    /// without a live <c>IBalloonModel</c>. <see cref="ColorId" /> null/empty means colourless
    /// (mirrors a balloon that does NOT implement <c>IHasColor</c>, e.g. <c>ToughBalloonModel</c>) —
    /// that flag, not <see cref="HitsRemaining" />, is what the simulator uses to choose flat
    /// streak-breaking tough scoring over multiplied green scoring. The dynamic-board fields
    /// (<see cref="SlotIndex" /> onward) are only meaningful when the sim is given a
    /// <see cref="ShotBoardDynamics" /> — the legacy 5-arg constructor zeroes them, which is exactly
    /// what the static (no-dynamics) code path ignores.</summary>
    internal readonly struct ShotBalloonSnapshot
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly string ColorId;
        public readonly int ScoreValue;
        public readonly int HitsRemaining;
        public readonly Vector2Int SlotIndex;
        public readonly int BalancePriority;
        public readonly int MaxBalanceSteps;
        public readonly bool DirectBalanceMotion;
        public readonly IReadOnlyList<NudgeOverride> NudgeOverrides;

        public ShotBalloonSnapshot(Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining)
            : this(position, radius, colorId, scoreValue, hitsRemaining, default, 0, 0, false, null)
        {
        }

        public ShotBalloonSnapshot(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining,
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, bool directBalanceMotion,
            IReadOnlyList<NudgeOverride> nudgeOverrides)
        {
            Position = position;
            Radius = radius;
            ColorId = colorId;
            ScoreValue = scoreValue;
            HitsRemaining = hitsRemaining;
            SlotIndex = slotIndex;
            BalancePriority = balancePriority;
            MaxBalanceSteps = maxBalanceSteps;
            DirectBalanceMotion = directBalanceMotion;
            NudgeOverrides = nudgeOverrides;
        }
    }

    /// <summary>Mutable per-simulation copy of a <see cref="ShotBalloonSnapshot" /> — <see cref="HitsRemaining" />
    /// decrements on a deflect; a popped entry is swap-removed from the caller's active count.
    /// <see cref="Actor" /> is null unless the sim was given a <see cref="ShotBoardDynamics" />, in
    /// which case it is the persistent stub backing this entry's position/nudge state.</summary>
    internal struct ShotBalloonState
    {
        public Vector2 Position;
        public float Radius;
        public string ColorId;
        public int ScoreValue;
        public int HitsRemaining;
        public ShotSimDynamicActor Actor;

        public ShotBalloonState(in ShotBalloonSnapshot snapshot)
        {
            Position = snapshot.Position;
            Radius = snapshot.Radius;
            ColorId = snapshot.ColorId;
            ScoreValue = snapshot.ScoreValue;
            HitsRemaining = snapshot.HitsRemaining;
            Actor = null;
        }
    }

    /// <summary>The cruise knobs, mirroring <c>ProjectileMotionResolver</c>/<c>ProjectileView</c>
    /// exactly (see <see cref="ShotSimulator" />'s cruise handling). Default (all-zero) disables cruise
    /// entirely — <see cref="WallBounceThreshold" /> &lt;= 0 is the same "0 disables" convention
    /// <c>IGameConfiguration.CruiseWallBounceThreshold</c> uses. The per-bounce tap ANIMATION (target
    /// speed scaled by curve(elapsed/duration), the freeze-then-pickup beat) never bends the path, so
    /// the event sim folds it into <see cref="TapLagSeconds" /> — the time an eased startup loses
    /// versus flying the whole segment at the target speed: duration × (1 − mean curve value) —
    /// added to the timeline once per cruise bounce.</summary>
    internal readonly struct ShotCruiseConfig
    {
        private const int CurveAverageSamples = 16;

        public readonly int WallBounceThreshold;
        public readonly float SpeedPerShield;
        public readonly float MaxSpeedMultiplier;
        public readonly float TapLagSeconds;
        public readonly int PiercingTapThreshold;

        public ShotCruiseConfig(int wallBounceThreshold, float speedPerShield,
            float maxSpeedMultiplier = 0f, float tapEaseDuration = 0f,
            AnimationCurve tapCurve = null, int piercingTapThreshold = 0)
        {
            WallBounceThreshold = wallBounceThreshold;
            SpeedPerShield = speedPerShield;
            MaxSpeedMultiplier = maxSpeedMultiplier;
            PiercingTapThreshold = piercingTapThreshold;

            if (tapEaseDuration <= 0f)
            {
                TapLagSeconds = 0f;
                return;
            }

            var curve = tapCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var sum = 0f;
            for (var i = 0; i <= CurveAverageSamples; i++)
            {
                sum += curve.Evaluate(i / (float)CurveAverageSamples);
            }

            TapLagSeconds = tapEaseDuration * (1f - (sum / (CurveAverageSamples + 1)));
        }
    }

    /// <summary>Outcome of one deterministic flight — see <see cref="ShotSimulator.Simulate" />.</summary>
    internal readonly struct ShotSimulationResult
    {
        public readonly int RawScore;
        public readonly int Pops;
        public readonly int ToughsCleared;
        public readonly bool BoardCleared;
        public readonly int Events;
        public readonly bool Died;
        public readonly bool Capped;

        public ShotSimulationResult(
            int rawScore, int pops, int toughsCleared, bool boardCleared, int events, bool died, bool capped)
        {
            RawScore = rawScore;
            Pops = pops;
            ToughsCleared = toughsCleared;
            BoardCleared = boardCleared;
            Events = events;
            Died = died;
            Capped = capped;
        }
    }

    /// <summary>Pure, headless, deterministic billiard simulator for one aim direction (see
    /// @ref plan_shot_geometry). Motion is linear at constant SPEED PER SEGMENT (speed only changes at
    /// events — wall bounces enter/advance cruise, balloon contacts reset it), so flight is simulated
    /// EVENT TO EVENT (next analytic wall crossing, next analytic balloon-corridor entry, or next due
    /// balance pulse), not fixed-step — exact rather than an approximation, and cheap enough for a
    /// sweep of thousands of angles. Reuses <see cref="ProjectileMotionResolver.TryComputeContactNormal" />
    /// for deflect contacts. With <paramref name="dynamics" /> null the loop takes the ORIGINAL static
    /// path unchanged (see <see cref="TryFindNearestBalloonEntry" />) — the fast path task 4b/4c were
    /// required to preserve; with it non-null, balloon centres become time-dependent
    /// (<see cref="ShotBoardDynamics.EvaluateCenter" />) and balance pulses/nudge impulses run for
    /// real.</summary>
    internal static class ShotSimulator
    {
        internal const int DefaultMaxEvents = 500;

        // Below this, a candidate crossing is the event we just resolved, not a new one — otherwise a
        // ray sitting exactly on a wall or circle boundary re-triggers the same event forever.
        private const float EventEpsilon = 1e-4f;

        // Below this, a direction's axis component is treated as parallel to that wall pair (no
        // crossing possible), avoiding a near-zero divide in the analytic wall-time formula.
        private const float AxisEpsilon = 1e-6f;

        // Floor under the current segment speed so a degenerate (zero/negative) config value can never
        // divide time by zero when converting a solved distance into a timestamp.
        private const float MinSpeed = 0.0001f;

        /// <summary>Simulates one aim direction to completion. <paramref name="workingSet" /> is a
        /// caller-owned scratch buffer (sized to at least <paramref name="board" />.Count) reused
        /// across calls — with <paramref name="dynamics" /> null the only per-call cost is copying the
        /// board into it, so a sweep of thousands of angles allocates nothing.
        /// <paramref name="pathOut" />, when non-null, is cleared and filled with the flight's event
        /// positions (origin first) for scene-view drawing; <paramref name="timestampsOut" />, when
        /// non-null, is filled in parallel with each point's absolute simulated time (task 4b's
        /// timeline). Leave both null during a bulk sweep. <paramref name="projectileSpeed" /> and
        /// <paramref name="cruiseConfig" /> drive the timeline even without a dynamic board;
        /// <paramref name="dynamics" />, when supplied, additionally runs flight-rebalance pulses and
        /// nudge impulses against a real <c>SlotGrid</c> (see <see cref="ShotBoardDynamics" />).</summary>
        internal static ShotSimulationResult Simulate(
            IReadOnlyList<ShotBalloonSnapshot> board,
            Vector4 wallLimitsClockwise,
            Vector2 origin,
            Vector2 aimDirection,
            int startingShields,
            float projectileContactRadius,
            ShotBalloonState[] workingSet,
            int maxEvents = DefaultMaxEvents,
            List<Vector2> pathOut = null,
            float projectileSpeed = 1f,
            ShotCruiseConfig cruiseConfig = default,
            ShotBoardDynamics dynamics = null,
            List<float> timestampsOut = null,
            string targetColorId = null,
            float radiusBias = 0f)
        {
            var walls = new WallLimits(wallLimitsClockwise);
            dynamics?.ResetForNewFlight();
            var activeCount = CopyIntoWorkingSet(board, workingSet, dynamics, radiusBias);

            var position = origin;
            var direction = aimDirection.sqrMagnitude > AxisEpsilon ? aimDirection.normalized : Vector2.right;
            var shields = startingShields;
            var elapsed = 0f;
            var consecutiveWallBounces = 0;
            var isCruising = false;
            var isPiercing = false;
            var pierceSpeedScale = 1f;
            var cruiseStartShields = 0;

            string streakColor = null;
            var streakCount = 0;
            string projectileColor = null;

            var rawScore = 0;
            var pops = 0;
            var toughsCleared = 0;
            var events = 0;
            var died = false;
            var capped = false;

            if (pathOut != null)
            {
                pathOut.Clear();
                pathOut.Add(position);
            }

            timestampsOut?.Clear();
            timestampsOut?.Add(0f);

            while (activeCount > 0)
            {
                if (events >= maxEvents)
                {
                    capped = true;
                    break;
                }

                var speed = Mathf.Max(
                    CurrentSpeed(projectileSpeed, isCruising, cruiseStartShields, shields, pierceSpeedScale, cruiseConfig),
                    MinSpeed);

                var hasWallEvent = TryFindWallCrossing(walls, position, direction, out var wallDistance, out var wallNormal);

                bool hasBalloonEvent;
                float balloonDistance;
                int balloonIndex;
                if (dynamics != null)
                {
                    hasBalloonEvent = TryFindNearestBalloonEntryDynamic(
                        workingSet, activeCount, position, direction, speed, elapsed, projectileContactRadius,
                        out balloonDistance, out balloonIndex);
                }
                else
                {
                    hasBalloonEvent = TryFindNearestBalloonEntry(
                        workingSet, activeCount, position, direction, projectileContactRadius,
                        out balloonDistance, out balloonIndex);
                }

                if (!hasWallEvent && !hasBalloonEvent)
                {
                    break;
                }

                var eventIsBalloon = hasBalloonEvent && (!hasWallEvent || balloonDistance < wallDistance);
                var eventDistance = eventIsBalloon ? balloonDistance : wallDistance;

                if (dynamics != null)
                {
                    var candidateEventTime = elapsed + (eventDistance / speed);
                    if (dynamics.TryRunPulseIfDue(candidateEventTime, out var pulseTime))
                    {
                        position += direction * ((pulseTime - elapsed) * speed);
                        elapsed = pulseTime;
                        pathOut?.Add(position);
                        timestampsOut?.Add(elapsed);
                        continue;
                    }
                }

                events++;
                elapsed += eventDistance / speed;

                if (eventIsBalloon)
                {
                    position += direction * balloonDistance;
                    pathOut?.Add(position);
                    timestampsOut?.Add(elapsed);
                    ResolveBalloonContact(
                        workingSet, ref activeCount, balloonIndex, position, projectileContactRadius,
                        ref direction, ref streakColor, ref streakCount, ref projectileColor,
                        ref rawScore, ref pops, ref toughsCleared, ref shields, elapsed, dynamics,
                        ref consecutiveWallBounces, ref isCruising, targetColorId, isPiercing,
                        ref pierceSpeedScale);
                    continue;
                }

                position += direction * wallDistance;
                pathOut?.Add(position);
                timestampsOut?.Add(elapsed);
                shields--;
                if (shields < 0)
                {
                    died = true;
                    break;
                }

                direction = Vector2.Reflect(direction, wallNormal.normalized);
                consecutiveWallBounces++;

                if (isCruising && pierceSpeedScale < 1f)
                {
                    // Only after plowing a tough (scale decayed) does a wall end the run: cruise
                    // ends, speed returns to base, and the earned piercing is consumed — mirrors
                    // ProjectileMotionResolver. An armed shot cruising empty space keeps both.
                    isCruising = false;
                    consecutiveWallBounces = 0;
                    pierceSpeedScale = 1f;
                    isPiercing = false;
                }
                else if (cruiseConfig.WallBounceThreshold > 0 && !isCruising
                    && consecutiveWallBounces >= cruiseConfig.WallBounceThreshold
                    && IsPathClearAhead(
                        walls, position, direction, cruiseConfig.WallBounceThreshold, projectileContactRadius,
                        workingSet, activeCount, elapsed, dynamics))
                {
                    cruiseStartShields = shields;
                    isCruising = true;
                }

                // Every cruise bounce (entry included) replays the tap animation — on the event
                // timeline that's a pure time cost, never a path change.
                if (isCruising)
                {
                    elapsed += cruiseConfig.TapLagSeconds;

                    // Mirrors ProjectileMotionResolver's piercing grant: a long-enough cruise arms
                    // the shot for the rest of its life — contacts end the cruise, never the buff.
                    if (cruiseConfig.PiercingTapThreshold > 0
                        && cruiseStartShields - shields >= cruiseConfig.PiercingTapThreshold)
                    {
                        isPiercing = true;
                    }
                }
            }

            return new ShotSimulationResult(rawScore, pops, toughsCleared, activeCount == 0, events, died, capped);
        }

        // Mirrors ProjectileMotionResolver.Step's cruise ramp exactly: every cruise bounce adds a
        // velocity TAP of SpeedPerShield (cumulative — a 13-shield bank accumulates 13 taps, a
        // 2-shield bank 2). This is the steady-state target; the per-tap animation envelope is
        // folded into ShotCruiseConfig.TapLagSeconds on the timeline instead.
        private static float CurrentSpeed(
            float baseSpeed, bool isCruising, int cruiseStartShields, int shieldsRemaining,
            float pierceSpeedScale, in ShotCruiseConfig cruiseConfig)
        {
            if (!isCruising)
            {
                return baseSpeed;
            }

            var startShields = Mathf.Max(cruiseStartShields, 1);
            var taps = Mathf.Clamp(cruiseStartShields - shieldsRemaining, 0, startShields);
            var target = 1f + cruiseConfig.SpeedPerShield * taps;
            if (cruiseConfig.MaxSpeedMultiplier > 0f)
            {
                target = Mathf.Min(target, cruiseConfig.MaxSpeedMultiplier);
            }

            // Pierce scale bleeds the ramp down through tough plows; floor at base speed.
            var speed = baseSpeed * target * pierceSpeedScale;
            return Mathf.Max(speed, baseSpeed);
        }

        // The event-timeline mirror of the live path-clear check (Shared.PathTrace / the predicate
        // ProjectileView feeds it): traces the wall-reflected ray for `bounces` more crossings, checking
        // each segment (up to its own wall-crossing point only, matching the game's per-segment
        // CircleCast) against every active balloon's CURRENT centre — frozen at tHit, since the live
        // check is one instantaneous physics query, not a projection of future balloon motion.
        // Deliberately NOT routed through PathTrace: it keeps its own determinism-tuned wall crossing
        // (TryFindWallCrossing, shared with the main flight loop) and its analytic occupancy test.
        private static bool IsPathClearAhead(
            in WallLimits walls, Vector2 position, Vector2 direction, int bounces, float projectileContactRadius,
            ShotBalloonState[] workingSet, int activeCount, float tHit, ShotBoardDynamics dynamics)
        {
            for (var i = 0; i < bounces; i++)
            {
                if (!TryFindWallCrossing(walls, position, direction, out var wallDistance, out var wallNormal))
                {
                    return false;
                }

                if (SegmentHitsAnyBalloon(
                        position, direction, wallDistance, projectileContactRadius, workingSet, activeCount, tHit,
                        dynamics))
                {
                    return false;
                }

                position += direction * wallDistance;
                direction = Vector2.Reflect(direction, wallNormal.normalized);
            }

            return true;
        }

        private static bool SegmentHitsAnyBalloon(
            Vector2 position, Vector2 direction, float segmentLength, float projectileContactRadius,
            ShotBalloonState[] workingSet, int activeCount, float tHit, ShotBoardDynamics dynamics)
        {
            for (var i = 0; i < activeCount; i++)
            {
                var center = CurrentBalloonCenter(workingSet, i, tHit, dynamics);
                var combinedRadius = workingSet[i].Radius + projectileContactRadius;
                var toCenter = position - center;

                if (toCenter.sqrMagnitude <= combinedRadius * combinedRadius)
                {
                    return true; // already overlapping at the check instant
                }

                var along = Vector2.Dot(toCenter, direction);
                var discriminant = (along * along) - toCenter.sqrMagnitude + (combinedRadius * combinedRadius);
                if (discriminant < 0f)
                {
                    continue;
                }

                var entryDistance = -along - Mathf.Sqrt(discriminant);
                if (entryDistance >= 0f && entryDistance <= segmentLength)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 CurrentBalloonCenter(
            ShotBalloonState[] workingSet, int index, float t, ShotBoardDynamics dynamics)
        {
            return dynamics != null ? workingSet[index].Actor.EvaluateCenter(t) : workingSet[index].Position;
        }

        // radiusBias fattens/thins every contact circle uniformly — the robustness band's positional-
        // uncertainty proxy (a balloon nudged toward the ray is equivalent to a fatter target).
        private static int CopyIntoWorkingSet(
            IReadOnlyList<ShotBalloonSnapshot> board, ShotBalloonState[] workingSet, ShotBoardDynamics dynamics,
            float radiusBias)
        {
            var count = Mathf.Min(board.Count, workingSet.Length);
            for (var i = 0; i < count; i++)
            {
                workingSet[i] = new ShotBalloonState(board[i]);
                workingSet[i].Radius = Mathf.Max(0f, workingSet[i].Radius + radiusBias);
                if (dynamics != null)
                {
                    workingSet[i].Actor = dynamics.TargetActors[i];
                }
            }

            return count;
        }

        // hitsRemaining > 1 survives as a deflect (mirrors BalloonModelBase.EvaluateNormalHit, damage
        // always 1 for a direct hit); == 1 pops. The pop path scores via the flat/streak-breaking tough
        // rule or the multiplied/colour-adopting green rule (see the class doc), then swap-removes the
        // entry — the ray pierces on, unbent.
        private static void ResolveBalloonContact(
            ShotBalloonState[] workingSet, ref int activeCount, int index, Vector2 contactPosition,
            float projectileContactRadius, ref Vector2 direction,
            ref string streakColor, ref int streakCount, ref string projectileColor,
            ref int rawScore, ref int pops, ref int toughsCleared, ref int shields,
            float tHit, ShotBoardDynamics dynamics, ref int consecutiveWallBounces, ref bool isCruising,
            string targetColorId, bool isPiercing, ref float pierceSpeedScale)
        {
            ref var balloon = ref workingSet[index];

            // Any contact ends an empty-corridor cruise and resets its bounce counter — mirrors
            // ProjectileHitResolver.Resolve — UNLESS the shot has earned piercing, which rides the
            // cruise (and its stacking speed ramp) on through the pop instead of dropping to base.
            if (!isPiercing)
            {
                consecutiveWallBounces = 0;
                isCruising = false;
            }

            var incomingDirection = direction;
            dynamics?.OnBalloonHit(balloon.Actor, tHit);

            var center = dynamics != null ? balloon.Actor.EvaluateCenter(tHit) : balloon.Position;

            // A piercing shot pops EVERYTHING it touches (DamageFlags.Piercing — unbreakables
            // included) and flies on unbent; only unarmed shots deflect off durable actors.
            if (!isPiercing && balloon.HitsRemaining > 1)
            {
                balloon.HitsRemaining--;
                DeflectOffBalloon(center, balloon.Radius + projectileContactRadius, contactPosition, ref direction);
                dynamics?.OnBalloonDeflected(balloon.Actor, incomingDirection, tHit);
                return;
            }

            // Plowing a tough (>1-hit) actor while piercing halves the cruise speed (floored at base
            // in CurrentSpeed) — mirrors ProjectileHitResolver.
            if (isPiercing && balloon.HitsRemaining > 1)
            {
                pierceSpeedScale *= 0.5f;
            }

            // A colour filter scopes SCORE attribution only (milestone masks count one colour's
            // points); streaks, refunds and board effects run unfiltered, exactly as the game would.
            if (string.IsNullOrEmpty(balloon.ColorId))
            {
                var counts = string.IsNullOrEmpty(targetColorId);
                ResolveToughPop(
                    counts ? balloon.ScoreValue : 0, ref streakColor, ref streakCount, ref rawScore,
                    ref toughsCleared);
            }
            else
            {
                var counts = string.IsNullOrEmpty(targetColorId)
                    || string.Equals(balloon.ColorId, targetColorId, StringComparison.Ordinal);
                ResolveGreenPop(
                    balloon.ColorId, counts ? balloon.ScoreValue : 0, ref streakColor, ref streakCount,
                    ref projectileColor, ref rawScore, ref shields);
            }

            pops++;
            dynamics?.RemoveFromGrid(balloon.Actor);
            RemoveActive(workingSet, ref activeCount, index);
        }

        private static void DeflectOffBalloon(
            Vector2 balloonPosition, float combinedRadius, Vector2 contactPosition, ref Vector2 direction)
        {
            if (!ProjectileMotionResolver.TryComputeContactNormal(
                    contactPosition, direction, balloonPosition, combinedRadius, out var normal))
            {
                // Same degenerate fallback as ProjectileMotionResolver.Deflect — a radial normal off
                // the (here, exact) contact point is still a sane reflection.
                normal = (contactPosition - balloonPosition).normalized;
            }

            direction = Vector2.Reflect(direction, normal);
        }

        // Tough pops always reset the streak and score their flat ScoreValue — mirrors
        // ScoreController.RecordStreakMultiplier collapsing ToughBalloonModel's per-point
        // breaksStreak:true attributions to a locked ×1 multiplier regardless of ScoreValue.
        private static void ResolveToughPop(
            int scoreValue, ref string streakColor, ref int streakCount, ref int rawScore, ref int toughsCleared)
        {
            rawScore += scoreValue;
            toughsCleared++;
            streakColor = null;
            streakCount = 0;
        }

        // Mirrors ProjectileHitResolver: colour adoption (ApplyColorChange) off the projectile's OLD
        // colour first, then the streak record (ColorStreakTracker.Record) off the balloon's own
        // colour, then the shield refund (streak >= 2 of the projectile's now-current colour).
        private static void ResolveGreenPop(
            string colorId, int scoreValue, ref string streakColor, ref int streakCount, ref string projectileColor,
            ref int rawScore, ref int shields)
        {
            if (!string.Equals(projectileColor, colorId, StringComparison.Ordinal))
            {
                projectileColor = colorId;
            }

            streakCount = string.Equals(streakColor, colorId, StringComparison.Ordinal) ? streakCount + 1 : 1;
            streakColor = colorId;
            rawScore += scoreValue * streakCount;

            if (streakCount >= 2 && string.Equals(streakColor, projectileColor, StringComparison.Ordinal))
            {
                shields++;
            }
        }

        private static void RemoveActive(ShotBalloonState[] workingSet, ref int activeCount, int index)
        {
            activeCount--;
            workingSet[index] = workingSet[activeCount];
        }

        // Nearest analytic line-circle entry among the active set — same family as
        // TraceHitGeometry.TryFindSurfaceHit / ProjectileMotionResolver.TryComputeContactNormal, solved
        // here for the smallest positive entry distance rather than a backtrack. UNCHANGED from before
        // task 4b/4c — this is the fast, exact path a dynamics-free call always takes, so the no-motion
        // regression case is byte-for-byte the pre-existing code.
        private static bool TryFindNearestBalloonEntry(
            ShotBalloonState[] workingSet, int activeCount, Vector2 position, Vector2 direction,
            float projectileContactRadius, out float bestT, out int bestIndex)
        {
            bestT = float.PositiveInfinity;
            bestIndex = -1;

            for (var i = 0; i < activeCount; i++)
            {
                var combinedRadius = workingSet[i].Radius + projectileContactRadius;
                var toCenter = position - workingSet[i].Position;
                var along = Vector2.Dot(toCenter, direction);
                var discriminant = (along * along) - toCenter.sqrMagnitude + (combinedRadius * combinedRadius);
                if (discriminant < 0f)
                {
                    continue;
                }

                var entryT = -along - Mathf.Sqrt(discriminant);
                if (entryT <= EventEpsilon || entryT >= bestT)
                {
                    continue;
                }

                bestT = entryT;
                bestIndex = i;
            }

            return bestIndex >= 0;
        }

        // The dynamic-board counterpart of TryFindNearestBalloonEntry: each balloon's centre is a
        // function of time (see ShotSimDynamicActor.EvaluateCenter), so its entry is found by the
        // two-pass fixed point in TryFindMovingBalloonEntry rather than the plain static formula.
        private static bool TryFindNearestBalloonEntryDynamic(
            ShotBalloonState[] workingSet, int activeCount, Vector2 position, Vector2 direction, float speed,
            float segmentStartTime, float projectileContactRadius, out float bestT, out int bestIndex)
        {
            bestT = float.PositiveInfinity;
            bestIndex = -1;

            for (var i = 0; i < activeCount; i++)
            {
                var combinedRadius = workingSet[i].Radius + projectileContactRadius;
                if (!TryFindMovingBalloonEntry(
                        workingSet[i].Actor, position, direction, speed, segmentStartTime, combinedRadius,
                        out var entryDistance))
                {
                    continue;
                }

                if (entryDistance <= EventEpsilon || entryDistance >= bestT)
                {
                    continue;
                }

                bestT = entryDistance;
                bestIndex = i;
            }

            return bestIndex >= 0;
        }

        // Two-pass fixed point (@ref plan_shot_geometry §7c): pass 1 freezes the balloon's centre at the
        // segment's start time and uses its balance-only velocity (nudge doesn't contribute a velocity
        // term — its curve isn't linear); pass 2 re-samples the FULL centre (balance + nudge) at the
        // pass-1 candidate hit time and re-solves with the same velocity, correcting most of the
        // curvature error. When neither balance nor nudge is active this is two identical static solves
        // — bit-identical to the non-dynamic path (ShotMotionMath.TrySolveMovingEntry reduces exactly to
        // the static formula when velocity is zero).
        private static bool TryFindMovingBalloonEntry(
            ShotSimDynamicActor actor, Vector2 origin, Vector2 direction, float speed, float segmentStartTime,
            float combinedRadius, out float distance)
        {
            distance = 0f;
            var velocity = actor.EvaluateBalanceVelocity(segmentStartTime);
            var center0 = actor.EvaluateCenter(segmentStartTime);

            if (!ShotMotionMath.TrySolveMovingEntry(origin, direction, speed, center0, velocity, combinedRadius, out var d1))
            {
                return false;
            }

            var t1 = d1 / speed;
            var refinedCenter = actor.EvaluateCenter(segmentStartTime + t1);
            var shiftedCenter = refinedCenter - (velocity * t1);

            if (!ShotMotionMath.TrySolveMovingEntry(origin, direction, speed, shiftedCenter, velocity, combinedRadius, out var d2))
            {
                distance = d1; // pass 2 degenerate (rare) — the pass-1 estimate is still a real contact
                return true;
            }

            distance = d2;
            return true;
        }

        // Analytic per-axis wall time — only walls the ray is heading toward are candidates. A tie
        // within EventEpsilon sums both normals, mirroring WallLimits.Clamp's simultaneous-crossing
        // (exact corner) case.
        private static bool TryFindWallCrossing(
            in WallLimits walls, Vector2 position, Vector2 direction, out float bestT, out Vector2 normal)
        {
            bestT = float.PositiveInfinity;
            normal = Vector2.zero;
            var hasCandidate = false;

            TryAxisCandidate(direction.x, walls.Right - position.x, Vector2.left, ref bestT, ref normal, ref hasCandidate);
            TryAxisCandidate(-direction.x, position.x - walls.Left, Vector2.right, ref bestT, ref normal, ref hasCandidate);
            TryAxisCandidate(direction.y, walls.Top - position.y, Vector2.down, ref bestT, ref normal, ref hasCandidate);
            TryAxisCandidate(-direction.y, position.y - walls.Bottom, Vector2.up, ref bestT, ref normal, ref hasCandidate);

            return hasCandidate;
        }

        private static void TryAxisCandidate(
            float rate, float distance, Vector2 wallNormal, ref float bestT, ref Vector2 normal, ref bool hasCandidate)
        {
            if (rate <= AxisEpsilon)
            {
                return;
            }

            var candidateT = distance / rate;
            if (candidateT <= EventEpsilon)
            {
                return;
            }

            if (!hasCandidate || candidateT < bestT - EventEpsilon)
            {
                bestT = candidateT;
                normal = wallNormal;
                hasCandidate = true;
                return;
            }

            if (candidateT < bestT + EventEpsilon)
            {
                normal += wallNormal;
            }
        }
    }
}

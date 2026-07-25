using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Effects;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>The cruise knobs, mirroring <c>ProjectileMotionResolver</c>/<c>ProjectileView</c>
    /// exactly (see <see cref="ShotSimulator" />'s cruise handling). Default (all-zero) disables cruise
    /// entirely — <see cref="WallBounceThreshold" /> &lt;= 0 is the same "0 disables" convention
    /// <c>IProjectileFlightConfig.CruiseWallBounceThreshold</c> uses. The per-bounce tap ANIMATION (target
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

        public ShotCruiseConfig(IProjectileFlightConfig config)
            : this(
                config.CruiseWallBounceThreshold, config.CruiseSpeedPerShield, config.MaxCruiseSpeedMultiplier,
                config.CruiseTapEaseDuration, config.CruiseTapCurve, config.CruisePiercingTapThreshold)
        {
        }
    }

    /// <summary>The board-global scoring inputs <see cref="ShotSimulator.ResolveBalloonContact" />/
    /// <see cref="ShotSimulator.ResolvePopScore" /> need — built once inside <see cref="ShotSimulator.Simulate" />
    /// from its own parameters and threaded <c>in</c> from there down, replacing the three loose
    /// strings/list that used to thread the same distance (@ref plan_shot_solver_accuracy Phase C §5).</summary>
    internal readonly struct ShotScoreRules
    {
        public readonly string TargetColorId;
        public readonly IReadOnlyList<string> AllowedColors;
        public readonly string RainbowColorId;

        public ShotScoreRules(string targetColorId, IReadOnlyList<string> allowedColors, string rainbowColorId)
        {
            TargetColorId = targetColorId;
            AllowedColors = allowedColors;
            RainbowColorId = rainbowColorId;
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
        public readonly bool Absorbed;

        public ShotSimulationResult(
            int rawScore, int pops, int toughsCleared, bool boardCleared, int events, bool died, bool capped,
            bool absorbed)
        {
            RawScore = rawScore;
            Pops = pops;
            ToughsCleared = toughsCleared;
            BoardCleared = boardCleared;
            Events = events;
            Died = died;
            Capped = capped;
            Absorbed = absorbed;
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

        // Reused across a rainbow-buffed pop's hex-neighbour conversion (@ref
        // plan_shot_solver_accuracy Phase D-core) — the sim runs single-threaded (editor sweeps, the
        // Fire Best cheat), so a shared scratch buffer never aliases across concurrent calls.
        private static readonly Vector2Int[] NeighborBuffer = new Vector2Int[6];

        // Reused across an item activation's EffectHit list (@ref plan_shot_solver_accuracy Phase C1
        // onward) — same single-threaded scratch-buffer convention as NeighborBuffer above.
        private static readonly List<EffectHit> ItemHitsScratch = new();

        // Reused across ApplyEffectHits' handle→slot resolution pass (@ref plan_shot_solver_accuracy
        // Phase C2) — same single-threaded scratch-buffer convention as NeighborBuffer/ItemHitsScratch.
        private static readonly List<Vector2Int> EffectHitSlotsScratch = new();

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
            float radiusBias = 0f,
            IReadOnlyList<string> allowedColors = null,
            string rainbowColorId = null,
            in ShotFlightSeed seed = default,
            ShotItemLayer items = null)
        {
            var walls = new WallLimits(wallLimitsClockwise);
            dynamics?.ResetForNewFlight();
            items?.ResetForNewFlight();
            var activeCount = CopyIntoWorkingSet(board, workingSet, dynamics, radiusBias);

            var direction = aimDirection.sqrMagnitude > AxisEpsilon ? aimDirection.normalized : Vector2.right;
            // No live gather sets a non-default seed yet (buff GRANTS, and a streak already in progress
            // when one lands mid-flight, are Phase C's item layer) — this is the seam that lets
            // D-core's own scoring/end-condition mirrors (including a refund firing through an
            // already-established streak once a buff takes over) be exercised by tests, and later a
            // headless scenario, without a full item layer in place.
            var state = new ShotFlightState(origin, direction, startingShields, in seed);
            var scoreRules = new ShotScoreRules(targetColorId, allowedColors, rainbowColorId);

            if (pathOut != null)
            {
                pathOut.Clear();
                pathOut.Add(state.Position);
            }

            timestampsOut?.Clear();
            timestampsOut?.Add(0f);

            while (activeCount > 0)
            {
                if (state.Events >= maxEvents)
                {
                    state.Capped = true;
                    break;
                }

                var speed = Mathf.Max(CurrentSpeed(projectileSpeed, in state, cruiseConfig), MinSpeed);

                var hasWallEvent = TryFindWallCrossing(
                    walls, state.Position, state.Direction, out var wallDistance, out var wallNormal);

                var hasBalloonEvent = TryFindNearestBalloonEntryAny(
                    workingSet, activeCount, state.Position, state.Direction, speed, state.Elapsed,
                    projectileContactRadius, dynamics, out var balloonDistance, out var balloonIndex);

                if (!hasWallEvent && !hasBalloonEvent)
                {
                    break;
                }

                var eventIsBalloon = hasBalloonEvent && (!hasWallEvent || balloonDistance < wallDistance);
                var eventDistance = eventIsBalloon ? balloonDistance : wallDistance;

                if (TryHandleDuePulse(dynamics, eventDistance, speed, pathOut, timestampsOut, ref state))
                {
                    continue;
                }

                state.Events++;
                state.Elapsed += eventDistance / speed;

                if (eventIsBalloon)
                {
                    state.Position += state.Direction * balloonDistance;
                    pathOut?.Add(state.Position);
                    timestampsOut?.Add(state.Elapsed);
                    ResolveBalloonContact(
                        workingSet, ref activeCount, balloonIndex, state.Position, projectileContactRadius,
                        state.Elapsed, dynamics, in scoreRules, items, ref state);
                    if (state.Absorbed)
                    {
                        break;
                    }

                    continue;
                }

                state.Position += state.Direction * wallDistance;
                pathOut?.Add(state.Position);
                timestampsOut?.Add(state.Elapsed);

                state.Died = HandleWallBounce(
                    wallNormal, walls, state.Position, projectileContactRadius, workingSet, activeCount,
                    cruiseConfig, dynamics, ref state);
                if (state.Died)
                {
                    break;
                }
            }

            return new ShotSimulationResult(
                state.RawScore, state.Pops, state.ToughsCleared, activeCount == 0, state.Events, state.Died,
                state.Capped, state.Absorbed);
        }

        private static bool TryFindNearestBalloonEntryAny(
            ShotBalloonState[] workingSet, int activeCount, Vector2 position, Vector2 direction, float speed,
            float elapsed, float projectileContactRadius, ShotBoardDynamics dynamics,
            out float balloonDistance, out int balloonIndex)
        {
            return dynamics != null
                ? TryFindNearestBalloonEntryDynamic(
                    workingSet, activeCount, position, direction, speed, elapsed, projectileContactRadius,
                    out balloonDistance, out balloonIndex)
                : TryFindNearestBalloonEntry(
                    workingSet, activeCount, position, direction, projectileContactRadius,
                    out balloonDistance, out balloonIndex);
        }

        // A due flight-rebalance pulse pre-empts the next path event: the flight jumps to the pulse's
        // moment in place (no path change) so later events see the post-pulse board.
        private static bool TryHandleDuePulse(
            ShotBoardDynamics dynamics, float eventDistance, float speed, List<Vector2> pathOut,
            List<float> timestampsOut, ref ShotFlightState state)
        {
            if (dynamics == null)
            {
                return false;
            }

            var candidateEventTime = state.Elapsed + (eventDistance / speed);
            if (!dynamics.TryRunPulseIfDue(candidateEventTime, out var pulseTime))
            {
                return false;
            }

            state.Position += state.Direction * ((pulseTime - state.Elapsed) * speed);
            state.Elapsed = pulseTime;
            pathOut?.Add(state.Position);
            timestampsOut?.Add(state.Elapsed);
            return true;
        }

        // Resolves a wall-contact event: consumes a shield (returns true if that kills the run),
        // reflects the flight, and updates cruise state exactly as ProjectileMotionResolver.Step does.
        private static bool HandleWallBounce(
            Vector2 wallNormal, in WallLimits walls, Vector2 position, float projectileContactRadius,
            ShotBalloonState[] workingSet, int activeCount, in ShotCruiseConfig cruiseConfig,
            ShotBoardDynamics dynamics, ref ShotFlightState state)
        {
            state.Shields--;
            // Mirrors WallBounceEndCondition (the Shield-item grant): a wall bounce that spends a
            // shield ends the SHIELD-granted rainbow buff, regardless of whether the shot survives it.
            // RainbowBuffUntilPierceEnd (the Snipe-item grant) is untouched here — it live-ends via
            // PierceEndedEndCondition at the DISCHARGE wall only (Phase C6/E2), never a plain bounce.
            state.RainbowBuffUntilWall = false;
            if (state.Shields < 0)
            {
                return true;
            }

            state.Direction = Vector2.Reflect(state.Direction, wallNormal.normalized);
            state.ConsecutiveWallBounces++;

            if (state.IsCruising && state.PierceSpeedScale < 1f)
            {
                // Only after plowing a tough (scale decayed) does a wall end the run: cruise
                // ends, speed returns to base, and the earned piercing is consumed — mirrors
                // ProjectileMotionResolver. An armed shot cruising empty space keeps both.
                state.IsCruising = false;
                state.ConsecutiveWallBounces = 0;
                state.PierceSpeedScale = 1f;
                state.IsPiercing = false;
                // Mirrors PierceEndedEndCondition firing on IsPiercing going false: BOTH concrete
                // pierce-riding grants end here, not just the multiplier — leaving HasSpeedBuff/
                // RainbowBuffUntilPierceEnd stuck true would wrongly block a LATER Snipe re-grant
                // (ApplyItemOutcome's non-stacking guard) and leave a stale buff flag set forever.
                state.HasSpeedBuff = false;
                state.SpeedBuffMultiplier = 1f;
                state.RainbowBuffUntilPierceEnd = false;
            }
            // Mirrors ProjectileView.TryEnterCruise's own bar (@ref plan_shot_solver_accuracy Phase
            // C6): a shot already piercing without cruising is a Snipe lance and must never enter
            // cruise, which would layer on the per-shield speed tap it deliberately excludes. Without
            // this, a Snipe-armed shot could accumulate bounces (a piercing contact never resets the
            // counter — see the `!state.IsPiercing` guard above it) and wrongly slip into the
            // IsCruising branch above on a later bounce, ending its pierce/buffs early through a
            // proxy live never applies to it. A cruise-earned pierce is always already cruising when
            // granted (below), so this only ever gates the Snipe case.
            else if (cruiseConfig.WallBounceThreshold > 0 && !state.IsCruising && !state.IsPiercing
                && state.ConsecutiveWallBounces >= cruiseConfig.WallBounceThreshold
                && IsPathClearAhead(
                    walls, position, state.Direction, cruiseConfig.WallBounceThreshold, projectileContactRadius,
                    workingSet, activeCount, state.Elapsed, dynamics))
            {
                state.CruiseStartShields = state.Shields;
                state.IsCruising = true;
            }

            // Every cruise bounce (entry included) replays the tap animation — on the event
            // timeline that's a pure time cost, never a path change.
            if (state.IsCruising)
            {
                state.Elapsed += cruiseConfig.TapLagSeconds;

                // Mirrors ProjectileMotionResolver's piercing grant: a long-enough cruise arms
                // the shot for the rest of its life — contacts end the cruise, never the buff.
                if (cruiseConfig.PiercingTapThreshold > 0
                    && state.CruiseStartShields - state.Shields >= cruiseConfig.PiercingTapThreshold)
                {
                    state.IsPiercing = true;
                }
            }

            return false;
        }

        // Mirrors ProjectileMotionResolver.Step's cruise ramp exactly: every cruise bounce adds a
        // velocity TAP of SpeedPerShield (cumulative — a 13-shield bank accumulates 13 taps, a
        // 2-shield bank 2). This is the steady-state target; the per-tap animation envelope is
        // folded into ShotCruiseConfig.TapLagSeconds on the timeline instead.
        private static float CurrentSpeed(float baseSpeed, in ShotFlightState state, in ShotCruiseConfig cruiseConfig)
        {
            // Mirrors ProjectileMotionResolver.ResolveFlightSpeed: the buff multiplies the base speed
            // BEFORE the cruise ramp, and is still the floor when the shot isn't cruising at all —
            // default 1f keeps this byte-identical until Phase C ever grants the buff.
            var buffedBase = baseSpeed * state.SpeedBuffMultiplier;
            if (!state.IsCruising)
            {
                return buffedBase;
            }

            var startShields = Mathf.Max(state.CruiseStartShields, 1);
            var taps = Mathf.Clamp(state.CruiseStartShields - state.Shields, 0, startShields);
            var target = 1f + cruiseConfig.SpeedPerShield * taps;
            if (cruiseConfig.MaxSpeedMultiplier > 0f)
            {
                target = Mathf.Min(target, cruiseConfig.MaxSpeedMultiplier);
            }

            // Pierce scale bleeds the ramp down through tough plows; floor at (buffed) base speed.
            var speed = buffedBase * target * state.PierceSpeedScale;
            return Mathf.Max(speed, buffedBase);
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

        // A null Actor (no BalanceProfile backed this entry — today only reachable from a hand-built
        // static-contact snapshot, never the live gather) has no moving centre to evaluate; it holds
        // its snapshot Position exactly like the no-dynamics path.
        private static Vector2 CurrentBalloonCenter(
            ShotBalloonState[] workingSet, int index, float t, ShotBoardDynamics dynamics)
        {
            var actor = workingSet[index].Actor;
            return dynamics != null && actor != null ? actor.EvaluateCenter(t) : workingSet[index].Position;
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

                // No BalanceProfile ⇒ no dynamic stub was built for this entry (ShotBoardDynamics
                // leaves the matching TargetActors slot null) — Actor stays null, exactly as it already
                // is from the ShotBalloonState constructor above.
                if (dynamics != null && board[i].Balance.HasValue)
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
            float projectileContactRadius, float tHit, ShotBoardDynamics dynamics, in ShotScoreRules scoreRules,
            ShotItemLayer items, ref ShotFlightState state)
        {
            ref var balloon = ref workingSet[index];

            // Absorb ends the flight outright, before anything else runs — no score, no streak
            // mutation, no removal (the live Absorber stays on the grid forever). The main loop's
            // `if (state.Absorbed) break;` stops the rest of the flight from resolving.
            if (balloon.ContactKind == ShotContactKind.Absorb)
            {
                state.Absorbed = true;
                return;
            }

            // Soap washes the projectile colourless on ANY contact (deflect or pop alike) — mirrors
            // ApplyColorChange running ahead of/independent from the deflect-vs-pop branch below
            // (ProjectileHitResolver.cs:193-201).
            if (balloon.WashesProjectileColor && !string.IsNullOrEmpty(state.ProjectileColor))
            {
                state.ProjectileColor = null;
            }

            // Any contact ends an empty-corridor cruise and resets its bounce counter — mirrors
            // ProjectileHitResolver.Resolve — UNLESS the shot has earned piercing, which rides the
            // cruise (and its stacking speed ramp) on through the pop instead of dropping to base.
            if (!state.IsPiercing)
            {
                state.ConsecutiveWallBounces = 0;
                state.IsCruising = false;
            }

            // No Actor ⇒ no BalanceProfile backed this entry (a static contact). Unlike a live balloon,
            // a live static NEVER nudges its neighbours — NudgeService.OnActorHit requires IHasNudge,
            // which no static archetype implements — so this branch deliberately does NOT call
            // ShotBoardDynamics.OnBalloonHitAt for a static. That method stays defined, unreachable
            // today, for a future IHasNudge static.
            var incomingDirection = state.Direction;
            if (balloon.Actor != null)
            {
                dynamics?.OnBalloonHit(balloon.Actor, tHit);
            }

            var center = CurrentBalloonCenter(workingSet, index, tHit, dynamics);

            // A piercing shot pops EVERYTHING it touches (DamageFlags.Piercing — unbreakables
            // included) and flies on unbent; only unarmed shots deflect off durable actors.
            if (!state.IsPiercing && balloon.HitsRemaining > 1)
            {
                balloon.HitsRemaining--;
                DeflectOffBalloon(
                    center, balloon.Radius + projectileContactRadius, contactPosition, ref state.Direction);

                // Deflected statics do NOT self-shove — they are immovable and a live static never
                // wobbles, unlike a deflected balloon (OnBalloonDeflected).
                if (balloon.Actor != null)
                {
                    dynamics?.OnBalloonDeflected(balloon.Actor, incomingDirection, tHit);
                }

                return;
            }

            // Plowing a tough (>1-hit) actor while piercing halves the cruise speed (floored at base
            // in CurrentSpeed) — mirrors ProjectileHitResolver.
            if (state.IsPiercing && balloon.HitsRemaining > 1)
            {
                state.PierceSpeedScale *= 0.5f;
            }

            // A Gatekeeper's final hit pops it — but it has no IHasScoreColor, so ScoreController
            // ignores its live pop entirely: no score, no ToughsCleared, and (unlike a tough pop) the
            // streak must NOT reset, since the game never touches it either. Discriminate on IsStatic,
            // not Actor == null (the fast/no-dynamics path nulls Actor for every entry) or ContactKind
            // (Absorb is orthogonal — already handled above).
            if (balloon.IsStatic)
            {
                state.Pops++;
                dynamics?.RemoveFromGridAt(balloon.SlotIndex);
                RemoveActive(workingSet, ref activeCount, index);
                return;
            }

            // A colour filter scopes SCORE attribution only (milestone masks count one colour's
            // points); streaks, refunds and board effects run unfiltered, exactly as the game would.
            // Built from the CURRENT (pre-adoption) projectile colour — ResolvePopScore's own adoption
            // step mutates state.ProjectileColor, so the cause must snapshot it first (@ref
            // plan_shot_solver_accuracy Phase C §1.5).
            var cause = ShotPopCause.ProjectileContact(
                state.HasRainbowBuff, state.IsPiercing,
                isRainbowTargetDeferred: balloon.IsRainbow && !state.HasRainbowBuff
                    && string.IsNullOrEmpty(state.ProjectileColor),
                projectileColor: state.ProjectileColor);
            ResolvePopScore(balloon, in scoreRules, in cause, ref state);

            // A rainbow-buffed shot converts nearby paintable balloons on every pop it lands, not
            // just a rainbow-target one — mirrors ProjectileHitResolver.ConvertNeighborsToRainbow,
            // run over the ACTIVE WORKING SET (SlotIndex-addressed) so it works with dynamics: null.
            if (state.HasRainbowBuff)
            {
                ConvertNeighborsToRainbow(workingSet, activeCount, balloon.SlotIndex, scoreRules.RainbowColorId);
            }

            state.Pops++;

            // Copy the host BY VALUE before RemoveActive's swap-remove reassigns whatever `balloon`
            // refs into (R6 ref-aliasing, @ref plan_shot_solver_accuracy Phase C1) — RunItemEffects
            // needs the host's own item/colour/slot identity, which the post-removal `balloon` ref no
            // longer holds.
            var host = balloon;

            if (balloon.Actor != null)
            {
                dynamics?.RemoveFromGrid(balloon.Actor);
            }
            else
            {
                dynamics?.RemoveFromGridAt(balloon.SlotIndex);
            }

            RemoveActive(workingSet, ref activeCount, index);

            // Statics/the gatekeeper path never reach here — ItemProfile only ever rides the colour/
            // rainbow snapshot factories (see ShotBalloonSnapshot), never ForStaticContact.
            if (items != null && host.Item != ItemType.None)
            {
                RunItemEffects(
                    items, in host, center, state.Direction, tHit, workingSet, ref activeCount, dynamics,
                    in scoreRules, ref state);
            }
        }

        // Pop-site item hook (@ref plan_shot_solver_accuracy Phase C1): the host's own item is the
        // FIRST activation into the FIFO queue — draining it breadth-first mirrors ItemActivator's
        // per-frame cadence (a popped item's own effect enqueuing another item resolves on a LATER
        // iteration here, not nested recursion, exactly like the live frame cadence).
        private static void RunItemEffects(
            ShotItemLayer items, in ShotBalloonState host, Vector2 hostCenter, Vector2 hostDirection, float tHit,
            ShotBalloonState[] workingSet, ref int activeCount, ShotBoardDynamics dynamics,
            in ShotScoreRules scoreRules, ref ShotFlightState state)
        {
            var activation = new ShotItemActivation(
                host.Item, hostCenter, hostDirection, host.SlotIndex, host.ColorId, host.IsRainbow,
                host.ItemSpinDegrees + (host.ItemSpinRate * tHit), isDirectHit: true);

            if (!items.TryBeginActivation(in activation))
            {
                return;
            }

            while (items.TryDequeue(out var next))
            {
                var outcome = items.Resolve(next, state.ProjectileColor, workingSet, activeCount, ItemHitsScratch);
                ApplyItemOutcome(in outcome, ref state);
                ApplyEffectHits(
                    ItemHitsScratch, items, in next, in outcome, workingSet, ref activeCount, dynamics, tHit,
                    in scoreRules, ref state);
            }
        }

        // Only Shield (C1, ShieldDelta/GrantsRainbowBuffUntilWall) and Snipe (C6, the remaining three
        // fields) ever populate a non-default ShotItemOutcome — Bomb/Laser/Lightning/Paint always
        // return default. Wiring every field's apply-side plumbing now, while it's free, means C6
        // only has to fill in ShotItemLayer.Resolve's own Snipe case.
        private static void ApplyItemOutcome(in ShotItemOutcome outcome, ref ShotFlightState state)
        {
            state.Shields += outcome.ShieldDelta;
            state.RainbowBuffUntilWall |= outcome.GrantsRainbowBuffUntilWall;
            state.RainbowBuffUntilPierceEnd |= outcome.GrantsRainbowBuffUntilPierceEnd;
            state.IsPiercing |= outcome.ArmsPierce;

            // 0 means "grants no speed buff" (1f can't express that — it's also the buffless
            // multiplier); the guard doubles as Phase C6's non-stacking re-pickup rule for free.
            if (outcome.SpeedBuffMultiplier > 0f && !state.HasSpeedBuff)
            {
                state.HasSpeedBuff = true;
                state.SpeedBuffMultiplier = outcome.SpeedBuffMultiplier;
            }
        }

        // Per-kind dispatch (@ref plan_shot_solver_accuracy Phase C2 "EffectHit application"):
        // PiercingDamage always pops (cause flags = Piercing ONLY — live REPLACES flags, never ORs
        // them); Damage pops when the activation's own configured flags carry Piercing, else decrements
        // HitsRemaining by the activation's own Damage and pops at/below zero — NEVER deflects/redirects,
        // unlike a projectile contact. Recolor never pops and never nudges. Every hit's target slot is
        // resolved from the effect board's frozen bind-time occupant snapshot BEFORE any hit below can
        // swap-remove a popped entry and scramble workingSet's own array indices — the board itself
        // never mutates, only workingSet does, so this one-shot resolution pass is always safe.
        private static void ApplyEffectHits(
            List<EffectHit> hits, ShotItemLayer items, in ShotItemActivation activation, in ShotItemOutcome outcome,
            ShotBalloonState[] workingSet, ref int activeCount, ShotBoardDynamics dynamics, float tHit,
            in ShotScoreRules scoreRules, ref ShotFlightState state)
        {
            if (hits == null || hits.Count == 0)
            {
                return;
            }

            EffectHitSlotsScratch.Clear();
            for (var i = 0; i < hits.Count; i++)
            {
                EffectHitSlotsScratch.Add(items.SlotOf(hits[i].Handle));
            }

            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                var index = FindActiveIndex(workingSet, activeCount, EffectHitSlotsScratch[i]);
                if (index < 0)
                {
                    continue; // already popped by an earlier hit in this same batch
                }

                if (hit.Kind == EffectHitKind.Recolor)
                {
                    ApplyRecolor(ref workingSet[index], hit.ColorId, scoreRules.RainbowColorId);
                    continue;
                }

                // NudgeService has no outcome filter — a surviving Damage hit nudges its neighbours
                // exactly like a pop does.
                NudgeItemHit(dynamics, workingSet[index], tHit);

                var flags = hit.Kind == EffectHitKind.PiercingDamage ? DamageFlags.Piercing : outcome.Flags;
                var pops = hit.Kind == EffectHitKind.PiercingDamage || flags.HasFlag(DamageFlags.Piercing);
                if (!pops)
                {
                    workingSet[index].HitsRemaining -= outcome.Damage;
                    pops = workingSet[index].HitsRemaining <= 0;
                }

                if (pops)
                {
                    PopItemHit(
                        items, workingSet, ref activeCount, index, tHit, dynamics, in scoreRules,
                        activation.SourceColorId, flags, ref state);
                }
            }
        }

        private static int FindActiveIndex(ShotBalloonState[] workingSet, int activeCount, Vector2Int slot)
        {
            for (var i = 0; i < activeCount; i++)
            {
                if (workingSet[i].SlotIndex == slot)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void NudgeItemHit(ShotBoardDynamics dynamics, in ShotBalloonState balloon, float tHit)
        {
            if (balloon.Actor != null)
            {
                dynamics?.OnBalloonHit(balloon.Actor, tHit);
            }
            else
            {
                dynamics?.OnBalloonHitAt(balloon.SlotIndex, tHit);
            }
        }

        // An item-effect pop: scores via its own cause (ShotPopCause.ItemEffect — never adopts the
        // projectile's colour, never refunds a shield), then removes exactly like a projectile-contact
        // pop, chaining the popped carrier's own item (if any) onto the SAME FIFO queue — a bomb-popped
        // balloon carrying another Bomb resolves on a LATER iteration of RunItemEffects' drain, not
        // nested recursion (mirrors the live frame cadence). Item pops never touch
        // ConvertNeighborsToRainbow/IsCruising/ConsecutiveWallBounces — those are projectile-contact-only.
        private static void PopItemHit(
            ShotItemLayer items, ShotBalloonState[] workingSet, ref int activeCount, int index, float tHit,
            ShotBoardDynamics dynamics, in ShotScoreRules scoreRules, string sourceColorId, DamageFlags flags,
            ref ShotFlightState state)
        {
            var cause = ShotPopCause.ItemEffect(sourceColorId, flags);
            ResolvePopScore(workingSet[index], in scoreRules, in cause, ref state);

            state.Pops++;

            // Copy BY VALUE before RemoveActive's swap-remove reassigns this index (R6 ref-aliasing) —
            // the chained activation below needs the popped balloon's OWN item/colour/slot identity.
            var popped = workingSet[index];

            if (popped.Actor != null)
            {
                dynamics?.RemoveFromGrid(popped.Actor);
            }
            else
            {
                dynamics?.RemoveFromGridAt(popped.SlotIndex);
            }

            RemoveActive(workingSet, ref activeCount, index);

            if (items != null && popped.Item != ItemType.None)
            {
                var chained = new ShotItemActivation(
                    popped.Item, popped.Position, Vector2.zero, popped.SlotIndex, popped.ColorId, popped.IsRainbow,
                    popped.ItemSpinDegrees + (popped.ItemSpinRate * tHit), isDirectHit: false);
                items.TryBeginActivation(in chained);
            }
        }

        // Shared by ApplyEffectHits' Recolor case and ConvertNeighborsToRainbow's rainbow-buff
        // conversion — a plain assignment mirrors ApplyColorChange writing IHasColor.Color.Value
        // directly: setting a balloon's colour to the reserved rainbow marker makes it rainbow; painting
        // a rainbow balloon back to an ordinary colour (Paint's green-on-rainbow case, Phase C5) clears
        // IsRainbow just as naturally, with no separate branch needed.
        private static void ApplyRecolor(ref ShotBalloonState balloon, string colorId, string rainbowColorId)
        {
            balloon.ColorId = colorId;
            balloon.IsRainbow = !string.IsNullOrEmpty(rainbowColorId)
                && string.Equals(colorId, rainbowColorId, StringComparison.Ordinal);
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
        // Only reached when NOT rainbow-buffed (see ResolvePopScore) — under a buff the WildcardStreak
        // flag pre-empts this reset entirely, live-side too (RecordStreakMultiplier checks flags
        // before it ever looks at an attribution's own BreaksStreak).
        private static void ResolveToughPop(int scoreValue, ref ShotFlightState state)
        {
            state.RawScore += scoreValue;
            state.ToughsCleared++;
            state.StreakColor = null;
            state.StreakCount = 0;
            state.DeferredPops = 0;
        }

        // Dispatches a pop's colour adoption + streak + payout, mirroring ScoreController.
        // RecordStreakMultiplier's precedence (ProjectileHitResolver.cs:124-152) and BalloonModel.
        // ResolveRainbowAttribution's payout (pre-cap product only — never ILevelProgress.ClaimProgress's
        // cap, which the sim doesn't model), re-keyed on cause.Flags (@ref plan_shot_solver_accuracy
        // Phase C §1.5's ShotPopCause seam) instead of reading state.HasRainbowBuff/IsRainbow-deferred
        // conditions directly — the same dispatch now serves both a projectile contact AND an item
        // effect's own AOE pop. Guard, then two orthogonal concerns gated on cause.IsProjectileContact,
        // then the streak-multiplier dispatch (in live precedence order):
        // - Guard: a rainbow with no board-allowed colours pays/records nothing (mirrors
        //   ResolveRainbowAttribution's own early return — ScoreController's group-publish exits
        //   before RecordStreakMultiplier ever runs).
        // - Adoption (ApplyColorChange) only ever runs for a projectile's own DIRECT contact — an item's
        //   AOE dispatch never touches projectile.ColorName live, so a Bomb/Laser/Lightning pop must
        //   never steal the shot's travelling colour.
        // - Dispatch: (1) WildcardStreak scores colour-agnostically — even an otherwise streak-breaking
        //   tough/Unbreakable pop just keeps the multiplier climbing; (2) DeferredStreak banks a
        //   colourless-projectile rainbow pop; (3) absent both flags, a colourless target that doesn't
        //   PaysSourceColor (an ordinary Tough) takes the flat/streak-breaking rule and RETURNS — never
        //   reaching the shared payout/refund step below, matching live's IHasColor-gated refund;
        //   (4) a rainbow target anchors on cause.SourceColorId if still allowed, else the balloon's
        //   first allowed one (mirrors ResolveRainbowAttribution reading context.SourceColorId, not the
        //   projectile's own live colour field, as its primary candidate); (5) a source-colour-paying
        //   colourless target (Unbreakable, Phase C2a) records cause.SourceColorId instead of its own
        //   (empty) ColorId; (6) an ordinary coloured pop just records its own colour.
        // The shield refund is hoisted out of every surviving branch (never the tough-rule return),
        // gated on cause.IsProjectileContact same as adoption: ColorId non-empty && streak >= 2 of the
        // projectile's now-current colour — an Unbreakable's empty ColorId already excludes it from ever
        // refunding on its own pop, live-faithfully (balloon is IHasColor gates the live refund, and
        // UnbreakableBalloonModel implements neither IHasColor nor IPaintable).
        private static void ResolvePopScore(
            in ShotBalloonState balloon, in ShotScoreRules scoreRules, in ShotPopCause cause, ref ShotFlightState state)
        {
            var allowedColors = scoreRules.AllowedColors;
            var targetColorId = scoreRules.TargetColorId;

            if (balloon.IsRainbow && (allowedColors == null || allowedColors.Count == 0))
            {
                return;
            }

            if (cause.IsProjectileContact && !balloon.IsRainbow && !string.IsNullOrEmpty(balloon.ColorId)
                && !string.Equals(state.ProjectileColor, balloon.ColorId, StringComparison.Ordinal))
            {
                state.ProjectileColor = balloon.ColorId;
            }

            int multiplier;
            string attributedColorId;
            if (cause.Flags.HasFlag(DamageFlags.WildcardStreak))
            {
                state.DeferredPops = 0;
                multiplier = ++state.StreakCount;
                attributedColorId = balloon.PaysSourceColor ? cause.SourceColorId : balloon.ColorId;
            }
            else if (cause.Flags.HasFlag(DamageFlags.DeferredStreak))
            {
                state.DeferredPops++;
                multiplier = 1;
                attributedColorId = balloon.ColorId;
            }
            else if (string.IsNullOrEmpty(balloon.ColorId) && !balloon.PaysSourceColor)
            {
                var countsTough = string.IsNullOrEmpty(targetColorId);
                ResolveToughPop(countsTough ? balloon.ScoreValue : 0, ref state);
                return;
            }
            else if (balloon.IsRainbow)
            {
                var primary = ContainsColor(allowedColors, cause.SourceColorId)
                    ? cause.SourceColorId
                    : allowedColors[0];
                multiplier = RecordColor(primary, ref state);
                attributedColorId = primary;
            }
            else if (balloon.PaysSourceColor)
            {
                // Still a colourless (Tough-shaped) target for the ToughsCleared tally even though its
                // PAYOUT rides the colour-record path instead of ResolveToughPop's flat rule (@ref
                // plan_shot_solver_accuracy Phase C2a) — the two are orthogonal: one counts "a tough
                // popped", the other decides how the streak reacts.
                state.ToughsCleared++;
                multiplier = RecordColor(cause.SourceColorId, ref state);
                attributedColorId = cause.SourceColorId;
            }
            else
            {
                multiplier = RecordColor(balloon.ColorId, ref state);
                attributedColorId = balloon.ColorId;
            }

            // A rainbow target pays every allowed colour at full ScoreValue — a target-colour filter
            // only narrows which score GROUP counts toward the milestone, never which colours a
            // rainbow pays (BalloonModel.ResolveRainbowAttribution isn't filter-aware at all).
            var payColors = balloon.IsRainbow
                ? (string.IsNullOrEmpty(targetColorId) ? allowedColors.Count : 1)
                : 1;
            var counts = balloon.IsRainbow
                || string.IsNullOrEmpty(targetColorId)
                || string.Equals(attributedColorId, targetColorId, StringComparison.Ordinal);
            state.RawScore += (counts ? balloon.ScoreValue * payColors : 0) * multiplier;

            if (cause.IsProjectileContact && !string.IsNullOrEmpty(balloon.ColorId) && state.StreakCount >= 2
                && string.Equals(state.StreakColor, state.ProjectileColor, StringComparison.Ordinal))
            {
                state.Shields++;
            }
        }

        // Mirrors ColorStreakTracker.Record's non-breaking branch, including the deferred-pop fold:
        // repeating the same colour just increments, a new colour starts at 1 plus any banked
        // deferred rainbow pops — either way the deferred bank clears.
        private static int RecordColor(string colorId, ref ShotFlightState state)
        {
            state.StreakCount = string.Equals(state.StreakColor, colorId, StringComparison.Ordinal)
                ? state.StreakCount + 1
                : 1 + state.DeferredPops;
            state.StreakColor = colorId;
            state.DeferredPops = 0;
            return state.StreakCount;
        }

        private static bool ContainsColor(IReadOnlyList<string> colors, string colorId)
        {
            if (colors == null || string.IsNullOrEmpty(colorId))
            {
                return false;
            }

            for (var i = 0; i < colors.Count; i++)
            {
                if (string.Equals(colors[i], colorId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // Mirrors ProjectileHitResolver.ConvertNeighborsToRainbow — a rainbow-buffed pop spreads onto
        // its hex neighbours, but over the ACTIVE WORKING SET (SlotIndex-addressed linear scan, not
        // the dynamics grid), so it works with dynamics: null too. Only a poppable, still-coloured,
        // non-rainbow, non-static target converts — IPaintable is "poppable, coloured" today.
        private static void ConvertNeighborsToRainbow(
            ShotBalloonState[] workingSet, int activeCount, Vector2Int slot, string rainbowColorId)
        {
            HexCoordinates.HexNeighborIndices(slot.x, slot.y, NeighborBuffer);

            for (var n = 0; n < 6; n++)
            {
                var neighbor = NeighborBuffer[n];
                for (var i = 0; i < activeCount; i++)
                {
                    if (workingSet[i].SlotIndex != neighbor)
                    {
                        continue;
                    }

                    if (!workingSet[i].IsStatic && !workingSet[i].IsRainbow
                        && !string.IsNullOrEmpty(workingSet[i].ColorId))
                    {
                        ApplyRecolor(ref workingSet[i], rainbowColorId, rainbowColorId);
                    }

                    break;
                }
            }
        }

        private static void RemoveActive(ShotBalloonState[] workingSet, ref int activeCount, int index)
        {
            activeCount--;
            workingSet[index] = workingSet[activeCount];
        }

        // Nearest analytic line-circle entry among the active set — same family as
        // TraceHitGeometry.TryFindSurfaceHit / ProjectileMotionResolver.TryComputeContactNormal, solved
        // here for the smallest positive entry distance rather than a backtrack. This is the fast,
        // exact path a dynamics-free call always takes; the per-circle solve lives in
        // TryFindStaticBalloonEntry (shared with the dynamic path's null-Actor fallback) and computes
        // the identical result the pre-task-4b/4c inline formula did.
        private static bool TryFindNearestBalloonEntry(
            ShotBalloonState[] workingSet, int activeCount, Vector2 position, Vector2 direction,
            float projectileContactRadius, out float bestT, out int bestIndex)
        {
            bestT = float.PositiveInfinity;
            bestIndex = -1;

            for (var i = 0; i < activeCount; i++)
            {
                var combinedRadius = workingSet[i].Radius + projectileContactRadius;
                if (!TryFindStaticBalloonEntry(workingSet[i].Position, position, direction, combinedRadius, out var entryT))
                {
                    continue;
                }

                if (entryT <= EventEpsilon || entryT >= bestT)
                {
                    continue;
                }

                bestT = entryT;
                bestIndex = i;
            }

            return bestIndex >= 0;
        }

        // The plain line-circle solve shared by the static path above and, per-entry, by the dynamic
        // path below for any working-set slot with a null Actor (no BalanceProfile ⇒ a fixed centre —
        // see the null-Actor gating in CopyIntoWorkingSet/CurrentBalloonCenter).
        private static bool TryFindStaticBalloonEntry(
            Vector2 center, Vector2 position, Vector2 direction, float combinedRadius, out float entryT)
        {
            var toCenter = position - center;
            var along = Vector2.Dot(toCenter, direction);
            var discriminant = (along * along) - toCenter.sqrMagnitude + (combinedRadius * combinedRadius);
            if (discriminant < 0f)
            {
                entryT = 0f;
                return false;
            }

            entryT = -along - Mathf.Sqrt(discriminant);
            return true;
        }

        // The dynamic-board counterpart of TryFindNearestBalloonEntry: each balloon's centre is a
        // function of time (see ShotSimDynamicActor.EvaluateCenter), so its entry is found by the
        // two-pass fixed point in TryFindMovingBalloonEntry rather than the plain static formula — UNLESS
        // the entry's Actor is null (a static contact with no BalanceProfile), which falls back to the
        // same fixed-centre solve the no-dynamics path uses.
        private static bool TryFindNearestBalloonEntryDynamic(
            ShotBalloonState[] workingSet, int activeCount, Vector2 position, Vector2 direction, float speed,
            float segmentStartTime, float projectileContactRadius, out float bestT, out int bestIndex)
        {
            bestT = float.PositiveInfinity;
            bestIndex = -1;

            for (var i = 0; i < activeCount; i++)
            {
                var combinedRadius = workingSet[i].Radius + projectileContactRadius;
                var actor = workingSet[i].Actor;
                float entryDistance;
                var found = actor != null
                    ? TryFindMovingBalloonEntry(
                        actor, position, direction, speed, segmentStartTime, combinedRadius, out entryDistance)
                    : TryFindStaticBalloonEntry(workingSet[i].Position, position, direction, combinedRadius, out entryDistance);

                if (!found)
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

using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Effects;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
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
        public readonly float SpeedGainPerTap;
        public readonly float MaxSpeedMultiplier;
        public readonly float TapLagSeconds;
        public readonly int PiercingTapThreshold;
        public readonly bool SweepEnabled;
        public readonly int SweepTapThreshold;

        // The arming bounce's own lag, in the same "duration not spent at full speed" form as
        // TapLagSeconds. Live replaces the per-tap beat there with one ramp from the arming speed into the
        // frozen top speed, and the ramp only loses time in proportion to the gap between those two —
        // HandleWallBounce scales this by that gap.
        public readonly float ArmRampLagSeconds;

        public ShotCruiseConfig(int wallBounceThreshold, float speedGainPerTap,
            float maxSpeedMultiplier = 0f, float tapEaseDuration = 0f,
            AnimationCurve tapCurve = null, int piercingTapThreshold = 0,
            float armRampDuration = 0f, AnimationCurve armRampCurve = null, bool sweepEnabled = false,
            int sweepTapThreshold = 0)
        {
            WallBounceThreshold = wallBounceThreshold;
            SpeedGainPerTap = speedGainPerTap;
            MaxSpeedMultiplier = maxSpeedMultiplier;
            PiercingTapThreshold = piercingTapThreshold;
            SweepEnabled = sweepEnabled;
            SweepTapThreshold = sweepTapThreshold;
            TapLagSeconds = EnvelopeLagSeconds(tapEaseDuration, tapCurve);
            ArmRampLagSeconds = EnvelopeLagSeconds(armRampDuration, armRampCurve);
        }

        public ShotCruiseConfig(IProjectileFlightConfig config)
            : this(
                config.CruiseWallBounceThreshold, config.SpeedGainPerTap, config.MaxSpeedMultiplier,
                config.CruiseTapEaseDuration, config.CruiseTapCurve, config.CruisePiercingTapThreshold,
                config.PierceArmRampDuration, config.PierceArmRampCurve, config.SweepEnabled,
                config.SweepTapThreshold)
        {
        }

        // Seconds of an eased envelope NOT spent at its target value — the curve's mean sampled over the
        // duration. On the event timeline that is a pure time cost, never a path change.
        private static float EnvelopeLagSeconds(float duration, AnimationCurve curve)
        {
            if (duration <= 0f)
            {
                return 0f;
            }

            var resolved = curve is { length: > 0 } ? curve : AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var sum = 0f;
            for (var i = 0; i <= CurveAverageSamples; i++)
            {
                sum += resolved.Evaluate(i / (float)CurveAverageSamples);
            }

            return duration * (1f - (sum / (CurveAverageSamples + 1)));
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

        /// <summary>
        ///     Mirrors <c>IRunConfig.StreakGrantsShields</c>. Off, a continued colour streak stops
        ///     paying a shield — and a simulated shot that still collected them would credit itself
        ///     wall bounces the real one cannot afford, so its "best" shots die early in play.
        /// </summary>
        public readonly bool StreakGrantsShields;

        /// <summary>The hit a ripe cluster's ripple deals each balloon it reaches (<c>IRipeBubbleSettings.RippleDamage</c>).</summary>
        public readonly int RippleDamage;

        /// <summary>How far that ripple reaches between slot centres, in world units
        /// (<c>IRipeBubbleSettings.RippleRadius</c>).</summary>
        public readonly float RippleRadius;

        public ShotScoreRules(
            string targetColorId, IReadOnlyList<string> allowedColors, string rainbowColorId,
            bool streakGrantsShields, int rippleDamage, float rippleRadius)
        {
            TargetColorId = targetColorId;
            AllowedColors = allowedColors;
            RainbowColorId = rainbowColorId;
            StreakGrantsShields = streakGrantsShields;
            RippleDamage = rippleDamage;
            RippleRadius = rippleRadius;
        }
    }

    /// <summary>Outcome of one deterministic flight — see <see cref="ShotSimulator.Simulate" />.</summary>
    internal readonly struct ShotSimulationResult
    {
        public readonly int RawScore;
        public readonly int Pops;

        /// <summary>Reward balloons popped, which pay no score: a ranking that wants them reads these.</summary>
        public readonly int GoldenPops;

        public readonly int HeartPops;
        public readonly int DiscoPops;
        public readonly int NukePops;
        public readonly int ToughsCleared;
        public readonly bool BoardCleared;
        public readonly int Events;
        public readonly bool Died;
        public readonly bool Capped;
        public readonly bool Absorbed;

        /// <summary>The colour the shot carries when the flight ends: null once soap or a coat has washed it.</summary>
        public readonly string FinalColor;

        /// <summary>The shot's colour streak when the flight ends: 0 once a cluster or a tough has broken it.</summary>
        public readonly int FinalStreak;

        /// <summary>Whether the shot is piercing when the flight ends: a lance however it came by it (loaded as
        /// one, a Snipe pickup, its own cruise, a chain of mushrooms).</summary>
        public readonly bool FinalPiercing;

        /// <summary>What the flight's mushrooms did: all zero on a board without them.</summary>
        public readonly ShotMushroomTally Mushrooms;

        public ShotSimulationResult(
            int rawScore, int pops, int toughsCleared, bool boardCleared, int events, bool died, bool capped,
            bool absorbed, int goldenPops, int heartPops, int discoPops, int nukePops = 0, string finalColor = null,
            int finalStreak = 0, bool finalPiercing = false, in ShotMushroomTally mushrooms = default)
        {
            RawScore = rawScore;
            Pops = pops;
            GoldenPops = goldenPops;
            HeartPops = heartPops;
            DiscoPops = discoPops;
            NukePops = nukePops;
            ToughsCleared = toughsCleared;
            BoardCleared = boardCleared;
            Events = events;
            Died = died;
            Capped = capped;
            Absorbed = absorbed;
            FinalColor = finalColor;
            FinalStreak = finalStreak;
            FinalPiercing = finalPiercing;
            Mushrooms = mushrooms;
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
    /// real. With <c>bumpers</c> (the meadow's mushrooms, <see cref="ShotBumperLayer" />) a fourth kind of event
    /// joins the three: the ball's entry into a mushroom's circle, found by the live flight's own swept test
    /// (<see cref="BumperSweep" />) and resolved by its own rules in its own order: a bounce for a ball that is
    /// not piercing (see <see cref="BounceOffBumper" />), a cut cap and no turn for one that is (see
    /// <see cref="TryCutCap" />). With none, no line of that runs.</summary>
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

        // Same single-threaded scratch-buffer convention as NeighborBuffer above, for
        // ConvertSideNeighboursToRainbow — this simulator is single-threaded per sweep sample, so a
        // static scratch buffer is safe and avoids a per-call allocation on a path ShotBoardGather's
        // sweep can invoke hundreds of times.
        private static readonly Vector2[] SideNeighborOffsetsScratch = new Vector2[6];
        private static readonly int[] SideNeighborWorkingSetIndicesScratch = new int[6];

        // Reused across an item activation's EffectHit list (@ref plan_shot_solver_accuracy Phase C1
        // onward) — same single-threaded scratch-buffer convention as NeighborBuffer above.
        private static readonly List<EffectHit> ItemHitsScratch = new();

        // Reused across ApplyEffectHits' handle→slot resolution pass (@ref plan_shot_solver_accuracy
        // Phase C2) — same single-threaded scratch-buffer convention as NeighborBuffer/ItemHitsScratch.
        private static readonly List<Vector2Int> EffectHitSlotsScratch = new();

        // The ripe clusters a shot has burst whose ripple has not run yet, by the centre of the slot each burst in,
        // and the slots the one running reaches. Same single-threaded scratch convention as NeighborBuffer: a
        // ripple's own pops only add to the queue, never re-enter it, so a chain runs in one index loop.
        private static readonly List<Vector2> RippleQueueScratch = new();
        private static readonly List<Vector2Int> RippleSlotsScratch = new();

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
        /// nudge impulses against a real <c>SlotGrid</c> (see <see cref="ShotBoardDynamics" />).
        /// <paramref name="rippleDamage" /> is the hit a ripe cluster's ripple deals each balloon it reaches
        /// (<c>IRipeBubbleSettings.RippleDamage</c>); it has no default, so a caller cannot forget it.
        /// <paramref name="rippleRadius" /> is how far that ripple reaches between slot centres
        /// (<c>IRipeBubbleSettings.RippleRadius</c>, each target's <see cref="ShotBalloonSnapshot.SlotCenter" />);
        /// left at 0, a ripe cluster's burst reaches nothing, so every flight over a board with one must pass it
        /// (<see cref="ShotBoardGather.SimulateAt" /> does, from its context).
        /// <paramref name="bumpers" /> is the board's mushrooms, or null for a board without them.</summary>
        internal static ShotSimulationResult Simulate(
            IReadOnlyList<ShotBalloonSnapshot> board,
            Vector4 wallLimitsClockwise,
            Vector2 origin,
            Vector2 aimDirection,
            int startingShields,
            float projectileContactRadius,
            ShotBalloonState[] workingSet,
            int rippleDamage,
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
            ShotItemLayer items = null,
            bool streakGrantsShields = true,
            float rippleRadius = 0f,
            ShotBumperLayer bumpers = null)
        {
            var walls = new WallLimits(wallLimitsClockwise);
            dynamics?.ResetForNewFlight();
            items?.ResetForNewFlight();
            bumpers?.ResetForNewFlight();
            var activeCount = CopyIntoWorkingSet(board, workingSet, dynamics, radiusBias);

            var direction = aimDirection.sqrMagnitude > AxisEpsilon ? aimDirection.normalized : Vector2.right;
            // The live gather seeds only what the loaded ball already carries in the thrower (a lance armed
            // there, ShotBoardGather.SeedFor); grants landing mid-flight are the item layer's. Tests also use
            // the seam to pin D-core's scoring/end-condition mirrors (including a refund firing through an
            // already-established streak once a buff takes over) without a full item layer in place.
            var state = new ShotFlightState(origin, direction, startingShields, in seed);
            var scoreRules = new ShotScoreRules(
                targetColorId, allowedColors, rainbowColorId, streakGrantsShields, rippleDamage, rippleRadius);
            RippleQueueScratch.Clear();

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

                // Before the next event is looked for: the bombs the last one set off, wherever it resolved them.
                BlowCapsOff(items, bumpers, ref state);
                var speed = Mathf.Max(CurrentSpeed(projectileSpeed, in state, cruiseConfig), MinSpeed);

                var hasWallEvent = TryFindWallCrossing(
                    walls, state.Position, state.Direction, out var wallDistance, out var wallNormal);

                var hasBalloonEvent = TryFindNearestBalloonEntryAny(
                    workingSet, activeCount, state.Position, state.Direction, speed, state.Elapsed,
                    projectileContactRadius, dynamics, out var balloonDistance, out var balloonIndex);

                if (bumpers != null
                    && TryResolveBumperEvent(
                        bumpers, walls, dynamics, hasWallEvent ? wallDistance : float.MaxValue,
                        hasBalloonEvent ? balloonDistance : float.MaxValue, speed, pathOut, timestampsOut, ref state))
                {
                    continue;
                }

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
                    cruiseConfig, dynamics, bumpers, ref state);
                if (state.Died)
                {
                    break;
                }
            }

            // A flight whose last event set a bomb off (it cleared the board, or the ball was taken) still tells
            // of the caps that bomb blew.
            BlowCapsOff(items, bumpers, ref state);

            return new ShotSimulationResult(
                state.RawScore, state.Pops, state.ToughsCleared, activeCount == 0, state.Events, state.Died,
                state.Capped, state.Absorbed, state.GoldenPops, state.HeartPops, state.DiscoPops,
                state.NukePops, state.ProjectileColor, state.StreakCount, state.IsPiercing, in state.Mushrooms);
        }

        // The bombs the last event set off (the one the ball popped, and any its blast chained into)
        // blow the caps off the mushrooms next to them, as the game's bomb handler tells MushroomBlast: gone from
        // the layer for the rest of the flight, by the one rule both sides ask (BombBlast.Pierces, the bomb's own
        // for what it breaks outright). The game's bomb goes off a frame after the pop that released it; here it
        // is the same instant, as for the balloons its blast pops. So a ball that reaches a cap next to the bomb
        // inside that frame bounces off it in the game and flies through here: an accepted limit. On a board
        // without mushrooms the blasts are only forgotten.
        private static void BlowCapsOff(ShotItemLayer items, ShotBumperLayer bumpers, ref ShotFlightState state)
        {
            if (items == null)
            {
                return;
            }

            while (items.TryTakeBombBlast(out var bomb, out var blast))
            {
                if (bumpers != null)
                {
                    state.Mushrooms.CapsBlown += bumpers.BlowOff(bomb.Origin, bomb.Slot, bomb.IsRainbowHost, in blast);
                }
            }
        }

        // The mushroom event, when it is the next one: a mushroom is met before a wall, as the live step sweeps
        // its stretch against them before it settles its walls, and before a balloon no nearer than it. True
        // when the loop's turn is spent, on the bounce or on a balance pulse that fell due before it.
        private static bool TryResolveBumperEvent(
            ShotBumperLayer bumpers, in WallLimits walls, ShotBoardDynamics dynamics, float wallDistance,
            float balloonDistance, float speed, List<Vector2> pathOut, List<float> timestampsOut,
            ref ShotFlightState state)
        {
            if (state.IsPiercing)
            {
                return TryCutCap(
                    bumpers, walls, dynamics, wallDistance, balloonDistance, speed, pathOut, timestampsOut, ref state);
            }

            if (!TryFindBumperEntry(bumpers, walls, in state, wallDistance, out var index, out var distance)
                || balloonDistance < distance)
            {
                return false;
            }

            // A ball found inside a circle bounces where its line came in, behind it: no time is flown getting
            // there, so the timeline never runs backwards.
            var flown = Mathf.Max(0f, distance);
            if (TryHandleDuePulse(dynamics, flown, speed, pathOut, timestampsOut, ref state))
            {
                return true;
            }

            state.Events++;
            state.Elapsed += flown / speed;
            BounceOffBumper(bumpers, index, distance, pathOut, timestampsOut, ref state);
            return true;
        }

        // A lance's event at a cap (ProjectileMotionResolver.CutCaps): the first whole cap its path enters before
        // its next wall is cut, there and then gone from the layer for the rest of the flight, and the ball flies
        // on unturned, so no point joins the path. The chain is not asked, spent or not, and the cap that armed
        // the ball is left out while it still flies the direction it left along. The cut banks the flight one
        // plain charge, as the game's does, re-armed with the Snipe's speed where a pierce is spent
        // (SpendPierceAtDischarge). True when the loop's turn is
        // spent, on the cut or on a balance pulse that fell due before it. Every true answer for a cut takes a
        // cap out of the layer, so the loop cannot be handed the same cap twice: a cut the layer refuses answers
        // false, and the flight goes on to its wall.
        private static bool TryCutCap(
            ShotBumperLayer bumpers, in WallLimits walls, ShotBoardDynamics dynamics, float wallDistance,
            float balloonDistance, float speed, List<Vector2> pathOut, List<float> timestampsOut,
            ref ShotFlightState state)
        {
            if (!BumperSweep.TryFindFirst(
                    bumpers.Circles, state.Position, state.Direction, wallDistance,
                    state.LeftBumper.IdWhileFlying(state.Direction), out var index, out var distance)
                || !walls.Contains(state.Position + (state.Direction * distance))
                || balloonDistance < distance)
            {
                return false;
            }

            var flown = Mathf.Max(0f, distance);
            if (TryHandleDuePulse(dynamics, flown, speed, pathOut, timestampsOut, ref state))
            {
                return true;
            }

            if (!bumpers.Cut(index))
            {
                return false;
            }

            state.Elapsed += flown / speed;
            state.Position += state.Direction * flown;
            state.Mushrooms.CapsCut++;
            BankCutCharge(bumpers.LanceSpeedBuffMultiplier, ref state);
            return true;
        }

        /// <summary>
        ///     What a cut banks (the game's <c>ProjectileMotionResolver.CutCaps</c>): one plain charge on the
        ///     flight, and the speed it re-arms the lance with where a pierce is spent
        ///     (<see cref="SpendPierceAtDischarge" />). That speed is the Snipe's speed buff
        ///     (<see cref="ShotBumperLayer.LanceSpeedBuffMultiplier" />, read at the gather from the item
        ///     configuration), the one number the game's <c>SnipeItemHandler</c> arms every lance with, a
        ///     banked charge's included. None, 0 or less, re-arms at no more than base speed.
        /// </summary>
        internal static void BankCutCharge(float lanceSpeedBuffMultiplier, ref ShotFlightState state)
        {
            state.Mushrooms.ChargesBanked++;
            state.BankedPierceCharges++;
            state.BankedPierceMultiplier = lanceSpeedBuffMultiplier > 0f ? lanceSpeedBuffMultiplier : 1f;
        }

        // The first mushroom the ball bounces off before its next wall, by the live flight's own swept test and
        // under its own conditions (ProjectileMotionResolver.BounceOffBumpers): none once the stretch has had
        // its bounces, not the mushroom being left while the ball still flies the direction it left along, and
        // none whose contact lies beyond the walls. The distance is zero or less for a ball found inside a
        // circle (set down there by a deflect off a balloon beside it): its entry is behind it, and wins.
        private static bool TryFindBumperEntry(
            ShotBumperLayer bumpers, in WallLimits walls, in ShotFlightState state, float wallDistance,
            out int index, out float distance)
        {
            index = -1;
            distance = 0f;
            if (MushroomChainRule.IsSpent(state.MushroomChain, bumpers.Rules))
            {
                return false;
            }

            return BumperSweep.TryFindFirst(
                    bumpers.Circles, state.Position, state.Direction, wallDistance,
                    state.LeftBumper.IdWhileFlying(state.Direction), out index, out distance)
                && walls.Contains(state.Position + (state.Direction * distance));
        }

        /// <summary>
        ///     One bounce off a mushroom, in the live flight's order
        ///     (<c>ProjectileMotionResolver.BounceOffBumpers</c>, then what <c>ProjectileView</c> and
        ///     <c>MushroomChainController</c> do with the bounce). Only a ball that is not piercing gets here.
        ///     The bounce is counted (<see cref="MushroomChainRule.OnBounce" />), then the game's bounce
        ///     rule is asked with the exact entry point, and the ball turns. No shield is spent and nothing is
        ///     scored. The shot's cruise and its run of empty wall bounces end, as on a balloon contact,
        ///     its taps kept; the Sweep rule's segment starts again at the bounce. Then the reward
        ///     (<see cref="PayMushroomReward" />): the live ball's step ends on the mushroom that arms it and it
        ///     is armed there, so the simulated one becomes the lance at this very point, and everything it meets
        ///     from here on is met piercing: balloons plowed, caps cut.
        /// </summary>
        /// <remarks>
        ///     Not mirrored, and not a path change: the live ball gives up what was left of the fixed step it
        ///     was armed in (under a step's length of travel, once an arming), so it runs up to a step behind this
        ///     timeline from there on. And the live step bounces off 8 mushrooms at most before it flies on
        ///     untested, which no planned board can reach in one step.
        /// </remarks>
        private static void BounceOffBumper(
            ShotBumperLayer bumpers, int index, float distance, List<Vector2> pathOut, List<float> timestampsOut,
            ref ShotFlightState state)
        {
            var circles = bumpers.Circles;
            var contact = new BumperContact(
                state.Position + (state.Direction * distance), state.Direction, circles[index]);
            var countBefore = state.MushroomChain.Count;
            var reward = MushroomChainRule.OnBounce(ref state.MushroomChain, contact.Bumper.Wall, bumpers.Rules);

            // Unreachable: a spent stretch is never searched (TryFindBumperEntry). Kept as the live flight keeps
            // it: the ball is not turned and the rule is not asked.
            if (reward == MushroomReward.PassThrough)
            {
                Log.Assert(false, "ShotSimulator", "A spent stretch was swept for mushrooms.");
                return;
            }

            var outgoing = bumpers.Rule.Resolve(contact, circles, state.MushroomChain);
            state.Position = contact.Point;
            pathOut?.Add(state.Position);
            timestampsOut?.Add(state.Elapsed);
            state.Direction = outgoing;
            state.LeftBumper.Leave(contact.Bumper.Id, outgoing);

            // ProjectileMotionResolver.TurnAtBumper.
            state.ConsecutiveWallBounces = 0;
            state.IsCruising = false;

            // ProjectileView.ResolveBumperBounces: the turn starts a new straight segment for the Sweep rule.
            state.SegmentPopCount = 0;
            state.SegmentSweepValid = true;
            state.LastBouncePosition = contact.Point;

            state.Mushrooms.Bounces++;
            if (contact.Bumper.Id >= 0 && contact.Bumper.Id < 32)
            {
                state.Mushrooms.HitMask |= 1 << contact.Bumper.Id;
            }

            var target = bumpers.Rules.ChainTarget;
            if (target > 0 && countBefore < target && state.MushroomChain.Count >= target)
            {
                state.Mushrooms.ChainsCompleted++;
            }

            PayMushroomReward(reward, bumpers.LanceSpeedBuffMultiplier, ref state);
            bumpers.BounceLog?.Add(new ShotBumperBounce(
                contact, outgoing, state.MushroomChain.Count, reward, state.IsPiercing));
        }

        /// <summary>
        ///     What <c>MushroomChainController</c> does with the chain rule's answer, as the game's
        ///     <c>SnipeItemHandler</c> does it. <see cref="MushroomReward.Arm" /> is <c>ISnipeArmer.ArmAt</c>: the
        ///     ball is piercing, with the Snipe's speed buff unless one already rides it. Any other answer pays
        ///     nothing.
        /// </summary>
        internal static void PayMushroomReward(
            MushroomReward reward, float lanceSpeedBuffMultiplier, ref ShotFlightState state)
        {
            if (reward != MushroomReward.Arm)
            {
                return;
            }

            state.Mushrooms.LancesArmed++;
            state.IsPiercing = true;
            if (lanceSpeedBuffMultiplier > 0f && !state.HasSpeedBuff)
            {
                state.HasSpeedBuff = true;
                state.SpeedBuffMultiplier = lanceSpeedBuffMultiplier;
            }
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
            ShotBoardDynamics dynamics, ShotBumperLayer bumpers, ref ShotFlightState state)
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

            EndMushroomChainAtWall(bumpers, ref state);
            state.Direction = Vector2.Reflect(state.Direction, wallNormal.normalized);
            state.ConsecutiveWallBounces++;

            var enteredCruiseHere = false;

            if (state.IsCruising && state.PierceSpeedScale < 1f)
            {
                SpendPierceAtDischarge(ref state);
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
                    workingSet, activeCount, state.Elapsed, dynamics, bumpers, in state))
            {
                state.IsCruising = true;
                enteredCruiseHere = true;
            }

            // Sweep: the OTHER rule that mints a tap, evaluated at this wall exactly as
            // ProjectileView.TryAwardSweepTap does — a segment that popped at least one balloon, every
            // contact on it a one-shot kill, and the corridor it just flew now clear. Live casts backward
            // from the wall over the segment; SegmentHitsAnyBalloon walks the same span analytically.
            // Exclusive with the cruise tap by construction (a pop clears IsCruising), and only one of the
            // two can mint per wall hit — the live funnel's invariant.
            var sweptClean = cruiseConfig.SweepEnabled && state.SegmentPopCount > 0 && state.SegmentSweepValid
                && TrySweepSegment(
                    position, projectileContactRadius, workingSet, activeCount, dynamics, ref state);

            // A Sweep is a RUN of consecutive clean clearing passes (ProjectileView.TryAwardSweepTap), so
            // any wall reached without one — an empty segment, a tougher contact, an occupied corridor —
            // starts the count over.
            if (!sweptClean)
            {
                state.ConsecutiveSweeps = 0;
            }

            var mintedTap = false;
            if (state.IsCruising)
            {
                mintedTap = CountCruiseTap(enteredCruiseHere, in cruiseConfig, ref state);
            }

            if (sweptClean && !mintedTap)
            {
                CountSweepTap(in cruiseConfig, ref state);
            }

            // This wall closes the segment: the next one starts here, with a fresh pop count and its
            // sweep validity restored (ProjectileView.MoveAndBounce resets the same three).
            state.SegmentPopCount = 0;
            state.SegmentSweepValid = true;
            state.LastBouncePosition = position;

            return false;
        }

        // A wall the shot survives is the one thing that ends a chain of mushroom bounces, here where the wall
        // bounce is counted (ProjectileMotionResolver.ResolveWalls). Nothing to end on a board without them.
        private static void EndMushroomChainAtWall(ShotBumperLayer bumpers, ref ShotFlightState state)
        {
            if (bumpers != null)
            {
                MushroomChainRule.OnWall(ref state.MushroomChain);
            }
        }

        // The corridor check, and the sweep credit that comes with passing it. ConsecutiveSweeps counts every
        // clean sweep (it is the warm-up SweepTapThreshold gates), whether or not this one pays a tap.
        private static bool TrySweepSegment(
            Vector2 wallContact, float projectileContactRadius, ShotBalloonState[] workingSet, int activeCount,
            ShotBoardDynamics dynamics, ref ShotFlightState state)
        {
            var span = wallContact - state.LastBouncePosition;
            var length = span.magnitude;
            if (length <= EventEpsilon)
            {
                return false;
            }

            if (SegmentHitsAnyBalloon(
                    state.LastBouncePosition, span / length, length, projectileContactRadius, workingSet,
                    activeCount, state.Elapsed, dynamics))
            {
                return false;
            }

            state.ConsecutiveSweeps++;
            return true;
        }

        // Mirrors the tail of ProjectileView.TryAwardSweepTap: below the warm-up threshold a sweep is
        // counted but buys no speed; at or past it every sweep mints a tap, which arms piercing at the
        // piercing threshold exactly as a cruise tap would.
        private static void CountSweepTap(in ShotCruiseConfig cruiseConfig, ref ShotFlightState state)
        {
            if (cruiseConfig.SweepTapThreshold > 0
                && state.ConsecutiveSweeps < cruiseConfig.SweepTapThreshold)
            {
                return;
            }

            state.TotalTaps++;
            if (cruiseConfig.PiercingTapThreshold > 0 && state.TotalTaps >= cruiseConfig.PiercingTapThreshold)
            {
                state.IsPiercing = true;
            }

            state.Elapsed += state.IsPiercing ? cruiseConfig.ArmRampLagSeconds : cruiseConfig.TapLagSeconds;
        }

        // A cruise bounce's tap, and the time its speed change costs the timeline (a pure time cost on the
        // event timeline, never a path change). Armed shots keep tapping — a wall hit is a wall hit — and
        // pay the ramp's cost rather than the beat's, mirroring ProjectileModelExtensions.TryGrantTap
        // handing every armed tap to the ramp.
        private static bool CountCruiseTap(
            bool enteredCruiseHere, in ShotCruiseConfig cruiseConfig, ref ShotFlightState state)
        {
            // Taps are COUNTED, exactly as live counts them (ProjectileModelExtensions.TryGrantTap) — not
            // derived from shields spent since cruise entry. The entry bounce isn't a tap itself (live
            // mints one only on a bounce taken while ALREADY cruising), but it still replays the envelope,
            // so it falls through to the beat below.
            if (enteredCruiseHere)
            {
                // Entry replays the envelope but isn't itself a tap (live mints one only on a bounce taken
                // while ALREADY cruising), so it never blocks this wall's sweep from paying instead.
                state.Elapsed += cruiseConfig.TapLagSeconds;
                return false;
            }

            state.TotalTaps++;

            // Mirrors ProjectileMotionResolver's piercing grant: a long-enough cruise arms the shot for
            // the rest of its life — contacts end the cruise, never the buff.
            if (cruiseConfig.PiercingTapThreshold <= 0 || state.TotalTaps < cruiseConfig.PiercingTapThreshold)
            {
                state.Elapsed += cruiseConfig.TapLagSeconds;
                return true;
            }

            state.IsPiercing = true;

            // The arming bounce pays the arm ramp instead of the tap beat (live hands the transition to
            // ProjectileMotionResolver's ramp there). The ramp starts from the speed the shot armed at, so
            // it only loses the fraction of the ramp the remaining speed gap accounts for — approximated
            // with the previous tap's steady target, the sim carrying no envelope state of its own to know
            // how far into a dip the arming bounce landed.
            var from = CruiseTargetMultiplier(state.TotalTaps - 1, cruiseConfig);
            var to = CruiseTargetMultiplier(state.TotalTaps, cruiseConfig);
            state.Elapsed += cruiseConfig.ArmRampLagSeconds * (1f - (from / to));
            return true;
        }

        // Only after plowing a tough (PierceSpeedScale decayed) does a wall end the run: cruise ends,
        // speed returns to base, and the earned piercing is consumed — mirrors ProjectileMotionResolver.
        // An armed shot cruising empty space keeps both.
        private static void SpendPierceAtDischarge(ref ShotFlightState state)
        {
            state.IsCruising = false;
            state.ConsecutiveWallBounces = 0;
            state.PierceSpeedScale = 1f;

            // Mirrors ProjectileModelExtensions.EndCruise: the taps that fed the spent pierce go with it,
            // so a re-armed lance restarts from base speed.
            state.TotalTaps = 0;
            state.IsPiercing = false;

            // Mirrors PierceEndedEndCondition firing on IsPiercing going false: BOTH concrete
            // pierce-riding grants end here, not just the multiplier — leaving HasSpeedBuff/
            // RainbowBuffUntilPierceEnd stuck true would wrongly block a LATER Snipe re-grant
            // (ApplyItemOutcome's non-stacking guard) and leave a stale buff flag set forever.
            state.HasSpeedBuff = false;
            state.SpeedBuffMultiplier = 1f;
            state.RainbowBuffUntilPierceEnd = false;

            // Mirrors ProjectileModelExtensions.SpendPierce: a Snipe banked mid-pierce activates on this
            // discharge, so the shot stays armed (live never dips the flag — the level-up commit gates on
            // it) but restarts from base speed, the cruise ramp above already dropped. Live re-applies the
            // grant through SnipeItemHandler.ArmLance, whose non-stacking guard is moot here since the
            // lines above just cleared the outgoing pierce's own buff. Rainbow charges spend first.
            var isRainbowCharge = state.BankedRainbowPierceCharges > 0;
            if (!isRainbowCharge && state.BankedPierceCharges <= 0)
            {
                return;
            }

            if (isRainbowCharge)
            {
                state.BankedRainbowPierceCharges--;
                state.RainbowBuffUntilPierceEnd = true;
            }
            else
            {
                state.BankedPierceCharges--;
            }

            state.IsPiercing = true;
            state.HasSpeedBuff = true;
            state.SpeedBuffMultiplier = state.BankedPierceMultiplier;
        }

        // The steady-state cruise speed multiplier for a given tap count, capped as live caps it.
        private static float CruiseTargetMultiplier(int taps, in ShotCruiseConfig cruiseConfig)
        {
            // Unclamped — the speed rail is applied to the final speed in CurrentSpeed, buffs included.
            return 1f + (cruiseConfig.SpeedGainPerTap * Mathf.Max(taps, 0));
        }

        // Mirrors ProjectileMotionResolver.Step's cruise ramp exactly: every qualifying wall hit adds a
        // velocity TAP of SpeedGainPerTap, cumulatively. This is the steady-state target; the per-tap
        // animation envelope is folded into ShotCruiseConfig.TapLagSeconds on the timeline instead.
        private static float CurrentSpeed(float baseSpeed, in ShotFlightState state, in ShotCruiseConfig cruiseConfig)
        {
            // Mirrors ProjectileMotionResolver.ResolveFlightSpeed: the buff multiplies the base speed
            // BEFORE the cruise ramp, and is still the floor when the shot has no taps at all —
            // default 1f keeps this byte-identical until Phase C ever grants the buff.
            var buffedBase = baseSpeed * state.SpeedBuffMultiplier;

            // Gated on the TAP COUNT, not on IsCruising: live reads TotalCruiseTaps regardless, so a shot
            // that pops something (ending its cruise but keeping its taps) holds the speed it earned. The
            // old !IsCruising gate dropped it to base and under-predicted every post-pop segment.
            // Pierce scale bleeds the ramp down through tough plows; floor at (buffed) base speed.
            var speed = state.TotalTaps <= 0
                ? buffedBase
                : Mathf.Max(
                    buffedBase * CruiseTargetMultiplier(state.TotalTaps, cruiseConfig) * state.PierceSpeedScale,
                    buffedBase);

            // The safety rail on the FINAL speed, mirroring ResolveFlightSpeed: measured against the
            // UNBUFFED base, so a speed buff falls inside the ceiling instead of multiplying past it.
            return cruiseConfig.MaxSpeedMultiplier > 0f
                ? Mathf.Min(speed, baseSpeed * cruiseConfig.MaxSpeedMultiplier)
                : speed;
        }

        // The event-timeline mirror of the live path-clear check (Shared.PathTrace / the predicate
        // ProjectileView feeds it): traces the wall-reflected ray for `bounces` more crossings, checking
        // each segment (up to its own wall-crossing point only, matching the game's per-segment
        // CircleCast) against every active balloon's CURRENT centre — frozen at tHit, since the live
        // check is one instantaneous physics query, not a projection of future balloon motion.
        // Deliberately NOT routed through PathTrace: it keeps its own determinism-tuned wall crossing
        // (TryFindWallCrossing, shared with the main flight loop) and its analytic occupancy test.
        // A mushroom blocks a leg as a balloon does, as it does the live lookahead: the shot would bounce off it.
        private static bool IsPathClearAhead(
            in WallLimits walls, Vector2 position, Vector2 direction, int bounces, float projectileContactRadius,
            ShotBalloonState[] workingSet, int activeCount, float tHit, ShotBoardDynamics dynamics,
            ShotBumperLayer bumpers, in ShotFlightState state)
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

                if (bumpers != null && LegMeetsABumper(bumpers, in state, position, direction, wallDistance, i))
                {
                    return false;
                }

                position += direction * wallDistance;
                direction = Vector2.Reflect(direction, wallNormal.normalized);
            }

            return true;
        }

        // BumperLookahead.TryFindAhead for the simulated flight: whether a mushroom stands in the way of a leg
        // looked along. None stands in a lance's way. For a ball that is not one, two things about its own flight
        // take a mushroom out of its way, and both hold only for the leg the ball is on (leg 0): a spent stretch,
        // and the mushroom it is leaving. Past a wall every mushroom is in the way: the wall clears the chain.
        private static bool LegMeetsABumper(
            ShotBumperLayer bumpers, in ShotFlightState state, Vector2 from, Vector2 direction, float length, int leg)
        {
            if (state.IsPiercing)
            {
                return false;
            }

            var isTheBallsOwnLeg = leg == 0;
            if (isTheBallsOwnLeg && MushroomChainRule.IsSpent(state.MushroomChain, bumpers.Rules))
            {
                return false;
            }

            var heading = direction.normalized;
            var leaving = isTheBallsOwnLeg ? state.LeftBumper.IdWhileFlying(heading) : MushroomChainState.NoBumper;
            return BumperSweep.TryFindFirst(bumpers.Circles, from, heading, length, leaving, out _, out _);
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
            // A coat is the exception: a pierce pops its balloon, coat and all, and that pop steals the colour as any
            // pop does (ApplyColorChange washes a coat's contact only when the balloon survives it).
            var coatWashes = balloon.Coat > 0 && !state.IsPiercing;
            if ((balloon.WashesProjectileColor || coatWashes) && !string.IsNullOrEmpty(state.ProjectileColor))
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

            // A coat takes an ordinary hit of its own (BalloonModel.EvaluateHit): a layer comes off, the balloon stays,
            // the shot flies on unbent having been washed above, and nothing scores or touches the streak. It is no
            // one-contact pop, so the segment is no clean sweep.
            var flags = state.IsPiercing ? DamageFlags.Piercing : DamageFlags.DirectHit;
            if (CoatStops(ref balloon, flags, 1))
            {
                state.SegmentSweepValid = false;
                return;
            }

            // A nuke never pops on contact (NukeBalloonModel): its last dent, or a pierce, arms it and the ball
            // bounces off, and once armed it bounces every ball. A pierce with dents to spare plows on through,
            // arming it on the way. Its blast comes a fuse later, past this flight, so arming is what's counted.
            var armsNuke = balloon.Reward == ShotRewardKind.Nuke && (state.IsPiercing || balloon.HitsRemaining <= 1);
            if (armsNuke && state.IsPiercing && balloon.HitsRemaining > 1)
            {
                ArmNuke(ref balloon, ref state);
                state.SegmentSweepValid = false;
                state.PierceSpeedScale *= 0.5f;
                return;
            }

            // Every hit on a cluster scores the bubbles it held (BubbleClusterModel.ResolveScoreAttribution).
            if (balloon.ContactKind == ShotContactKind.PassThrough)
            {
                balloon.ScoreValue = balloon.HitsRemaining;
            }

            // A cluster survives an ordinary hit as a pass-through (BalloonModelBase's default survive outcome): the
            // hit is scored like a pop, the shot flies on unbent, and the cluster stays a bubble smaller. A ripe one
            // has one hit under it (HitsUnderCoat): it bursts.
            if (balloon.ContactKind == ShotContactKind.PassThrough && !state.IsPiercing && balloon.HitsUnderCoat > 1)
            {
                state.SegmentSweepValid = false;
                var survivingCause = ShotPopCause.ProjectileContact(
                    state.HasRainbowBuff, false, isRainbowTargetCarry: false, projectileColor: state.ProjectileColor,
                    isOwnPaint: false);
                ScoreSurvivingClusterHit(in balloon, in scoreRules, in survivingCause, ref state);
                balloon.HitsRemaining--;
                return;
            }

            // A piercing shot pops EVERYTHING it touches (DamageFlags.Piercing — unbreakables
            // included) and flies on unbent; only unarmed shots deflect off durable actors.
            if (armsNuke || (!state.IsPiercing && balloon.HitsUnderCoat > 1))
            {
                if (armsNuke)
                {
                    ArmNuke(ref balloon, ref state);
                }
                else
                {
                    balloon.HitsRemaining--;
                }

                DeflectOffBalloon(
                    center, balloon.Radius + projectileContactRadius, contactPosition, ref state.Direction);

                // A DEFLECT wipes the taps earned so far, unlike a pop — mirrors
                // ProjectileView.OnBalloonDeflected ("deflection interrupts free roaming"). Reachable only
                // now that taps are counted rather than derived: the old derivation was moot here, since
                // the same contact cleared IsCruising and the speed read bailed on that instead.
                // ...and it restarts the segment from the contact, exactly as that method does: "deflection
                // interrupts free roaming" resets EVERY piece of progress — both counters to zero, a fresh
                // pop count, sweep validity restored, and the segment re-anchored here. (The resolver's own
                // `else if (!wasOneHitBalloon)` invalidation is what this overwrites: the view's reset runs
                // after the hit resolver in the same frame, so the reset is the net live behaviour.)
                state.TotalTaps = 0;
                state.ConsecutiveSweeps = 0;
                state.ConsecutiveWallBounces = 0;
                state.SegmentPopCount = 0;
                state.SegmentSweepValid = true;
                state.LastBouncePosition = contactPosition;

                // Deflected statics do NOT self-shove — they are immovable and a live static never
                // wobbles, unlike a deflected balloon (OnBalloonDeflected).
                if (balloon.Actor != null)
                {
                    dynamics?.OnBalloonDeflected(balloon.Actor, incomingDirection, tHit);
                }

                return;
            }

            // Sweep bookkeeping for the pop about to happen, mirroring ResolveContactPop: the pop counts
            // toward the segment, and only a target that was at 1 HP when struck leaves the segment
            // sweepable — so a piercing tough plow disqualifies it, as live's plow does.
            state.SegmentPopCount++;
            state.SegmentSweepValid &= balloon.HitsUnderCoat <= 1;

            // Plowing a tough (>1-hit) actor while piercing halves the cruise speed (floored at base
            // in CurrentSpeed) — mirrors ProjectileHitResolver. A ripe cluster is no tough: it bursts on contact.
            if (state.IsPiercing && balloon.HitsUnderCoat > 1)
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
            var isOwnPaint = balloon.IsRainbow && balloon.OwnPaint && !state.HasRainbowBuff;
            var cause = ShotPopCause.ProjectileContact(
                state.HasRainbowBuff, state.IsPiercing,
                isRainbowTargetCarry: balloon.IsRainbow && !state.HasRainbowBuff && !isOwnPaint,
                projectileColor: state.ProjectileColor,
                isOwnPaint: isOwnPaint);
            ResolvePopScore(balloon, in scoreRules, in cause, ref state);
            PaintBoardIfDisco(workingSet, activeCount, balloon.Reward, scoreRules.RainbowColorId);

            // A rainbow-buffed shot converts nearby paintable balloons on every pop it lands, not
            // just a rainbow-target one — mirrors ProjectileHitResolver.ConvertSideNeighboursToRainbow,
            // run over the ACTIVE WORKING SET (SlotIndex-addressed) so it works with dynamics: null.
            if (state.HasRainbowBuff)
            {
                ConvertSideNeighboursToRainbow(workingSet, activeCount, balloon.SlotIndex, state.Direction, scoreRules.RainbowColorId);
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

            // A ripe cluster's burst ripples into the balloons round it. The game runs it BurstSeconds after the
            // contact (and each chained burst as long after its own); the simulation resolves the whole chain here.
            // Items its pops release join the item queue, drained after it as any chained item is.
            if (host.Ripe)
            {
                RippleQueueScratch.Add(host.SlotCenter);
                RunRipples(workingSet, ref activeCount, dynamics, tHit, in scoreRules, items, ref state);
                DrainItemQueue(items, tHit, workingSet, ref activeCount, dynamics, in scoreRules, ref state);
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

            DrainItemQueue(items, tHit, workingSet, ref activeCount, dynamics, in scoreRules, ref state);
        }

        // Breadth-first over the item queue: an activation whose hits pop another carrier (or burst a ripe cluster
        // whose ripple does) enqueues it, and it resolves on a later iteration here.
        private static void DrainItemQueue(
            ShotItemLayer items, float tHit, ShotBalloonState[] workingSet, ref int activeCount,
            ShotBoardDynamics dynamics, in ShotScoreRules scoreRules, ref ShotFlightState state)
        {
            if (items == null)
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

            // A pierce-arming pickup (Snipe) landing on an ALREADY-armed shot is banked whole rather than
            // applied — two pierces can't overlap, so it activates at the discharge that spends the
            // running one (HandleWallBounce). Mirrors SnipeItemHandler.Activate: the speed and rainbow
            // grants defer with it, so they can't compound onto the pierce already in flight.
            if (outcome.ArmsPierce && state.IsPiercing)
            {
                if (outcome.GrantsRainbowBuffUntilPierceEnd)
                {
                    state.BankedRainbowPierceCharges++;
                }
                else
                {
                    state.BankedPierceCharges++;
                }

                state.BankedPierceMultiplier = outcome.SpeedBuffMultiplier > 0f
                    ? outcome.SpeedBuffMultiplier
                    : 1f;
                return;
            }

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
                // A nuke's dents come only from the shot's own hit: plain item damage is HitOutcome.None live, and
                // piercing damage arms it rather than popping it.
                if (workingSet[index].Reward == ShotRewardKind.Nuke)
                {
                    if (pops)
                    {
                        ArmNuke(ref workingSet[index], ref state);
                    }

                    continue;
                }

                if (CoatStops(ref workingSet[index], flags, outcome.Damage))
                {
                    continue;
                }

                var isCluster = workingSet[index].ContactKind == ShotContactKind.PassThrough;
                if (isCluster)
                {
                    workingSet[index].ScoreValue = workingSet[index].HitsRemaining;
                }

                // A ripe cluster bursts whole on any damaging hit (BubbleClusterModel.EvaluateHit).
                pops |= workingSet[index].Ripe && outcome.Damage > 0;
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
                else if (isCluster)
                {
                    // Every hit on a cluster scores the bubbles it held and breaks the streak, an item's too.
                    var cause = ShotPopCause.ItemEffect(activation.SourceColorId, flags);
                    ScoreSurvivingClusterHit(in workingSet[index], in scoreRules, in cause, ref state);
                }
            }

            // After every hit of the activation has landed, as the game's ripple comes a tick after the blast.
            RunRipples(workingSet, ref activeCount, dynamics, tHit, in scoreRules, items, ref state);
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
        // ConvertSideNeighboursToRainbow/IsCruising/ConsecutiveWallBounces — those are projectile-contact-only.
        private static void PopItemHit(
            ShotItemLayer items, ShotBalloonState[] workingSet, ref int activeCount, int index, float tHit,
            ShotBoardDynamics dynamics, in ShotScoreRules scoreRules, string sourceColorId, DamageFlags flags,
            ref ShotFlightState state)
        {
            var cause = ShotPopCause.ItemEffect(sourceColorId, flags);
            ResolvePopScore(workingSet[index], in scoreRules, in cause, ref state);
            PaintBoardIfDisco(workingSet, activeCount, workingSet[index].Reward, scoreRules.RainbowColorId);

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

            // Its ripple runs once the hits in hand have landed (RunRipples): a ripple's own pops only queue more.
            if (popped.Ripe)
            {
                RippleQueueScratch.Add(popped.SlotCenter);
            }

            if (items != null && popped.Item != ItemType.None)
            {
                var chained = new ShotItemActivation(
                    popped.Item, popped.Position, Vector2.zero, popped.SlotIndex, popped.ColorId, popped.IsRainbow,
                    popped.ItemSpinDegrees + (popped.ItemSpinRate * tHit), isDirectHit: false);
                items.TryBeginActivation(in chained);
            }
        }

        // Shared by ApplyEffectHits' Recolor case and ConvertSideNeighboursToRainbow's rainbow-buff
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
            state.CarryOnColorChange = false;
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
        //   tough/Unbreakable pop just keeps the multiplier climbing; (2) CarryStreak climbs a rainbow
        //   pop colour-agnostically, coloured shot or not yet; (3) absent both flags, a colourless target that doesn't
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
            if (!balloon.Scoreless)
            {
                ResolveScoredPop(in balloon, in scoreRules, in cause, ref state);
                return;
            }

            // A reward balloon: no score, and the streak is left as it was. Each kind is counted apart.
            switch (balloon.Reward)
            {
                case ShotRewardKind.Golden:
                    state.GoldenPops++;
                    break;
                case ShotRewardKind.Heart:
                    state.HeartPops++;
                    break;
                case ShotRewardKind.Disco:
                    state.DiscoPops++;
                    break;
                case ShotRewardKind.Nuke:
                    state.NukePops++;
                    break;
                default:
                    Log.Assert(false, "ShotSimulator", $"Scoreless pop of an uncounted reward kind {balloon.Reward}.");
                    break;
            }
        }

        private static void ResolveScoredPop(
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
            if (cause.Flags.HasFlag(DamageFlags.OwnPaint))
            {
                // Mirrors ScoreController's OwnPaint: scores at the streak as it stands and leaves it there.
                multiplier = Math.Max(1, state.StreakCount);
                attributedColorId = ContainsColor(allowedColors, cause.SourceColorId)
                    ? cause.SourceColorId
                    : allowedColors[0];
            }
            else if (cause.Flags.HasFlag(DamageFlags.Ripple))
            {
                // Mirrors ScoreController's Ripple: the ripple lands after the pop that set it off, so it scores at the
                // streak as it stands and never changes it, not even to break it on a cluster or a tough.
                multiplier = Math.Max(1, state.StreakCount);
                if (string.IsNullOrEmpty(balloon.ColorId) && !balloon.PaysSourceColor)
                {
                    var countsRipple = string.IsNullOrEmpty(targetColorId);
                    state.RawScore += (countsRipple ? balloon.ScoreValue : 0) * multiplier;
                    state.ToughsCleared++;
                    return;
                }

                attributedColorId = !balloon.IsRainbow
                    ? balloon.ColorId
                    : ContainsColor(allowedColors, cause.SourceColorId)
                        ? cause.SourceColorId
                        : allowedColors[0];
            }
            else if (cause.Flags.HasFlag(DamageFlags.WildcardStreak))
            {
                multiplier = ++state.StreakCount;
                state.CarryOnColorChange = true;
                attributedColorId = balloon.PaysSourceColor ? cause.SourceColorId : balloon.ColorId;
            }
            else if (cause.Flags.HasFlag(DamageFlags.CarryStreak))
            {
                // Mirrors RecordWildcard: colour-agnostic increment + arm carry.
                multiplier = ++state.StreakCount;
                state.CarryOnColorChange = true;
                if (balloon.IsRainbow)
                {
                    attributedColorId = ContainsColor(allowedColors, cause.SourceColorId)
                        ? cause.SourceColorId
                        : allowedColors[0];
                }
                else
                {
                    attributedColorId = balloon.PaysSourceColor ? cause.SourceColorId : balloon.ColorId;
                }
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

            if (StreakShieldRule.GrantsShield(
                    scoreRules.StreakGrantsShields,
                    cause.IsProjectileContact && !cause.Flags.HasFlag(DamageFlags.OwnPaint),
                    !string.IsNullOrEmpty(balloon.ColorId), state.StreakCount, state.StreakColor,
                    state.ProjectileColor))
            {
                state.Shields++;
            }
        }

        // A cluster's surviving hit scores as its pop would (the bubbles it held, and the streak rule its cause brings),
        // but nothing left the board, so it is no tough cleared.
        private static void ScoreSurvivingClusterHit(
            in ShotBalloonState cluster, in ShotScoreRules scoreRules, in ShotPopCause cause, ref ShotFlightState state)
        {
            var cleared = state.ToughsCleared;
            ResolvePopScore(in cluster, in scoreRules, in cause, ref state);
            state.ToughsCleared = cleared;
        }

        // The ripple of every ripe cluster queued in RippleQueueScratch (RipeBubbleBurst.Ripple): one hit of
        // RippleDamage, flagged Ripple, to each target RippleTakes whose slot the burst reaches. A ripe cluster it
        // bursts joins the queue, so a chain runs to its end in this one index loop; each cluster leaves the board
        // as it bursts, so the chain is bounded. The reach is the game's own rule (RipeBubbleRule.RippleReaches)
        // over the slot centres gathered with the board; the targets are then found in the working set by slot, as
        // the rainbow side-conversion and the item effect board find theirs.
        private static void RunRipples(
            ShotBalloonState[] workingSet, ref int activeCount, ShotBoardDynamics dynamics, float tHit,
            in ShotScoreRules scoreRules, ShotItemLayer items, ref ShotFlightState state)
        {
            // Simulate's rippleRadius is optional (0), so a caller that sets a ripe cluster up and forgets it would
            // simulate a burst that reaches nothing, in silence.
            Log.Assert(
                RippleQueueScratch.Count == 0 || scoreRules.RippleRadius > 0f, "ShotSimulator",
                "A ripe cluster bursts with no ripple radius: pass rippleRadius to Simulate.");
            for (var q = 0; q < RippleQueueScratch.Count; q++)
            {
                CollectRippleSlots(workingSet, activeCount, RippleQueueScratch[q], scoreRules.RippleRadius);
                for (var n = 0; n < RippleSlotsScratch.Count; n++)
                {
                    var index = FindActiveIndex(workingSet, activeCount, RippleSlotsScratch[n]);
                    if (index >= 0 && RippleTakes(in workingSet[index]))
                    {
                        ApplyRippleHit(workingSet, ref activeCount, index, dynamics, tHit, in scoreRules, items, ref state);
                    }
                }
            }

            RippleQueueScratch.Clear();
        }

        // Every slot still on the board that a burst at origin reaches, in the order the game hits them (column,
        // then row: RipeBubbleBurst.CollectBlast). Taken before any hit lands, as the game takes them: a hit's
        // swap-remove reorders the working set under the loop.
        private static void CollectRippleSlots(
            ShotBalloonState[] workingSet, int activeCount, Vector2 origin, float radius)
        {
            RippleSlotsScratch.Clear();
            for (var i = 0; i < activeCount; i++)
            {
                if (!RipeBubbleRule.RippleReaches(origin, workingSet[i].SlotCenter, radius))
                {
                    continue;
                }

                var slot = workingSet[i].SlotIndex;
                var at = RippleSlotsScratch.Count;
                while (at > 0 && ComesBefore(slot, RippleSlotsScratch[at - 1]))
                {
                    at--;
                }

                RippleSlotsScratch.Insert(at, slot);
            }
        }

        private static bool ComesBefore(Vector2Int slot, Vector2Int other)
        {
            return slot.x < other.x || (slot.x == other.x && slot.y < other.y);
        }

        // Mirrors BalloonModel.EvaluateHit on a coated balloon: a piercing or beam hit takes the coat whole and goes on
        // to the balloon; any other hit takes a layer (if it does damage) and goes no further. Returns whether the coat
        // stopped the hit.
        private static bool CoatStops(ref ShotBalloonState target, DamageFlags flags, int damage)
        {
            if (target.Coat <= 0)
            {
                return false;
            }

            if ((flags & (DamageFlags.Piercing | DamageFlags.Beam)) != 0)
            {
                target.Coat = 0;
                return false;
            }

            if (damage > 0)
            {
                target.Coat--;
            }

            return true;
        }

        // Mirrors RipeBubbleRule.RippleTakes: any balloon but a reward balloon or one that deflects an ordinary hit
        // (a colourless target with more than one hit left: a tough, an unbreakable). Grid actors are no balloons.
        private static bool RippleTakes(in ShotBalloonState target)
        {
            var deflects = target.ContactKind == ShotContactKind.Poppable && string.IsNullOrEmpty(target.ColorId)
                && target.HitsRemaining > 1;
            return !target.IsStatic && !target.Scoreless && !deflects;
        }

        // One ripple hit, as the game's models take it: a coat loses a layer and nothing else happens; a ripe cluster
        // bursts; anything else takes the damage. A surviving cluster scores what it held at the streak as it stands;
        // a pop goes the way an item's pop goes (PopItemHit), so a carrier it pops chains its item, and a ripe cluster
        // it bursts queues its own ripple.
        private static void ApplyRippleHit(
            ShotBalloonState[] workingSet, ref int activeCount, int index, ShotBoardDynamics dynamics, float tHit,
            in ShotScoreRules scoreRules, ShotItemLayer items, ref ShotFlightState state)
        {
            // A ripple of no damage is no hit: nothing nudges, no coat comes off, nothing scores.
            var damage = scoreRules.RippleDamage;
            if (damage <= 0)
            {
                return;
            }

            ref var target = ref workingSet[index];
            NudgeItemHit(dynamics, target, tHit);

            if (CoatStops(ref target, DamageFlags.Ripple, damage))
            {
                return;
            }

            var isCluster = target.ContactKind == ShotContactKind.PassThrough;
            if (isCluster)
            {
                target.ScoreValue = target.HitsRemaining;
            }

            var pops = target.Ripe && damage > 0;
            if (!pops)
            {
                target.HitsRemaining -= damage;
                pops = target.HitsRemaining <= 0;
            }

            if (pops)
            {
                PopItemHit(
                    items, workingSet, ref activeCount, index, tHit, dynamics, in scoreRules, string.Empty,
                    DamageFlags.Ripple, ref state);
            }
            else if (isCluster)
            {
                var cause = ShotPopCause.ItemEffect(string.Empty, DamageFlags.Ripple);
                ScoreSurvivingClusterHit(in target, in scoreRules, in cause, ref state);
            }
        }

        // Mirrors ColorStreakTracker.Record's non-breaking branch, including carry-on-color-change:
        // repeating the same colour just increments, a colour change with carry transfers the
        // multiplier, otherwise a new colour starts at 1.
        private static int RecordColor(string colorId, ref ShotFlightState state)
        {
            if (string.Equals(state.StreakColor, colorId, StringComparison.Ordinal))
            {
                state.StreakCount++;
            }
            else if (state.CarryOnColorChange && state.StreakCount > 0)
            {
                state.StreakColor = colorId;
                state.StreakCount++;
                state.CarryOnColorChange = false;
            }
            else
            {
                state.StreakCount = 1;
                state.StreakColor = colorId;
                state.CarryOnColorChange = false;
            }

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

        // Mirrors ProjectileHitResolver.ConvertSideNeighboursToRainbow — a rainbow-buffed pop converts
        // only the two side neighbours orthogonal to the projectile direction, not every nearby neighbour.
        // This runs over the ACTIVE WORKING SET (SlotIndex-addressed linear scan, not the dynamics grid),
        // so it works with dynamics: null too. Only a poppable, still-coloured, non-rainbow, non-static
        // target converts — IPaintable is "poppable, coloured" today.
        private static void ConvertSideNeighboursToRainbow(
            ShotBalloonState[] workingSet, int activeCount, Vector2Int slot, Vector2 direction, string rainbowColorId)
        {
            if (direction.sqrMagnitude < 1e-8f)
            {
                return;
            }

            direction.Normalize();
            var centerIndex = FindActiveIndex(workingSet, activeCount, slot);
            if (centerIndex < 0)
            {
                return;
            }

            var center = workingSet[centerIndex].Position;
            HexCoordinates.HexNeighborIndices(slot.x, slot.y, NeighborBuffer);

            var count = 0;
            for (var n = 0; n < 6; n++)
            {
                var neighbor = NeighborBuffer[n];
                for (var i = 0; i < activeCount; i++)
                {
                    if (workingSet[i].SlotIndex != neighbor)
                    {
                        continue;
                    }

                    SideNeighborOffsetsScratch[count] = workingSet[i].Position - center;
                    SideNeighborWorkingSetIndicesScratch[count] = i;
                    count++;
                    break;
                }
            }

            if (count == 0)
            {
                return;
            }

            VectorMathExtensions.GetTwoMostPerpendicular(
                direction, SideNeighborOffsetsScratch, count, out var firstIndex, out var secondIndex);
            RecolorSideNeighbor(workingSet, firstIndex, rainbowColorId);
            RecolorSideNeighbor(workingSet, secondIndex, rainbowColorId);
        }

        private static void RecolorSideNeighbor(ShotBalloonState[] workingSet, int sideIndex, string rainbowColorId)
        {
            if (sideIndex < 0)
            {
                return;
            }

            var i = SideNeighborWorkingSetIndicesScratch[sideIndex];
            if (IsRainbowPaintable(in workingSet[i]))
            {
                ApplyRecolor(ref workingSet[i], rainbowColorId, rainbowColorId);
            }
        }

        // Mirrors PaintTargets under rainbow paint: a colourless balloon (tough, unbreakable, plated) resists it.
        private static bool IsRainbowPaintable(in ShotBalloonState balloon)
        {
            return !balloon.IsStatic && !balloon.IsRainbow && !string.IsNullOrEmpty(balloon.ColorId);
        }

        // A disco ball's pop paints the whole board rainbow (DiscoWave), booked to this shot. Live, the disc takes
        // DiscoWaveSettings.Seconds to cross the board and a pierced ball only pops at the next wall; here it is
        // instant, which lets the rest of the flight score painted balloons a little early.
        private static void PaintBoardIfDisco(
            ShotBalloonState[] workingSet, int activeCount, ShotRewardKind reward, string rainbowColorId)
        {
            if (reward != ShotRewardKind.Disco || string.IsNullOrEmpty(rainbowColorId))
            {
                return;
            }

            for (var i = 0; i < activeCount; i++)
            {
                if (IsRainbowPaintable(in workingSet[i]))
                {
                    ApplyRecolor(ref workingSet[i], rainbowColorId, rainbowColorId);
                    workingSet[i].OwnPaint = true;
                }
            }
        }

        // An armed nuke is one with no dents left; it stays on the board, counted once (NukePops). The board it clears
        // goes after its fuse, which outlasts the flight and kills every shot still in it (NukeBlast).
        private static void ArmNuke(ref ShotBalloonState nuke, ref ShotFlightState state)
        {
            if (nuke.HitsRemaining <= 0)
            {
                return;
            }

            nuke.HitsRemaining = 0;
            state.NukePops++;
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

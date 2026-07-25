using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Solver;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>Phase D-core (@ref plan_shot_solver_accuracy §4 Phase D-core): rainbow/wildcard
    /// scoring + the in-sim buff state (<c>ShotFlightState.HasRainbowBuff</c>/<c>SpeedBuffMultiplier</c>),
    /// mirroring <c>ProjectileHitResolver.ResolveContactPop</c>, <c>ScoreController.RecordStreakMultiplier</c>,
    /// <c>ColorStreakTracker</c>, and <c>BalloonModel.ResolveRainbowAttribution</c>. Buff GRANTS
    /// (item layer) are Phase C — out of scope here; <c>ShotSimulator.Simulate</c>'s
    /// <c>seed</c> parameter (a <c>ShotFlightSeed</c>) is this phase's test seam standing in for a
    /// grant.</summary>
    [TestFixture]
    public class ShotBuffScoringTests
    {
        // Mirrors ShotSimulatorTests'/ShotStaticContactTests' convention: a box far larger than
        // anything fired in these tests, so only the geometry each test cares about ever produces
        // an event.
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

        private static ColorStreakTracker CreateTracker()
        {
            // Same construction precedent as ColorStreakTrackerTests/ProjectileHitResolverTests: the
            // level-up and projectile-loaded subscriptions are never exercised here, only stubbed so
            // the constructor doesn't NRE.
            var levelUpSubscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            levelUpSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ScoreLevelUpMessage>>(),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var projectileLoadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            projectileLoadedSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ProjectileLoadedMessage>>(),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            return new ColorStreakTracker(
                Substitute.For<IPublisher<StreakChangedMessage>>(), levelUpSubscriber, projectileLoadedSubscriber);
        }

        [Test]
        public void ResolvePopScore_ColorStreakSequence_MatchesRealColorStreakTrackerRecordCalls()
        {
            // Drives a REAL ColorStreakTracker through the exact same call sequence the sim's
            // RecordColor dispatch makes for this board (green, wildcard-with-a-real-colour rainbow,
            // green again — see ProjectileHitResolver.cs:154 adopt-then-record ordering), then checks
            // the sim's RawScore against the tracker's own multipliers — the field-mapping proof that
            // RecordColor really does mirror ColorStreakTracker.Record.
            var tracker = CreateTracker();
            var expectedMultipliers = new[]
            {
                tracker.Record("Red"),
                tracker.Record("Red"),
                tracker.Record("Red"),
            };

            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 2, 1),
                ShotBoardBuilder.Rainbow(new Vector2(0f, 2f), 0.1f, GamePalette.RainbowColorId, 2, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Red", 2, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, allowedColors: new[] { "Red" });

            var expectedScore = (2 * expectedMultipliers[0]) + (2 * expectedMultipliers[1]) + (2 * expectedMultipliers[2]);
            Assert.AreEqual(expectedScore, result.RawScore);
            Assert.AreEqual(3, result.Pops);
        }

        [Test]
        public void ResolvePopScore_ColorlessProjectile_RainbowPopsDefer_ThenFoldIntoTheNextRealColour()
        {
            // Mirrors RecordDeferred/RecordDeferred/Record's fold (ColorStreakTrackerTests'
            // RecordDeferred_ThenRecord_FoldsIntoStreak, live-side).
            var tracker = CreateTracker();
            tracker.RecordDeferred();
            var expectedFold = tracker.RecordDeferred();
            var expectedFoldedStreak = tracker.Record("Red");

            var board = new[]
            {
                ShotBoardBuilder.Rainbow(new Vector2(0f, 1f), 0.1f, GamePalette.RainbowColorId, 5, 1),
                ShotBoardBuilder.Rainbow(new Vector2(0f, 2f), 0.1f, GamePalette.RainbowColorId, 5, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Red", 3, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, allowedColors: new[] { "Red" });

            Assert.AreEqual(2, expectedFold, "sanity: two deferred pops bank to 2");
            Assert.AreEqual(3, expectedFoldedStreak, "sanity: the fold gives the third pop a streak of 3");

            // Both deferred rainbow pops pay unfiltered (allowedColors.Count == 1, multiplier 1 each);
            // the folded green pop pays its own ScoreValue at the folded multiplier.
            var expectedScore = (5 * 1) + (5 * 1) + (3 * expectedFoldedStreak);
            Assert.AreEqual(expectedScore, result.RawScore);
            Assert.AreEqual(3, result.Pops);
        }

        [Test]
        public void ResolvePopScore_RainbowBuff_ScoresColorAgnosticallyAndConvertsHexNeighbour()
        {
            // Two buffed pops of DIFFERENT colours (Blue then Green) still climb the SAME streak
            // (1, then 2) instead of resetting — including a colourless (tough) pop in between,
            // which would normally break the streak but doesn't under HasRainbowBuff (the
            // WildcardStreak flag pre-empts ScoreController ever looking at BreaksStreak, live-side).
            // The Blue pop's hex neighbour (slot (1,0), off the ray entirely) converts to rainbow.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.1f, "Blue", 3, 1,
                    slotIndex: new Vector2Int(0, 0), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 1f,
                    directBalanceMotion: false, nudgeOverrides: null),
                ShotBoardBuilder.Green(
                    new Vector2(5f, 1f), 0.1f, "Red", 1, 1,
                    slotIndex: new Vector2Int(1, 0), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 1f,
                    directBalanceMotion: false, nudgeOverrides: null),
                ShotBoardBuilder.Tough(new Vector2(0f, 2f), 0.1f, 7, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Green", 2, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, rainbowColorId: GamePalette.RainbowColorId,
                seed: ShotFlightSeed.WithRainbowBuff(untilWall: true));

            // multiplier climbs 1 (Blue), 2 (tough — NOT reset), 3 (Green) — RawScore reflects it.
            var expectedScore = (3 * 1) + (7 * 2) + (2 * 3);
            Assert.AreEqual(expectedScore, result.RawScore);
            Assert.AreEqual(3, result.Pops);
            Assert.AreEqual(0, result.ToughsCleared, "the buff routes the tough through the shared payout, never ResolveToughPop");

            // Only the Blue pop's own hex neighbour (slot (1,0)) is in the working set; after it pops
            // and swap-removes, the survivor (the off-path "Red" at slot (1,0)) shifts into slot 0.
            Assert.IsTrue(workingSet[0].IsRainbow, "the Blue pop's hex neighbour converts to rainbow");
            Assert.AreEqual(GamePalette.RainbowColorId, workingSet[0].ColorId);
        }

        [Test]
        public void ResolvePopScore_RainbowTarget_PaysEveryAllowedColour_UnfilteredAndUnderAnyFilter()
        {
            var allowedColors = new[] { "Red", "Blue", "Green" };

            var unfilteredBoard = new[]
                { ShotBoardBuilder.Rainbow(new Vector2(0f, 1f), 0.1f, GamePalette.RainbowColorId, 10, 1) };
            var unfilteredWorkingSet = new ShotBalloonState[unfilteredBoard.Length];
            var unfiltered = ShotSimulator.Simulate(
                unfilteredBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1,
                projectileContactRadius: 0f, workingSet: unfilteredWorkingSet, allowedColors: allowedColors);

            Assert.AreEqual(10 * allowedColors.Length, unfiltered.RawScore, "an unfiltered rainbow pays every allowed colour");

            // A target-colour filter that ISN'T even one of the rainbow's allowed colours still
            // counts (a rainbow always counts under any filter — it only narrows payColors to 1).
            var filteredBoard = new[]
                { ShotBoardBuilder.Rainbow(new Vector2(0f, 1f), 0.1f, GamePalette.RainbowColorId, 10, 1) };
            var filteredWorkingSet = new ShotBalloonState[filteredBoard.Length];
            var filtered = ShotSimulator.Simulate(
                filteredBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1,
                projectileContactRadius: 0f, workingSet: filteredWorkingSet, allowedColors: allowedColors,
                targetColorId: "Purple");

            Assert.AreEqual(10, filtered.RawScore, "a filter narrows payColors to 1 but never zeroes a rainbow's count");
        }

        [Test]
        public void ResolvePopScore_ColouredProjectileHitsRainbow_KeepsItsOwnColour_NotStolenByTheRainbowMarker()
        {
            // The adoption deviation is the crux of a wildcard pop (ProjectileHitResolver.cs:140-152,
            // ApplyColorChange's isWildcardPop skip): a rainbow balloon must NOT overwrite the
            // projectile's own colour with the reserved marker. With only one allowed colour (as the
            // other tests use), ContainsColor's fallback-to-allowedColors[0] would coincidentally
            // reproduce the correct primary even if adoption wrongly ran — so this needs a SECOND
            // allowed colour to actually discriminate: if the rainbow pop corrupted ProjectileColor to
            // the marker, ContainsColor(allowedColors, marker) fails, RecordColor falls back to
            // "Red" (allowedColors[0]) instead of "Blue", resetting the streak (1) instead of
            // extending it (2) — and the trailing Blue pop would then also see a stale "Red"
            // StreakColor and reset again (1) instead of climbing to 3.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Blue", 1, 1),
                ShotBoardBuilder.Rainbow(new Vector2(0f, 2f), 0.1f, GamePalette.RainbowColorId, 10, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Blue", 100, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, allowedColors: new[] { "Red", "Blue" });

            // Blue(1*1) + rainbow paying both allowed colours at the extended streak(10*2*2) +
            // Blue continuing the SAME streak(100*3) — 1 + 40 + 300. A corrupted ProjectileColor
            // would instead score 1 + 20 + 100 (two broken streaks along the way).
            Assert.AreEqual(1 + 40 + 300, result.RawScore);
            Assert.AreEqual(3, result.Pops);
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ResolvePopScore_Chain_WashThenColorlessRainbowDefers_ThenFoldsIntoTheNextRealColourPop()
        {
            // Distinct from RainbowPopsDefer_ThenFold (projectile is colourless because nothing has
            // popped yet) and from the Wash test (its chain ends at the washer's OWN tough pop,
            // never reaching a real-colour fold): here a soap wash colourlesses an ALREADY-colour
            // projectile, the very next balloon is a rainbow (defers instead of anchoring), and only
            // THEN does a real colour arrive to fold the bank in — the scenario the design memo calls
            // out by name.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 2, 1),
                ShotBoardBuilder.Tough(new Vector2(0f, 2f), 0.1f, 5, 1, washes: true),
                ShotBoardBuilder.Rainbow(new Vector2(0f, 3f), 0.1f, GamePalette.RainbowColorId, 3, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 4f), 0.1f, "Blue", 7, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, allowedColors: new[] { "Red" });

            // Red(2*1) + washer's tough pop(5, ALSO clears the streak/deferred bank) + rainbow
            // defers colourless(3*1) + Blue folds the one banked defer into a fresh streak(7*2).
            Assert.AreEqual(2 + 5 + 3 + 14, result.RawScore);
            Assert.AreEqual(4, result.Pops);
            Assert.AreEqual(1, result.ToughsCleared, "only the washer's own pop is a tough pop");
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ResolveBalloonContact_Wash_FiresOnAMereDeflectContact_ThenTheNextRainbowDefers()
        {
            // Ordering pin (memo: wash -> adopt -> record -> refund; ProjectileHitResolver.cs:154,
            // 169-176): pop a real "Red" first (adopts), then dead-centre deflect off a washing
            // 2-hit tough (ProjectileHitResolver.cs:193-201's wash fires on the DEFLECT outcome, not
            // just a pop) — reused from ShotSimulatorTests.Simulate_ToughTwoTouch's bounce-back trick
            // (bottom wall close, dead-on-centre hit reflects straight back). If wash didn't fire on
            // the deflect, the rainbow below would anchor on the still-adopted "Red" (mirrors "Red")
            // instead of deferring — scoring 6 instead of 3 — and the washer's final (tough) pop
            // would leave a stale deferred bank instead of a fresh Reset.
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 2, 1),
                ShotBoardBuilder.Tough(new Vector2(0f, 5f), 0.3f, 7, 2, washes: true),
                ShotBoardBuilder.Rainbow(new Vector2(0f, -0.5f), 0.1f, GamePalette.RainbowColorId, 3, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, allowedColors: new[] { "Red" });

            // green(2*1) + rainbow-deferred(3*1, NOT 3*2) + washer's tough pop(7)
            Assert.AreEqual(2 + 3 + 7, result.RawScore);
            Assert.AreEqual(3, result.Pops);
            Assert.AreEqual(1, result.ToughsCleared, "only the washer's final (colourless) pop is a tough pop");
            Assert.IsTrue(result.BoardCleared);
            Assert.IsFalse(result.Died);
        }

        [Test]
        public void HandleWallBounce_RainbowBuff_EndsOnTheFirstShieldLossWall()
        {
            // Buff active from the flight's very first event but the FIRST event here is a wall
            // bounce (close right wall) with no balloon contact at all — proving the buff is gone by
            // the time the shot reaches the two balloons beyond it: a same-colour-agnostic climb
            // (buff still on) would score 2*1 + 3*2 = 8; the correct (buff-ended) ordinary streak
            // scores 2*1 + 3*1 = 5, since a DIFFERENT colour resets the streak.
            var walls = new Vector4(1000f, 0.5f, -1000f, -1000f);
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(-1f, 0f), 0.1f, "Red", 2, 1),
                ShotBoardBuilder.Green(new Vector2(-2f, 0f), 0.1f, "Blue", 3, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, seed: ShotFlightSeed.WithRainbowBuff(untilWall: true));

            Assert.AreEqual((2 * 1) + (3 * 1), result.RawScore, "the buff ended at the wall — the colour change resets the streak");
            Assert.AreEqual(2, result.Pops);
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ResolvePopScore_RefundGate_FiresOnABuffedPop_WhenAStreakWasAlreadyEstablished()
        {
            // The refund gate (ColorId non-empty && streak >= 2 && StreakColor == ProjectileColor)
            // is unconditional on HasRainbowBuff — but RecordWildcard (branch 1) never touches
            // StreakColor, so a streak has to already be in progress (a grant landing mid-streak;
            // Phase C) before a buffed pop can ever satisfy it. Seeds that pre-existing streak
            // directly since D-core has no item layer to grant one. No shields to spare (0): the
            // refund must cover the (close) top-wall bounce, or the shot dies there instead of
            // reaching the return-path balloon that clears the board (mirrors
            // ShotSimulatorTests.Simulate_SameColorStreakOfTwo_RefundsShieldAndSurvivesBounce).
            var walls = new Vector4(3f, 1000f, -1000f, -1000f);

            var refundingBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, -0.5f), 0.1f, "Green", 1, 1),
            };
            var refundingWorkingSet = new ShotBalloonState[refundingBoard.Length];
            var refunding = ShotSimulator.Simulate(
                refundingBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: refundingWorkingSet,
                seed: ShotFlightSeed.WithRainbowBuff(
                    untilWall: true, projectileColor: "Red", streakColor: "Red", streakCount: 1));

            Assert.IsFalse(refunding.Died, "the pre-established streak's refund covers the top-wall bounce");
            Assert.IsTrue(refunding.BoardCleared);

            // The refund earned on the FIRST pop still lets the wall bounce spend (and end) the buff —
            // ShieldLostMessage/HasRainbowBuff=false fire on every wall decrement regardless of a same-
            // step refund (mirrors WallBounceEndCondition). Red(1*2, buffed) then the wall — if the buff
            // wrongly survived the refunded bounce, the trailing Green pop would keep climbing the same
            // (colour-agnostic) streak to 1*3 = 3; ended correctly, a real colour change resets it to
            // 1*1 = 1, for a total of 3 rather than 5.
            Assert.AreEqual((1 * 2) + (1 * 1), refunding.RawScore, "the buff must end at the wall even though that same bounce was refund-covered");

            var nonRefundingBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, -0.5f), 0.1f, "Green", 1, 1),
            };
            var nonRefundingWorkingSet = new ShotBalloonState[nonRefundingBoard.Length];
            var nonRefunding = ShotSimulator.Simulate(
                nonRefundingBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: nonRefundingWorkingSet,
                seed: ShotFlightSeed.WithRainbowBuff(untilWall: true, projectileColor: "Red", streakColor: "Red"));

            Assert.IsTrue(nonRefunding.Died, "without an already-established streak of two, no refund covers the bounce");
        }

        [Test]
        public void BalloonModel_ResolveRainbowAttribution_PaysEveryAllowedColourAtFullScoreValue()
        {
            // Live payout anchor (mirrors the pre-cap PRODUCT only — never ILevelProgress.ClaimProgress's
            // cap, which the sim doesn't model): every allowed colour scores the balloon's full
            // ScoreValue, and the primary (streak-anchor) entry is the projectile's own colour when
            // the level still allows it.
            var allowedColors = new[] { "Red", "Blue", "Green" };
            var model = new BalloonModel(new BalloonModelConfig(scoreValue: 5, hitsToPop: 1), allowedColors: allowedColors);
            model.HitsRemaining.Value = 0;
            model.Color.Value = GamePalette.RainbowColorId;

            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Normal, "Blue"), Array.Empty<string>(), results);

            Assert.AreEqual(allowedColors.Length, results.Count);
            Assert.AreEqual(5 * allowedColors.Length, results.Sum(r => r.Points), "pre-cap product across every allowed colour");
            Assert.IsTrue(results.All(r => r.Points == 5));

            var primary = results.Single(r => r.IsPrimary);
            Assert.AreEqual("Blue", primary.ColorId, "the primary anchors on the projectile's own colour when it's allowed");
        }

        [Test]
        public void BalloonModel_ResolveRainbowAttribution_FallsBackToFirstAllowedColour_WhenProjectileColourIsntAllowed()
        {
            var allowedColors = new[] { "Red", "Blue" };
            var model = new BalloonModel(new BalloonModelConfig(scoreValue: 4, hitsToPop: 1), allowedColors: allowedColors);
            model.HitsRemaining.Value = 0;
            model.Color.Value = GamePalette.RainbowColorId;

            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Normal, "Purple"), Array.Empty<string>(), results);

            var primary = results.Single(r => r.IsPrimary);
            Assert.AreEqual("Red", primary.ColorId, "falls back to the first allowed colour when the projectile's own isn't allowed");
        }

        [Test]
        public void ResolvePopScore_PaysSourceColorTarget_ExtendsStreakButNeverRefunds_C2ARegressionGuard()
        {
            // C2a regression guard (@ref plan_shot_solver_accuracy Phase C2a): the sim used to score an
            // Unbreakable exactly like an ordinary Tough (flat ScoreValue, streak-breaking) — it must
            // instead pay whatever colour struck it and EXTEND the streak
            // (UnbreakableBalloonModel.ResolveScoreAttribution pays context.SourceColorId with an
            // implicit breaksStreak:false), yet never refund a shield off its OWN pop (the live refund
            // gate requires `balloon is IHasColor`, which Unbreakable never satisfies). This is a plain
            // PROJECTILE CONTACT test — no item layer involved — so it belongs here, not
            // ShotItemEffectTests.
            //
            // Sequence (both boards): Red (streak1, no refund yet — needs streak >= 2) -> the target
            // under test (streak2, pays "Red") -> Red#3 (streak3, pays "Red") -> a far-off filler that's
            // never reached, keeping the working set non-empty through the wall bounces below.
            // RawScore is IDENTICAL in both boards (2*1 + 7*2 + 3*3 = 25) — this test isn't about
            // scoring differing, only about the refund.
            var walls = new Vector4(3.5f, 1000f, -3.5f, -1000f);

            var paysSourceColorBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 2, 1),
                ShotBoardBuilder.Tough(new Vector2(0f, 2f), 0.1f, 7, 1, paysSourceColor: true),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Red", 3, 1),
                ShotBoardBuilder.Green(new Vector2(500f, 500f), 0.1f, "Blue", 1, 1),
            };
            var paysSourceColorWorkingSet = new ShotBalloonState[paysSourceColorBoard.Length];
            var paysSourceColorResult = ShotSimulator.Simulate(
                paysSourceColorBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: paysSourceColorWorkingSet);

            Assert.AreEqual(
                (2 * 1) + (7 * 2) + (3 * 3), paysSourceColorResult.RawScore,
                "pays out on the streak colour, extending it — not a flat/reset tough payout");
            Assert.AreEqual(3, paysSourceColorResult.Pops);

            // Unlike the WildcardStreak-routed-tough precedent (ResolvePopScore_RainbowBuff_
            // ScoresColorAgnosticallyAndConvertsHexNeighbour, above, which stays 0 — a buffed pop never
            // ticks ToughsCleared for ANY colour shape), the plain (non-buffed) PaysSourceColor branch
            // DOES still increment it: ToughsCleared counts "a colourless target popped" as a tally
            // orthogonal to which payout/streak rule scored it (@ref plan_shot_solver_accuracy Phase C2a
            // — the memo calls this out explicitly: "RecordColor(SourceColorId) payout ... ToughsCleared++
            // still").
            Assert.AreEqual(1, paysSourceColorResult.ToughsCleared);
            Assert.IsTrue(paysSourceColorResult.Died);

            // Only Red#3's own pop refunds (shields 0 -> 1) — surviving exactly ONE of the two
            // close-wall bounces before dying on the second: Events == 3 contacts + 2 walls == 5.
            Assert.AreEqual(
                5, paysSourceColorResult.Events, "exactly one shield (from Red#3 alone) survives one bounce, not two");

            // Control: the SAME streak position against an ORDINARY colour-scoring balloon (same
            // ScoreValue) in place of the PaysSourceColor target — its pop ALSO refunds (ColorId is
            // non-empty), stacking a SECOND shield and surviving one bounce further before dying —
            // proving the suppression above is real, not just "no refund ever fires in this setup".
            var ordinaryBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 2, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.1f, "Red", 7, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Red", 3, 1),
                ShotBoardBuilder.Green(new Vector2(500f, 500f), 0.1f, "Blue", 1, 1),
            };
            var ordinaryWorkingSet = new ShotBalloonState[ordinaryBoard.Length];
            var ordinaryResult = ShotSimulator.Simulate(
                ordinaryBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: ordinaryWorkingSet);

            Assert.AreEqual(
                paysSourceColorResult.RawScore, ordinaryResult.RawScore, "identical scoring — only the refund differs");
            Assert.AreEqual(
                6, ordinaryResult.Events, "two shields (both pops refund) survive two bounces, dying on the third");
            Assert.IsTrue(ordinaryResult.Died);
        }
    }
}

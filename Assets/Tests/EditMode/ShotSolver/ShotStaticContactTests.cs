using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>Phase A (@ref plan_shot_solver_accuracy §4 Phase A): pins the interactive-static
    /// contact rules — Absorber ends the flight, a durable-less static (Deflector) deflects forever
    /// and scores nothing, and a Gatekeeper's final-hit pop is scoreless/streak-neutral — plus the
    /// two review-flagged regressions the phase closed: the (0,0) slot landmine and the static
    /// no-nudge fidelity fix.</summary>
    [TestFixture]
    public class ShotStaticContactTests
    {
        // Mirrors ShotSimulatorTests' convention: a box far larger than anything fired in these
        // tests, so only the geometry each test cares about ever produces an event.
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

        [Test]
        public void Simulate_Absorber_TerminatesFlightWithoutPoppingBalloonBehindIt()
        {
            var board = new[]
            {
                ShotBoardBuilder.Static(Vector2Int.zero, new Vector2(0f, 2f), 0.2f, ShotContactKind.Absorb),
                ShotBoardBuilder.Green(new Vector2(0f, 4f), 0.2f, "Red", 5, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.IsTrue(result.Absorbed, "the absorber ends the flight");
            Assert.IsFalse(result.Died, "Absorbed is distinct from Died");
            Assert.AreEqual(0, result.Pops, "the balloon behind the absorber is never reached");
            Assert.AreEqual(0, result.RawScore);
            Assert.IsFalse(result.BoardCleared);

            // Pops==0 alone would also pass if the absorb branch mistakenly fell through to
            // RemoveActive without incrementing Pops — check the working-set entries directly: the
            // Absorber's own slot is untouched, and the swap-compaction RemoveActive would have
            // performed never ran (index 1 still holds the untouched green behind it).
            Assert.AreEqual(
                ShotContactKind.Absorb, workingSet[0].ContactKind, "the absorber's own entry is never removed");
            Assert.AreEqual(
                "Red", workingSet[1].ColorId, "the balloon behind the absorber is never swap-compacted either");
        }

        [Test]
        public void Simulate_Absorber_AfterAColorPop_PreservesTheBankedScoreInsteadOfResettingIt()
        {
            // Task 2 gap: every other absorb test scores 0 because nothing was popped before the
            // absorber — that can't distinguish "absorb preserves RawScore" from "absorb resets it to
            // 0 and 0 just happens to already be the value". Pop a real green first so RawScore is
            // non-zero, then absorb immediately after.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 3, 1),
                ShotBoardBuilder.Static(Vector2Int.zero, new Vector2(0f, 2f), 0.2f, ShotContactKind.Absorb),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.IsTrue(result.Absorbed);
            Assert.AreEqual(1, result.Pops, "only the green before the absorber counts");
            Assert.AreEqual(3, result.RawScore, "the banked score from before the absorber survives — absorb doesn't zero it");
        }

        [Test]
        public void Simulate_Absorber_MidCruiseStillEndsFlightScorelessly()
        {
            // A shallow-angle shot ping-pongs between the vertical walls while drifting up +y each
            // pass (only vertical walls are in range, so the y-component of direction never flips) —
            // cruise engages on the very first bounce's clear-ahead check (nothing blocks that first
            // pass), and the absorber sits squarely on the SECOND pass, well after that one-time
            // check — so the contact genuinely lands while state.IsCruising is true.
            //
            // Events/Absorbed/RawScore/Pops/Died are driven by GEOMETRY (wall reflections + the
            // absorber's fixed position), never by the cruise flag itself — cruise only changes
            // SPEED, not direction — so none of those alone prove cruise was actually engaged at
            // contact. With speedPerShield=1, CruiseStartShields is pinned to the shield count right
            // after the first bounce (9), so by the third segment (the one ending at the absorber)
            // taps = 9 − 8 = 1 → CurrentSpeed's target = 2× base — provably faster than the same
            // geometry run with cruise disabled, which is what the final assertion below checks.
            var walls = new Vector4(1000f, 5f, -1000f, -5f);
            var board = new[]
            {
                ShotBoardBuilder.Static(Vector2Int.zero, new Vector2(0f, 2f), 0.3f, ShotContactKind.Absorb),
            };
            var workingSet = new ShotBalloonState[board.Length];
            var cruiseConfig = new ShotCruiseConfig(wallBounceThreshold: 1, speedPerShield: 1f);
            var timestampsWithCruise = new List<float>();

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(1f, 0.1f), startingShields: 10, projectileContactRadius: 0f,
                workingSet: workingSet, cruiseConfig: cruiseConfig, timestampsOut: timestampsWithCruise);

            Assert.IsTrue(result.Absorbed, "the absorber ends the flight even while cruising");
            Assert.AreEqual(0, result.RawScore);
            Assert.AreEqual(0, result.Pops);
            Assert.IsFalse(result.Died);
            Assert.AreEqual(3, result.Events, "bounce, bounce, absorb contact — cruise engaged on the first bounce");

            // Same geometry, cruise disabled (a zero threshold never arms it) — every segment runs at
            // base speed, so this is the "no cruise" baseline arrival time at the same contact.
            var baselineWorkingSet = new ShotBalloonState[board.Length];
            var timestampsWithoutCruise = new List<float>();
            ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(1f, 0.1f), startingShields: 10, projectileContactRadius: 0f,
                workingSet: baselineWorkingSet, timestampsOut: timestampsWithoutCruise);

            Assert.Less(
                timestampsWithCruise[^1], timestampsWithoutCruise[^1],
                "the cruise-armed run reaches the absorber strictly faster — proof the speed ramp was genuinely " +
                "applied on the segment ending at the contact, not just a flag nobody reads");
        }

        [Test]
        public void Simulate_Deflector_NeverPopsAndScoresNothing()
        {
            // Same geometry family as ShotSimulatorTests' tough-two-touch mirror test, but
            // int.MaxValue HitsRemaining (no IHasDurability — DeflectorActorModel's shape) means the
            // >1-HP branch NEVER resolves to a pop, however many times the shot returns to it.
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[]
            {
                ShotBoardBuilder.Static(
                    Vector2Int.zero, new Vector2(0f, 2f), 0.3f, ShotContactKind.Poppable, int.MaxValue),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.IsTrue(result.Died, "the deflector never gives, so bounces eventually outrun the shield budget");
            Assert.AreEqual(0, result.Pops);
            Assert.AreEqual(0, result.RawScore);
            Assert.IsFalse(result.BoardCleared);
        }

        [Test]
        public void Simulate_GatekeeperFinalHitPop_ScoresNothingAndLeavesStreakUntouched()
        {
            // A column: two "Red" pops build a streak, a 2-HP gatekeeper deflects once (bounced back
            // by the bottom wall) and pops on its SECOND (final) hit — unbent, so the ray carries on
            // to a third "Red" pop that continues the SAME streak. Proves the gatekeeper's pop is
            // scoreless/streak-neutral, not merely "no score": ColorStreakTracker never even notices it.
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Static(Vector2Int.zero, new Vector2(0f, 3f), 0.2f, ShotContactKind.Poppable, 2),
                ShotBoardBuilder.Green(new Vector2(0f, 4f), 0.1f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.AreEqual(1 + 2 + 3, result.RawScore, "the gatekeeper's pop doesn't reset or skip the running streak");
            Assert.AreEqual(4, result.Pops, "2 greens + the gatekeeper's final hit + the trailing green");
            Assert.AreEqual(0, result.ToughsCleared, "the gatekeeper's pop is neither a tough-rule nor a colour-rule pop");
            Assert.IsTrue(result.BoardCleared);
            Assert.IsFalse(result.Died);
        }

        [Test]
        public void ClassifyContactKind_MatchesEachArchetype()
        {
            Assert.AreEqual(
                ShotContactKind.Absorb, ShotBoardGather.ClassifyContactKind(new AbsorberActorModel()),
                "Absorber always returns HitOutcome.Absorb");
            Assert.AreEqual(
                ShotContactKind.Poppable, ShotBoardGather.ClassifyContactKind(new DeflectorActorModel()),
                "Deflector returns HitOutcome.Deflect — durability (int.MaxValue), not this enum, keeps it deflecting");
            Assert.AreEqual(
                ShotContactKind.Poppable, ShotBoardGather.ClassifyContactKind(new GatekeeperActorModel(2)),
                "Gatekeeper returns Deflect while HitsRemaining > 0 — a damage-0 probe never pops it");
        }

        [Test]
        public void ClassifyContactKind_GatekeeperProbe_DoesNotMutateHitsRemaining()
        {
            // Pins the gather-time safety assumption the classify probe relies on (@ref
            // plan_shot_solver_accuracy Phase A memo): GatekeeperActorModel.EvaluateHit computes
            // Math.Max(0, HitsRemaining − damage), so a damage-0 probe writes back the SAME value —
            // it must never actually consume a hit. If a future rewrite of EvaluateHit broke that
            // identity (e.g. an unconditional decrement), gather would silently pop every gatekeeper
            // one hit early.
            var gatekeeper = new GatekeeperActorModel(2);

            ShotBoardGather.ClassifyContactKind(gatekeeper);

            Assert.AreEqual(2, gatekeeper.HitsRemaining.Value, "the classification probe must not consume a hit");
        }

        [Test]
        public void ResolveBalloonContact_StaticPopAtNonOriginSlot_LeavesTheGridOccupantAtSlotZeroZeroIntact()
        {
            // Landmine pin (0b review): before Phase A threaded a real SlotIndex through
            // ForStaticContact, every static defaulted its SlotIndex to (0,0) — popping ANY static
            // would call RemoveFromGridAt(default), vacating whatever legitimately occupies (0,0).
            // Proven via its balance-planner symptom: a "filler" balloon sitting directly above (0,0)
            // only ever has somewhere to fall once (0,0) reads empty — so if it moves, (0,0) was
            // wrongly vacated by the gatekeeper's (correctly slotted, elsewhere) pop.
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(4, 4));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            gridConfig.SlotsOffset.Returns(Vector2.zero);
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(0.5f);

            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(-5f, -5f), 0.1f, "Blue", 1, 1,
                    slotIndex: Vector2Int.zero, balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 1f,
                    directBalanceMotion: false, nudgeOverrides: null),
                ShotBoardBuilder.Green(
                    new Vector2(-5f, -4f), 0.1f, "Blue", 1, 1,
                    slotIndex: new Vector2Int(0, 1), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 1f,
                    directBalanceMotion: false, nudgeOverrides: null),
                ShotBoardBuilder.Static(new Vector2Int(2, 3), new Vector2(2f, 0f), 0.2f, ShotContactKind.Poppable, 1),
            };

            // GridBalanceQuery.IsUnbalanced(0,1) is true if EITHER of its two support slots — the
            // straight-down (0,0) or, since row 1 is odd, the shifted-down (1,0) — is empty. A blocker
            // at (1,0) closes that second door, so the filler's stability depends ENTIRELY on (0,0),
            // which is exactly what this test needs to isolate.
            var staticActors = new[]
            {
                new ShotStaticActorSnapshot(new Vector2Int(2, 3)),
                new ShotStaticActorSnapshot(new Vector2Int(1, 0)),
            };
            var dynamics = new ShotBoardDynamics(
                gridConfig, balloonsConfig, board, Array.Empty<ShotDynamicActorSnapshot>(), staticActors);
            var workingSet = new ShotBalloonState[board.Length];

            // Fired level along the gatekeeper's line (x from 0 to 2), far off the two "Blue" fillers
            // at x=-5 — the right wall at x=4 with zero shields ends the flight cleanly right after,
            // without the ray ever doubling back over the filler column.
            var result = ShotSimulator.Simulate(
                board, new Vector4(1000f, 4f, -1000f, -1000f), Vector2.zero, Vector2.right, startingShields: 0,
                projectileContactRadius: 0f, workingSet: workingSet, dynamics: dynamics);

            Assert.AreEqual(1, result.Pops, "only the gatekeeper sits on the ray's line");
            Assert.AreEqual(
                new Vector2Int(0, 1), dynamics.TargetActors[1].SlotIndex.Value,
                "the filler above (0,0) never had anywhere to fall — (0,0) stayed occupied");
        }

        [Test]
        public void ResolveBalloonContact_StaticDeflect_NeverNudgesNeighbours()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(4, 4));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            gridConfig.SlotsOffset.Returns(Vector2.zero);
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(0f);
            balloonsConfig.NudgeDistance.Returns(0.5f);
            balloonsConfig.NudgeDuration.Returns(0.2f);

            var board = new[]
            {
                ShotBoardBuilder.Static(new Vector2Int(2, 2), new Vector2(0f, 2f), 0.3f, ShotContactKind.Poppable, 2),
                ShotBoardBuilder.Green(
                    new Vector2(3f, 3f), 0.1f, "Blue", 1, 1,
                    slotIndex: new Vector2Int(2, 3), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 1f,
                    directBalanceMotion: false, nudgeOverrides: null),
            };
            var staticActors = new[] { new ShotStaticActorSnapshot(new Vector2Int(2, 2)) };
            var dynamics = new ShotBoardDynamics(
                gridConfig, balloonsConfig, board, Array.Empty<ShotDynamicActorSnapshot>(), staticActors);
            var workingSet = new ShotBalloonState[board.Length];

            ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, dynamics: dynamics);

            Assert.AreEqual(
                0, dynamics.TargetActors[1].NudgeImpulses.Count,
                "a static contact never nudges its hex neighbours — live statics have no IHasNudge");
        }

        [Test]
        public void ResolveBalloonContact_OrdinaryBalloonDeflect_DoesNudgeNeighbours()
        {
            // Same neighbour-nudge geometry as the static-deflect pin above, but the (2,2) contact
            // now carries its own BalanceProfile (Actor != null) — proving the null-Actor gating, not
            // some accidental universal removal, is what suppresses the static's nudge.
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(4, 4));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            gridConfig.SlotsOffset.Returns(Vector2.zero);
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(0f);
            balloonsConfig.NudgeDistance.Returns(0.5f);
            balloonsConfig.NudgeDuration.Returns(0.2f);

            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 2f), 0.3f, "Red", 1, 2,
                    slotIndex: new Vector2Int(2, 2), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 1f,
                    directBalanceMotion: false, nudgeOverrides: null),
                ShotBoardBuilder.Green(
                    new Vector2(3f, 3f), 0.1f, "Blue", 1, 1,
                    slotIndex: new Vector2Int(2, 3), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 1f,
                    directBalanceMotion: false, nudgeOverrides: null),
            };
            var dynamics = new ShotBoardDynamics(
                gridConfig, balloonsConfig, board, Array.Empty<ShotDynamicActorSnapshot>(),
                Array.Empty<ShotStaticActorSnapshot>());
            var workingSet = new ShotBalloonState[board.Length];

            ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, dynamics: dynamics);

            Assert.Greater(
                dynamics.TargetActors[1].NudgeImpulses.Count, 0,
                "an ordinary (non-static) deflect DOES nudge its hex neighbours — the static test's zero isn't dead code");
        }
    }
}

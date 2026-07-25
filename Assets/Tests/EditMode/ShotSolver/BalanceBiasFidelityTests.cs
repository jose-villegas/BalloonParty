using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>Phase B (@ref plan_shot_solver_accuracy §3 "Weight-bias sharing", §4 Phase B):
    /// proves the sim's <see cref="ShotSimDynamicActor" /> stub reproduces the live weight system
    /// bit-for-bit, then follows the effect through to the balance planner and a full flight.</summary>
    [TestFixture]
    public class BalanceBiasFidelityTests
    {
        [Test]
        public void WeightBias_ColorDiagonal_LiveModelAndStubActorAreBitIdentical()
        {
            var grid = BuildGrid(5, 6);
            var candidate = new Vector2Int(2, 3);

            var liveSelf = new BalloonModel(new BalloonModelConfig(balanceBias: 2f));
            liveSelf.Color.Value = "Red";
            var stubSelf = new ShotSimDynamicActor
            {
                ColorId = "Red", BiasKind = BalanceBiasKind.ColorDiagonal, BiasValue = 2f,
            };

            Assert.AreEqual(0, liveSelf.WeightBias(grid, candidate), "no same-colour neighbour placed yet");
            Assert.AreEqual(liveSelf.WeightBias(grid, candidate), stubSelf.WeightBias(grid, candidate));

            // (2,1) and (3,2) both sit in candidate (2,3)'s colour-diagonal band (Balance
            // BiasExtensions.CountSameColorDiagonals: rows candidate.y ± 1/± 2) — one a live model,
            // one a stub, proving the formula treats both uniformly as neighbours too.
            var liveNeighbour = new BalloonModel();
            liveNeighbour.Color.Value = "Red";
            grid.Place(liveNeighbour, null, new Vector2Int(2, 1));
            grid.Place(new ShotSimDynamicActor { ColorId = "Red" }, null, new Vector2Int(3, 2));

            var liveWeight = liveSelf.WeightBias(grid, candidate);
            var stubWeight = stubSelf.WeightBias(grid, candidate);
            Assert.AreNotEqual(0, liveWeight, "sanity: the formula engaged once same-colour neighbours exist");
            Assert.AreEqual(liveWeight, stubWeight, "live and stub must run byte-identical formula output");

            // Colourless self short-circuits regardless of the neighbours now present.
            var liveColorless = new BalloonModel(new BalloonModelConfig(balanceBias: 2f));
            var stubColorless = new ShotSimDynamicActor
            {
                ColorId = "", BiasKind = BalanceBiasKind.ColorDiagonal, BiasValue = 2f,
            };
            Assert.AreEqual(0, liveColorless.WeightBias(grid, candidate));
            Assert.AreEqual(0, stubColorless.WeightBias(grid, candidate));

            // Zero-bias guard short-circuits regardless of the neighbours too.
            var liveZeroBias = new BalloonModel(new BalloonModelConfig(balanceBias: 0f));
            liveZeroBias.Color.Value = "Red";
            var stubZeroBias = new ShotSimDynamicActor
            {
                ColorId = "Red", BiasKind = BalanceBiasKind.ColorDiagonal, BiasValue = 0f,
            };
            Assert.AreEqual(0, liveZeroBias.WeightBias(grid, candidate));
            Assert.AreEqual(0, stubZeroBias.WeightBias(grid, candidate));
        }

        [Test]
        public void WeightBias_Line_LiveModelAndStubActorAreBitIdentical()
        {
            var grid = BuildGrid(7, 3);
            var candidate = new Vector2Int(3, 1);

            var liveSelf = new ToughBalloonModel(
                new BalloonModelConfig(typeName: BalloonType.Tough, hitsToPop: 2, balanceBias: 3f));
            var stubSelf = new ShotSimDynamicActor
            {
                BiasKind = BalanceBiasKind.Line, BiasValue = 3f, BiasTypeId = (int)BalloonType.Tough,
            };

            Assert.AreEqual(0, liveSelf.WeightBias(grid, candidate), "no same-type neighbour placed yet");
            Assert.AreEqual(liveSelf.WeightBias(grid, candidate), stubSelf.WeightBias(grid, candidate));

            // (2,1) and (4,1) flank candidate (3,1) on the horizontal hex axis — one a live Tough,
            // one a stub configured with the matching BiasTypeId.
            grid.Place(
                new ToughBalloonModel(new BalloonModelConfig(typeName: BalloonType.Tough, hitsToPop: 2)),
                null, new Vector2Int(2, 1));
            grid.Place(
                new ShotSimDynamicActor { BiasTypeId = (int)BalloonType.Tough }, null, new Vector2Int(4, 1));

            var liveWeight = liveSelf.WeightBias(grid, candidate);
            var stubWeight = stubSelf.WeightBias(grid, candidate);
            Assert.AreNotEqual(0, liveWeight, "sanity: the line formula engaged once neighbours flank the candidate");
            Assert.AreEqual(liveWeight, stubWeight, "live and stub must run byte-identical formula output");

            // Zero-bias guard short-circuits regardless of the flanking neighbours.
            var liveZeroBias = new ToughBalloonModel(
                new BalloonModelConfig(typeName: BalloonType.Tough, hitsToPop: 2, balanceBias: 0f));
            var stubZeroBias = new ShotSimDynamicActor
            {
                BiasKind = BalanceBiasKind.Line, BiasValue = 0f, BiasTypeId = (int)BalloonType.Tough,
            };
            Assert.AreEqual(0, liveZeroBias.WeightBias(grid, candidate));
            Assert.AreEqual(0, stubZeroBias.WeightBias(grid, candidate));
        }

        [Test]
        public void WeightBias_Clump_LiveModelAndStubActorAreBitIdentical()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());

            var grid = BuildGrid(4, 4);
            var candidate = new Vector2Int(2, 2);

            var liveSelf = new BubbleClusterModel(
                new BalloonModelConfig(typeName: BalloonType.BubbleCluster, hitsToPop: 1, balanceBias: -1.5f),
                palette);
            var stubSelf = new ShotSimDynamicActor
            {
                BiasKind = BalanceBiasKind.Clump, BiasValue = -1.5f, BiasTypeId = (int)BalloonType.BubbleCluster,
            };

            Assert.AreEqual(0, liveSelf.WeightBias(grid, candidate), "no same-type occupant on the board yet");
            Assert.AreEqual(liveSelf.WeightBias(grid, candidate), stubSelf.WeightBias(grid, candidate));

            // A same-type stub neighbour makes the nearest-distance term finite and nonzero.
            grid.Place(
                new ShotSimDynamicActor { BiasTypeId = (int)BalloonType.BubbleCluster }, null, new Vector2Int(0, 0));

            var liveWeight = liveSelf.WeightBias(grid, candidate);
            var stubWeight = stubSelf.WeightBias(grid, candidate);
            Assert.AreNotEqual(0, liveWeight, "sanity: the clump formula engaged once a same-type neighbour exists");
            Assert.AreEqual(liveWeight, stubWeight, "live and stub must run byte-identical formula output");

            // Zero-bias guard short-circuits regardless of the neighbour now present (unlike the
            // other two formulas, Clump's guard excludes only exactly-zero, not "negative").
            var liveZeroBias = new BubbleClusterModel(
                new BalloonModelConfig(typeName: BalloonType.BubbleCluster, hitsToPop: 1, balanceBias: 0f), palette);
            var stubZeroBias = new ShotSimDynamicActor
            {
                BiasKind = BalanceBiasKind.Clump, BiasValue = 0f, BiasTypeId = (int)BalloonType.BubbleCluster,
            };
            Assert.AreEqual(0, liveZeroBias.WeightBias(grid, candidate));
            Assert.AreEqual(0, stubZeroBias.WeightBias(grid, candidate));
        }

        [Test]
        public void TryScoreMove_OmnidirectionalStub_UnlocksSideAndDownWithoutAShove()
        {
            var grid = BuildGrid(3, 3);
            var evaluator = new GridBalanceQuery(grid).Evaluator;
            grid.Place(new ShotSimDynamicActor { OmnidirectionalBalance = true }, null, new Vector2Int(1, 1));

            Assert.IsTrue(
                evaluator.TryScoreMove(new Vector2Int(1, 1), new Vector2Int(2, 1), ShoveVector.None, out _),
                "omnidirectional unlocks a side move without a shove");
            Assert.IsTrue(
                evaluator.TryScoreMove(new Vector2Int(1, 1), new Vector2Int(1, 2), ShoveVector.None, out _),
                "omnidirectional unlocks a down move without a shove");
        }

        [Test]
        public void TryScoreMove_NonOmnidirectionalStub_RejectsSideAndDownWithoutAShove()
        {
            var grid = BuildGrid(3, 3);
            var evaluator = new GridBalanceQuery(grid).Evaluator;
            grid.Place(new ShotSimDynamicActor(), null, new Vector2Int(1, 1));

            Assert.IsFalse(evaluator.TryScoreMove(new Vector2Int(1, 1), new Vector2Int(2, 1), ShoveVector.None, out _));
            Assert.IsFalse(evaluator.TryScoreMove(new Vector2Int(1, 1), new Vector2Int(1, 2), ShoveVector.None, out _));
        }

        [Test]
        public void MoveWeightEvaluator_TieBrokenByBias_FlipsWhichNeighborWins()
        {
            // Baseline: (1,1)'s two up-candidates — (1,0) straight and (2,0) shifted — tie on support
            // weight (both empty, row0 base case); the historical `>=` tie-break favours the shifted slot.
            var baselineGrid = BuildGrid(3, 3);
            baselineGrid.Place(new BalloonModel(), null, new Vector2Int(1, 1));
            var baselineEvaluator = new GridBalanceQuery(baselineGrid).Evaluator;

            Assert.AreEqual(
                new Vector2Int(2, 0), baselineEvaluator.BestMove(1, 1, ShoveVector.None),
                "no bias: the shifted slot wins the tie, per the historical >= tie-break");

            // Bias flips it: a same-colour neighbour at (0,1) sits ONLY in candidate (1,0)'s colour-
            // diagonal band (its adjacent-row check excludes the mover's own slot but includes (0,1)),
            // not (2,0)'s — giving (1,0) a strictly higher weight than the shifted slot.
            var biasedGrid = BuildGrid(3, 3);
            var biasedMover = new BalloonModel(new BalloonModelConfig(balanceBias: 5f));
            biasedMover.Color.Value = "Red";
            biasedGrid.Place(biasedMover, null, new Vector2Int(1, 1));
            var sameColorNeighbour = new BalloonModel();
            sameColorNeighbour.Color.Value = "Red";
            biasedGrid.Place(sameColorNeighbour, null, new Vector2Int(0, 1));
            var biasedEvaluator = new GridBalanceQuery(biasedGrid).Evaluator;

            Assert.AreEqual(
                new Vector2Int(1, 0), biasedEvaluator.BestMove(1, 1, ShoveVector.None),
                "bias on the straight-up slot outweighs the shift tie-break, flipping the winner");
        }

        [Test]
        public void Simulate_BalanceBiasFlipsWhichHopTheBalancerTakes_ChangesWhichBalloonEntersTheShotsPath()
        {
            // Minimal rig mirroring the tie-break test above, but through a full dynamic-board flight:
            // the sole mover at (1,1) has two tied up-moves; a same-colour stub neighbour at (0,2) —
            // walled off from ever moving itself by a static blocker at (0,1) — biases only the
            // straight-up candidate (1,0). A vertical shot aimed through (1,0)'s world position hits
            // the mover only if the bias-driven pulse actually sends it there instead of the (2,0)
            // shift the tie-break would otherwise pick.
            var baseline = RunFlight(BalanceBiasKind.None, 0f);
            Assert.AreEqual(0, baseline.Pops, "no bias: shifted (2,0) wins the tie — off the shot's line");
            Assert.IsFalse(baseline.BoardCleared);

            var biased = RunFlight(BalanceBiasKind.ColorDiagonal, 5f);
            Assert.AreEqual(1, biased.Pops, "bias flips the hop to straight-up (1,0) — squarely on the line");
            Assert.IsTrue(biased.BoardCleared);
        }

        // The three tests below close a gap the mirror tests above cannot: live and stub now call the
        // IDENTICAL BalanceBiasExtensions formula, so a bug baked into that shared code (e.g. an off-by-one
        // in a band offset) would make liveWeight == stubWeight regardless — the "nonzero + equal" pattern
        // can't tell a correct shared formula from a wrong one both sides agree on. Each hand-counts its
        // band/axis/distance on a tiny grid and asserts one absolute expected int.
        //
        // They double as the Phase B pre-phase-lock proof (@ref plan_shot_solver_accuracy §4 Phase B):
        // "today's non-IBalloonModel-neighbour = 0" inverted into "an IBalanceBiasSource neighbour now
        // counts". The mirror tests above always place a same-color/same-type LIVE neighbour alongside the
        // stub one, so a live self's count would stay nonzero even if the stub half of the retarget had
        // silently failed. Here the ONLY neighbour present is a bare ShotSimDynamicActor (not IBalloonModel)
        // and the caller is a real live model — proving CountSameColorDiagonals/BestLineCountSameType/
        // NearestSameTypeSqrDistance all actually see it, not just tolerate it being absent.

        [Test]
        public void WeightBias_ColorDiagonal_HandComputedAnchor_LiveModelSeesStubOnlyNeighbor()
        {
            var grid = BuildGrid(5, 6);
            var candidate = new Vector2Int(2, 3);

            var liveSelf = new BalloonModel(new BalloonModelConfig(balanceBias: 2f));
            liveSelf.Color.Value = "Red";

            // (3,2) sits in candidate (2,3)'s ±1-row adjacent-diagonal band (see the mirror test above for
            // the full band derivation) — the sole same-colour occupant, and it is a stub, not a live model.
            grid.Place(new ShotSimDynamicActor { ColorId = "Red" }, null, new Vector2Int(3, 2));

            Assert.AreEqual(
                2, liveSelf.WeightBias(grid, candidate), "RoundToInt(bias 2 * 1 same-colour neighbour) == 2");
        }

        [Test]
        public void WeightBias_Line_HandComputedAnchor_LiveModelSeesStubOnlyNeighbor()
        {
            var grid = BuildGrid(7, 3);
            var candidate = new Vector2Int(3, 1);

            var liveSelf = new ToughBalloonModel(
                new BalloonModelConfig(typeName: BalloonType.Tough, hitsToPop: 2, balanceBias: 3f));

            // (4,1) is the sole hit on the horizontal hex axis's positive walk from candidate (3,1); the
            // negative walk and both diagonal axes are empty, so BestLineCountSameType == 1.
            grid.Place(new ShotSimDynamicActor { BiasTypeId = (int)BalloonType.Tough }, null, new Vector2Int(4, 1));

            Assert.AreEqual(
                3, liveSelf.WeightBias(grid, candidate), "RoundToInt(bias 3 * 1 same-type neighbour) == 3");
        }

        [Test]
        public void WeightBias_Clump_HandComputedAnchor_LiveModelSeesStubOnlyNeighbor()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());

            var grid = BuildGrid(2, 2);
            var candidate = new Vector2Int(0, 0);

            var liveSelf = new BubbleClusterModel(
                new BalloonModelConfig(typeName: BalloonType.BubbleCluster, hitsToPop: 1, balanceBias: -1f),
                palette);

            // HexCoordinates.IndexToWorldPosition (separation (1,1), zero offset) puts (0,0) at (-0.5,0)
            // and (1,0) at (1.5,0) — squared distance 4 — so RoundToInt(-bias(-1) * 4) == 4.
            grid.Place(
                new ShotSimDynamicActor { BiasTypeId = (int)BalloonType.BubbleCluster }, null, new Vector2Int(1, 0));

            Assert.AreEqual(4, liveSelf.WeightBias(grid, candidate));
        }

        [Test]
        public void WeightBias_UnbreakableBalloonModel_StaysZero_AfterTheWeightBiasHoist()
        {
            // Regression pin for the WeightBias hoist (BalloonModelBase.WeightBias went from a hardcoded
            // `return 0` override to `Evaluate(BiasKind, candidate, BiasValue)`, dispatched off the virtual
            // BiasKind/BiasValue properties). UnbreakableBalloonModel overrides neither, so it must still
            // resolve to None/0 — even surrounded by same-type neighbours that WOULD engage Line/Clump for
            // a type that opts in. Belongs beside UnbreakableBalloonModelTests in spirit; kept here per this
            // audit's directory scope (Assets/Tests/EditMode/ShotSolver/).
            var grid = BuildGrid(3, 3);
            var candidate = new Vector2Int(1, 1);
            grid.Place(
                new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)),
                null, new Vector2Int(0, 1));
            grid.Place(
                new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)),
                null, new Vector2Int(2, 1));

            var liveSelf = new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable));

            Assert.AreEqual(0, liveSelf.WeightBias(grid, candidate));
        }

        private static ShotSimulationResult RunFlight(BalanceBiasKind biasKind, float biasValue)
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(3, 3));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            gridConfig.SlotsOffset.Returns(Vector2.zero);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(0.01f);

            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(2.5f, -1f), 0.2f, "Red", 1, 1,
                    slotIndex: new Vector2Int(1, 1), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 100f,
                    directBalanceMotion: false, nudgeOverrides: null, omnidirectional: false,
                    biasKind: biasKind, biasValue: biasValue, biasTypeId: 0),
            };
            var otherDynamicActors = new[]
            {
                new ShotDynamicActorSnapshot(
                    new Vector2Int(0, 2), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 0f,
                    directBalanceMotion: false, omnidirectional: false, colorId: "Red",
                    biasKind: BalanceBiasKind.None, biasValue: 0f, biasTypeId: 0),
            };
            var staticActors = new[] { new ShotStaticActorSnapshot(new Vector2Int(0, 1)) };

            var dynamics = new ShotBoardDynamics(gridConfig, balloonsConfig, board, otherDynamicActors, staticActors);
            var workingSet = new ShotBalloonState[board.Length];

            return ShotSimulator.Simulate(
                board, new Vector4(1000f, 1000f, -1000f, -1000f), new Vector2(1.5f, -10f), Vector2.up,
                startingShields: 1, projectileContactRadius: 0f, workingSet: workingSet, projectileSpeed: 1f,
                dynamics: dynamics);
        }

        private static SlotGrid BuildGrid(int columns, int rows)
        {
            var config = Substitute.For<ISlotGridConfig>();
            config.SlotsSize.Returns(new Vector2Int(columns, rows));
            config.SlotSeparation.Returns(new Vector2(1f, 1f));
            return new SlotGrid(config, new BalancePathHolder());
        }
    }
}

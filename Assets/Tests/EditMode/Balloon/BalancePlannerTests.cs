using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class BalancePlannerTests
    {
        private SlotGrid _grid;
        private GridBalanceQuery _balanceQuery;
        private BalancePlanner _planner;
        private Dictionary<IWriteableDynamicSlotActor, int> _turnSteps;
        private List<BalanceMove> _moves;

        [SetUp]
        public void SetUp()
        {
            _turnSteps = new Dictionary<IWriteableDynamicSlotActor, int>();
            _moves = new List<BalanceMove>();
        }

        [Test]
        public void Plan_SingleUnbalancedActor_MovesToOptimalSlot()
        {
            BuildGrid(3, 2);

            // (2,0) blocks the parity-shifted alternative so (1,0) is the only candidate.
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 0));
            var actor = new BalloonModel();
            _grid.Place(actor, null, new Vector2Int(1, 1));

            _planner.Plan(_turnSteps, _moves);

            Assert.AreEqual(1, _moves.Count);
            Assert.AreSame(actor, _moves[0].Actor);
            Assert.AreEqual(new Vector2Int(1, 1), _moves[0].From);
            Assert.AreEqual(new Vector2Int(1, 0), _moves[0].To);
            Assert.IsTrue(_grid.IsEmpty(1, 1));
            Assert.AreSame(actor, _grid.At(new Vector2Int(1, 0)));
        }

        [Test]
        public void Plan_ContestedSlot_HigherPriorityWins()
        {
            BuildGrid(3, 2);

            // Both actors' only empty up-neighbour is (1,0): (0,0) and (2,0) are blocked.
            _grid.Place(new StaticActorModel(), null, new Vector2Int(0, 0));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 0));

            var lowPriority = new BalloonModel(new BalloonModelConfig(hitsToPop: 1, balancePriority: 0));
            var highPriority = new BalloonModel(new BalloonModelConfig(hitsToPop: 1, balancePriority: 1));
            _grid.Place(lowPriority, null, new Vector2Int(0, 1));
            _grid.Place(highPriority, null, new Vector2Int(1, 1));

            _planner.Plan(_turnSteps, _moves);

            // Only the higher-priority actor takes the contested slot; the loser has nowhere left to go.
            Assert.AreEqual(1, _moves.Count);
            Assert.AreSame(highPriority, _moves[0].Actor);
            Assert.AreEqual(new Vector2Int(1, 0), _moves[0].To);
            Assert.AreSame(lowPriority, _grid.At(new Vector2Int(0, 1)));
        }

        [Test]
        public void Plan_StepCappedActor_StopsAfterBudgetWithinAndAcrossCalls()
        {
            BuildGrid(3, 4);

            // Blockers force a single straight-up path at every level: (2,2), (0,1), (2,0).
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 2));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(0, 1));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 0));

            var actor = new BalloonModel(new BalloonModelConfig(hitsToPop: 1, maxBalanceSteps: 1));
            _grid.Place(actor, null, new Vector2Int(1, 3));

            _planner.Plan(_turnSteps, _moves);

            Assert.AreEqual(1, _moves.Count, "capped at one step within a single Plan call");
            Assert.AreEqual(new Vector2Int(1, 2), _moves[0].To);
            Assert.AreEqual(1, _turnSteps[actor]);

            _moves.Clear();
            _planner.Plan(_turnSteps, _moves);

            Assert.AreEqual(0, _moves.Count, "budget is shared across calls via the same turnSteps dictionary");

            _turnSteps.Clear();
            _planner.Plan(_turnSteps, _moves);

            Assert.AreEqual(1, _moves.Count, "a cleared budget (new turn/pulse) allows another step");
            Assert.AreEqual(new Vector2Int(1, 1), _moves[0].To);
        }

        [Test]
        public void Plan_StaticActor_NeverMoves()
        {
            BuildGrid(3, 2);

            var staticActor = new StaticActorModel();
            _grid.Place(staticActor, null, new Vector2Int(1, 1));

            _planner.Plan(_turnSteps, _moves);

            Assert.AreEqual(0, _moves.Count);
            Assert.AreSame(staticActor, _grid.At(new Vector2Int(1, 1)));
            Assert.IsTrue(_grid.IsEmpty(1, 0));
        }

        [Test]
        public void Plan_MultiPassCascade_UnblockedActorMovesInTheSamePlanCall()
        {
            BuildGrid(3, 3);

            // (2,0) forces actorA's only move to (1,0); (0,1) forces actorB's only move to (1,1) once free.
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 0));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(0, 1));

            var actorA = new BalloonModel();
            var actorB = new BalloonModel();
            _grid.Place(actorA, null, new Vector2Int(1, 1));
            _grid.Place(actorB, null, new Vector2Int(1, 2));

            _planner.Plan(_turnSteps, _moves);

            Assert.AreEqual(2, _moves.Count);
            Assert.AreSame(actorA, _moves[0].Actor);
            Assert.AreEqual(new Vector2Int(1, 0), _moves[0].To);
            Assert.AreSame(actorB, _moves[1].Actor);
            Assert.AreEqual(new Vector2Int(1, 1), _moves[1].To);
            Assert.AreSame(actorA, _grid.At(new Vector2Int(1, 0)));
            Assert.AreSame(actorB, _grid.At(new Vector2Int(1, 1)));
            Assert.IsTrue(_grid.IsEmpty(1, 2));
        }

        [Test]
        public void Plan_UncappedOmnidirectionalActor_TerminatesInsteadOfPingPonging()
        {
            // The fallback-level crash: an uncapped omnidirectional actor (the Unbreakable profile)
            // alone on a sparse board. Every candidate move scores 0 (empty support cones tie with
            // omni side/down moves) and the later-wins tie-break picks down/side hops that re-
            // unbalance it — (0,1) → (1,2) → (0,1) → … forever, growing the move list until OOM.
            // The revisit guard must break the cycle: once a slot has been occupied this Plan, the
            // actor can't take it back.
            BuildGrid(2, 3);

            var roamer = new BalloonModel(new BalloonModelConfig(hitsToPop: 1, omnidirectionalBalance: true));
            _grid.Place(roamer, null, new Vector2Int(0, 1));

            _planner.Plan(_turnSteps, _moves);

            Assert.LessOrEqual(_moves.Count, 3, "a lone actor settles (or stalls) in a hop or two — never loops");
            foreach (var move in _moves)
            {
                Assert.AreSame(roamer, move.Actor);
            }
        }

        private void BuildGrid(int columns, int rows)
        {
            var gameConfig = Substitute.For<IGameConfiguration>();
            gameConfig.SlotsSize.Returns(new Vector2Int(columns, rows));
            var pathHolder = new BalancePathHolder();
            _grid = new SlotGrid(gameConfig, pathHolder);
            _balanceQuery = new GridBalanceQuery(_grid);
            _planner = new BalancePlanner(_grid, _balanceQuery);
        }
    }
}

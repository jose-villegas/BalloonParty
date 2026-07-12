using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class MoveWeightEvaluatorTests
    {
        private SlotGrid _grid;
        private GridBalanceQuery _balanceQuery;
        private MoveWeightEvaluator _evaluator;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<IGameConfiguration>();
            config.SlotsSize.Returns(new Vector2Int(3, 3));
            // Non-zero separation so world-space move directions (and shove dot products) are meaningful.
            config.SlotSeparation.Returns(new Vector2(1f, 1f));
            config.SlotsOffset.Returns(Vector2.zero);

            _grid = new SlotGrid(config, new BalancePathHolder());
            _balanceQuery = new GridBalanceQuery(_grid);
            _evaluator = _balanceQuery.Evaluator;
        }

        [Test]
        public void OptimalBalanceMove_EqualSupport_ShiftedSlotWinsTie()
        {
            PlaceBalloon(1, 1);

            // Odd row shifts +1: (1,0) and (2,0) tie on weight, the parity-shifted slot wins.
            Assert.AreEqual(new Vector2Int(2, 0), _evaluator.OptimalBalanceMove(1, 1));
        }

        [Test]
        public void OptimalBalanceMove_HigherSupport_BeatsTheShiftTieBreak()
        {
            PlaceBalloon(1, 2);
            PlaceBalloon(2, 0);

            // (2,0) sits in the support cone of straight-up (1,1) only, outweighing shifted (0,1).
            Assert.AreEqual(new Vector2Int(1, 1), _evaluator.OptimalBalanceMove(1, 2));
        }

        [Test]
        public void OptimalBalanceMove_NegativeClumpBias_PullsTowardSameType()
        {
            _grid.Place(new BalloonModel(new BalloonModelConfig(separationBias: -1f)), null, new Vector2Int(1, 2));
            PlaceBalloon(2, 2);

            // Both candidates score negative (clump bias), so the start-at-int.MinValue rule matters:
            // straight-up (1,1) is nearer the same-type buddy and wins over shifted (0,1).
            Assert.AreEqual(new Vector2Int(1, 1), _evaluator.OptimalBalanceMove(1, 2));
        }

        [Test]
        public void OptimalBalanceMove_BottomRow_ReturnsNull()
        {
            PlaceBalloon(1, 0);

            Assert.IsNull(_evaluator.OptimalBalanceMove(1, 0));
        }

        [Test]
        public void OptimalNextEmptySlot_DelegatesToTheEvaluator()
        {
            PlaceBalloon(1, 1);
            PlaceBalloon(2, 2);

            Assert.AreEqual(_evaluator.OptimalBalanceMove(1, 1), _balanceQuery.OptimalNextEmptySlot(1, 1));
            Assert.AreEqual(_evaluator.OptimalBalanceMove(2, 2), _balanceQuery.OptimalNextEmptySlot(2, 2));
        }

        [Test]
        public void TryScoreMove_SideOrDownWithoutShove_NotACandidate()
        {
            PlaceBalloon(1, 1);

            Assert.IsFalse(_evaluator.TryScoreMove(new Vector2Int(1, 1), new Vector2Int(2, 1), ShoveVector.None, out _));
            Assert.IsFalse(_evaluator.TryScoreMove(new Vector2Int(1, 1), new Vector2Int(1, 2), ShoveVector.None, out _));
        }

        [Test]
        public void TryScoreMove_UpWithoutShove_IsACandidate()
        {
            PlaceBalloon(1, 1);

            Assert.IsTrue(_evaluator.TryScoreMove(new Vector2Int(1, 1), new Vector2Int(1, 0), ShoveVector.None, out _));
        }

        [Test]
        public void TryScoreMove_OccupiedSlot_NotACandidateEvenAligned()
        {
            PlaceBalloon(0, 1);
            PlaceBalloon(1, 1);
            var shove = new ShoveVector(Vector2.right, new Vector2Int(0, 1));

            Assert.IsFalse(_evaluator.TryScoreMove(new Vector2Int(0, 1), new Vector2Int(1, 1), shove, out _));
        }

        [Test]
        public void BestMove_AlignedSideMove_BeatsEveryUpCandidate()
        {
            PlaceBalloon(0, 1);
            var shove = new ShoveVector(Vector2.right, new Vector2Int(0, 1));

            // The sideways slot (1,1) is fully aligned with the shove; pressure gain dwarfs the up
            // candidates even though those stay valid.
            Assert.AreEqual(new Vector2Int(1, 1), _evaluator.BestMove(0, 1, shove));
        }

        [Test]
        public void TryScoreMove_MoveOpposingTheShove_Rejected()
        {
            PlaceBalloon(1, 1);
            var shove = new ShoveVector(Vector2.right, new Vector2Int(2, 1));

            // (0,1) lies behind the shove direction (dot < 0): never move back into the shover.
            Assert.IsFalse(_evaluator.TryScoreMove(new Vector2Int(1, 1), new Vector2Int(0, 1), shove, out _));
        }

        [Test]
        public void ShoveVector_None_IsInactive_ConstructedIsActive()
        {
            Assert.IsFalse(ShoveVector.None.Active);
            Assert.IsTrue(new ShoveVector(Vector2.up, Vector2Int.zero).Active);
        }

        private void PlaceBalloon(int col, int row)
        {
            _grid.Place(new BalloonModel(), null, new Vector2Int(col, row));
        }
    }
}

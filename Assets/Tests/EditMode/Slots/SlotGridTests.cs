using System;
using System.Linq;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Slots;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class SlotGridTests
    {
        private IGameConfiguration _config;
        private SlotGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IGameConfiguration>();
            _config.SlotsSize.Returns(new Vector2Int(6, 10));
            _config.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            _config.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(_config);
        }

        [Test]
        public void BottomEmptySlotPerColumn_SkipsOccupiedRows()
        {
            PlaceAt(0, 0);
            PlaceAt(0, 1);
            PlaceAt(2, 0);

            var result = _grid.BottomEmptySlotPerColumn().ToList();

            Assert.AreEqual(new Vector2Int(0, 2), result[0]);
            Assert.AreEqual(new Vector2Int(1, 0), result[1]);
            Assert.AreEqual(new Vector2Int(2, 1), result[2]);
        }

        [Test]
        public void GetNeighbors_CornerSlot_OmitsOutOfBoundsNeighbors()
        {
            // Even row shiftedCol = -1, so most neighbors fall out of bounds
            PlaceAt(1, 0);
            PlaceAt(0, 1);

            var neighbors = _grid.GetNeighbors(0, 0);

            Assert.AreEqual(2, neighbors.Count);
        }

        [Test]
        public void GetNeighbors_EvenRow_UsesCorrectDiagonalShift()
        {
            // Even row shiftedCol = col - 1
            PlaceAt(1, 2);
            PlaceAt(3, 2);
            PlaceAt(2, 1);
            PlaceAt(1, 1);
            PlaceAt(2, 3);
            PlaceAt(1, 3);

            var neighbors = _grid.GetNeighbors(2, 2);

            Assert.AreEqual(6, neighbors.Count);
        }

        [Test]
        public void GetNeighbors_OddRow_UsesCorrectDiagonalShift()
        {
            // Odd row shiftedCol = col + 1
            PlaceAt(1, 3);
            PlaceAt(3, 3);
            PlaceAt(2, 2);
            PlaceAt(3, 2);
            PlaceAt(2, 4);
            PlaceAt(3, 4);

            var neighbors = _grid.GetNeighbors(2, 3);

            Assert.AreEqual(6, neighbors.Count);
        }

        [Test]
        public void IndexToWorldPosition_EvenRow_CalculatesCorrectly()
        {
            var pos = _grid.IndexToWorldPosition(new Vector2Int(0, 0));

            Assert.AreEqual(-3.0f, pos.x, 0.001f);
            Assert.AreEqual(4.0f, pos.y, 0.001f);
        }

        [Test]
        public void IndexToWorldPosition_OddRow_AppliesHalfColumnOffset()
        {
            var pos = _grid.IndexToWorldPosition(new Vector2Int(0, 1));

            Assert.AreEqual(-2.0f, pos.x, 0.001f);
            Assert.AreEqual(3.15f, pos.y, 0.001f);
        }

        [Test]
        public void IsEmpty_OutOfBoundsIndices_ReturnsTrueInsteadOfThrowing()
        {
            Assert.IsTrue(_grid.IsEmpty(-1, 0));
            Assert.IsTrue(_grid.IsEmpty(0, -1));
            Assert.IsTrue(_grid.IsEmpty(6, 0));
            Assert.IsTrue(_grid.IsEmpty(0, 10));
        }

        [Test]
        public void IsUnbalanced_BothSupportsPresent_ReturnsFalse()
        {
            PlaceAt(2, 1);
            PlaceAt(1, 1);
            PlaceAt(2, 2);

            Assert.IsFalse(_grid.IsUnbalanced(2, 2));
        }

        [Test]
        public void IsUnbalanced_DirectSupportAbsent_ReturnsTrue()
        {
            PlaceAt(2, 1);

            Assert.IsTrue(_grid.IsUnbalanced(2, 1));
        }

        [Test]
        public void IsUnbalanced_DirectSupportPresent_DiagonalEmpty_ReturnsTrue()
        {
            // Even row shiftedCol = col - 1, so diagonal support slot is empty
            PlaceAt(2, 1);
            PlaceAt(2, 2);

            Assert.IsTrue(_grid.IsUnbalanced(2, 2));
        }

        [Test]
        public void IsUnbalanced_Row0_AlwaysFalse()
        {
            PlaceAt(0, 0);

            Assert.IsFalse(_grid.IsUnbalanced(0, 0));
        }

        [Test]
        public void OptimalNextEmptySlot_BothCandidatesEmpty_EqualWeight_ReturnsDiagonal()
        {
            // >= comparison means the diagonal candidate wins on tie
            var result = _grid.OptimalNextEmptySlot(2, 2);

            Assert.IsNotNull(result);
            Assert.AreEqual(new Vector2Int(1, 1), result.Value);
        }

        [Test]
        public void OptimalNextEmptySlot_BothCandidatesOccupied_ReturnsNull()
        {
            PlaceAt(2, 1);
            PlaceAt(1, 1);

            Assert.IsNull(_grid.OptimalNextEmptySlot(2, 2));
        }

        [Test]
        public void OptimalNextEmptySlot_DiagonalOutOfBounds_ReturnsDirect()
        {
            // Col 0, even row: diagonal candidate is -1, out of bounds
            var result = _grid.OptimalNextEmptySlot(0, 2);

            Assert.IsNotNull(result);
            Assert.AreEqual(new Vector2Int(0, 1), result.Value);
        }

        [Test]
        public void OptimalNextEmptySlot_DirectCandidateHasHigherWeight_ReturnsDirect()
        {
            // Both (2,0) and (3,0) feed into direct candidate's weight tree
            PlaceAt(2, 0);
            PlaceAt(3, 0);

            var result = _grid.OptimalNextEmptySlot(2, 2);

            Assert.IsNotNull(result);
            Assert.AreEqual(new Vector2Int(2, 1), result.Value);
        }

        [Test]
        public void OptimalNextEmptySlot_Row0_ReturnsNull()
        {
            Assert.IsNull(_grid.OptimalNextEmptySlot(2, 0));
        }

        [Test]
        public void Place_IntoOccupiedSlot_Throws()
        {
            var first = CreateModel();
            var second = CreateModel();
            var index = new Vector2Int(1, 1);

            _grid.Place(first, null, index);

            Assert.Throws<InvalidOperationException>(() => _grid.Place(second, null, index));
            Assert.AreSame(first, _grid.At(index));
        }

        private static BalloonModel CreateModel()
        {
            return new BalloonModel();
        }

        private void PlaceAt(int col, int row, BalloonModel model = null)
        {
            model ??= CreateModel();
            _grid.Place(model, null, new Vector2Int(col, row));
        }
    }
}

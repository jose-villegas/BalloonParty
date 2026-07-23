using System;
using System.Linq;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class SlotGridTests
    {
        private ISlotGridConfig _config;
        private SlotGrid _grid;
        private GridBalanceQuery _balanceQuery;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<ISlotGridConfig>();
            _config.SlotsSize.Returns(new Vector2Int(6, 10));
            _config.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            _config.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(_config, new BalancePathHolder());
            _balanceQuery = new GridBalanceQuery(_grid);
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
        public void HexNeighborIndices_EvenRow_ShiftsLeft()
        {
            var indices = HexCoordinates.HexNeighborIndices(2, 2);

            Assert.Contains(new Vector2Int(1, 2), indices);
            Assert.Contains(new Vector2Int(3, 2), indices);
            Assert.Contains(new Vector2Int(2, 1), indices);
            Assert.Contains(new Vector2Int(1, 1), indices);
            Assert.Contains(new Vector2Int(2, 3), indices);
            Assert.Contains(new Vector2Int(1, 3), indices);
        }

        [Test]
        public void HexNeighborIndices_OddRow_ShiftsRight()
        {
            var indices = HexCoordinates.HexNeighborIndices(2, 3);

            Assert.Contains(new Vector2Int(1, 3), indices);
            Assert.Contains(new Vector2Int(3, 3), indices);
            Assert.Contains(new Vector2Int(2, 2), indices);
            Assert.Contains(new Vector2Int(3, 2), indices);
            Assert.Contains(new Vector2Int(2, 4), indices);
            Assert.Contains(new Vector2Int(3, 4), indices);
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
        public void InBounds_InsideGrid_ReturnsTrue()
        {
            Assert.IsTrue(_grid.InBounds(0, 0));
            Assert.IsTrue(_grid.InBounds(_grid.Columns - 1, _grid.Rows - 1));
            Assert.IsTrue(_grid.InBounds(new Vector2Int(2, 1)));
        }

        [Test]
        public void InBounds_OffEachEdge_ReturnsFalse()
        {
            Assert.IsFalse(_grid.InBounds(-1, 0));
            Assert.IsFalse(_grid.InBounds(0, -1));
            Assert.IsFalse(_grid.InBounds(_grid.Columns, 0));
            Assert.IsFalse(_grid.InBounds(0, _grid.Rows));
        }

        [Test]
        public void IsUnbalanced_BothSupportsPresent_ReturnsFalse()
        {
            PlaceAt(2, 1);
            PlaceAt(1, 1);
            PlaceAt(2, 2);

            Assert.IsFalse(_balanceQuery.IsUnbalanced(2, 2));
        }

        [Test]
        public void IsUnbalanced_DirectSupportAbsent_ReturnsTrue()
        {
            PlaceAt(2, 1);

            Assert.IsTrue(_balanceQuery.IsUnbalanced(2, 1));
        }

        [Test]
        public void IsUnbalanced_DirectSupportPresent_DiagonalEmpty_ReturnsTrue()
        {
            // Even row shiftedCol = col - 1, so diagonal support slot is empty
            PlaceAt(2, 1);
            PlaceAt(2, 2);

            Assert.IsTrue(_balanceQuery.IsUnbalanced(2, 2));
        }

        [Test]
        public void IsUnbalanced_Row0_AlwaysFalse()
        {
            PlaceAt(0, 0);

            Assert.IsFalse(_balanceQuery.IsUnbalanced(0, 0));
        }

        [Test]
        public void OptimalNextEmptySlot_BothCandidatesEmpty_EqualWeight_ReturnsDiagonal()
        {
            // >= comparison means the diagonal candidate wins on tie
            var result = _balanceQuery.OptimalNextEmptySlot(2, 2);

            Assert.IsNotNull(result);
            Assert.AreEqual(new Vector2Int(1, 1), result.Value);
        }

        [Test]
        public void OptimalNextEmptySlot_BothCandidatesOccupied_ReturnsNull()
        {
            PlaceAt(2, 1);
            PlaceAt(1, 1);

            Assert.IsNull(_balanceQuery.OptimalNextEmptySlot(2, 2));
        }

        [Test]
        public void OptimalNextEmptySlot_DiagonalOutOfBounds_ReturnsDirect()
        {
            // Col 0, even row: diagonal candidate is -1, out of bounds
            var result = _balanceQuery.OptimalNextEmptySlot(0, 2);

            Assert.IsNotNull(result);
            Assert.AreEqual(new Vector2Int(0, 1), result.Value);
        }

        [Test]
        public void OptimalNextEmptySlot_DirectCandidateHasHigherWeight_ReturnsDirect()
        {
            // Both (2,0) and (3,0) feed into direct candidate's weight tree
            PlaceAt(2, 0);
            PlaceAt(3, 0);

            var result = _balanceQuery.OptimalNextEmptySlot(2, 2);

            Assert.IsNotNull(result);
            Assert.AreEqual(new Vector2Int(2, 1), result.Value);
        }

        [Test]
        public void OptimalNextEmptySlot_Row0_ReturnsNull()
        {
            Assert.IsNull(_balanceQuery.OptimalNextEmptySlot(2, 0));
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

        [Test]
        public void MutationVersion_IncrementsOnPlaceAndRemove()
        {
            // MoveWeightEvaluator's memo persists across calls and only rebuilds when this
            // counter changes; it must bump on every occupancy write or the memo goes stale.
            var index = new Vector2Int(1, 1);
            var before = _grid.MutationVersion;

            _grid.Place(CreateModel(), null, index);
            var afterPlace = _grid.MutationVersion;

            _grid.Remove(index);
            var afterRemove = _grid.MutationVersion;

            Assert.Greater(afterPlace, before);
            Assert.Greater(afterRemove, afterPlace);
        }

        [Test]
        public void IsKind_EmptySlot_ReturnsFalse()
        {
            Assert.IsFalse(_grid.IsKind(0, 0, SlotActorKind.Dynamic));
        }

        [Test]
        public void IsKind_OccupiedMatchingKind_ReturnsTrue()
        {
            // BalloonModel.Kind is always Dynamic
            PlaceAt(1, 1);

            Assert.IsTrue(_grid.IsKind(1, 1, SlotActorKind.Dynamic));
        }

        [Test]
        public void IsKind_OccupiedWrongKind_ReturnsFalse()
        {
            // BalloonModel.Kind is Dynamic, not Static
            PlaceAt(1, 1);

            Assert.IsFalse(_grid.IsKind(1, 1, SlotActorKind.Static));
        }

        [Test]
        public void IsTraversable_EmptySlot_ReturnsTrue()
        {
            Assert.IsTrue(_grid.IsTraversable(2, 2));
        }

        [Test]
        public void IsTraversable_SlotWithPassThroughActor_ReturnsTrue()
        {
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 2));

            Assert.IsTrue(_grid.IsTraversable(2, 2));
        }

        [Test]
        public void IsTraversable_SlotWithNonPassThroughActor_ReturnsFalse()
        {
            // BalloonModel does not implement IPassThrough
            PlaceAt(2, 2);

            Assert.IsFalse(_grid.IsTraversable(2, 2));
        }

        [Test]
        public void ComputePath_VerticalPath_LengthEqualsRowDeltaPlusOne()
        {
            // Source row 4 → target row 0: 5 waypoints
            var path = _grid.ComputePath(new Vector2Int(2, 4), new Vector2Int(2, 0));

            Assert.AreEqual(5, path.Length);
        }

        [Test]
        public void ComputePath_LastWaypointIsTargetWorldPosition()
        {
            var target = new Vector2Int(2, 3);
            var path = _grid.ComputePath(new Vector2Int(2, 6), target);

            Assert.AreEqual(_grid.IndexToWorldPosition(target), path[^1]);
        }

        [Test]
        public void ComputePath_PassThroughActorAtIntermediate_IncludedInPath()
        {
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 2));

            var path = _grid.ComputePath(new Vector2Int(2, 4), new Vector2Int(2, 0));

            Assert.AreEqual(5, path.Length);
            Assert.AreEqual(_grid.IndexToWorldPosition(new Vector2Int(2, 2)), path[2]);
        }

        [Test]
        public void ComputePath_SourceOutsideGridBounds_IncludesOutOfBoundsPosition()
        {
            // Source at row 12 — outside the 10-row grid; path length = 13
            var path = _grid.ComputePath(new Vector2Int(2, 12), new Vector2Int(2, 0));

            Assert.AreEqual(13, path.Length);
        }

        [Test]
        public void ComputePath_SameSourceAndTarget_ReturnsSingleWaypoint()
        {
            var target = new Vector2Int(2, 3);
            var path = _grid.ComputePath(target, target);

            Assert.AreEqual(1, path.Length);
            Assert.AreEqual(_grid.IndexToWorldPosition(target), path[0]);
        }

        [Test]
        public void AllEmptySlots_EmptyGrid_ReturnsAllSlots()
        {
            var slots = _grid.AllEmptySlots();

            Assert.AreEqual(_grid.Columns * _grid.Rows, slots.Count());
        }

        [Test]
        public void AllEmptySlots_PartiallyFilled_ExcludesOccupied()
        {
            PlaceAt(0, 0);
            PlaceAt(3, 5);

            var slots = _grid.AllEmptySlots().ToList();

            Assert.AreEqual(_grid.Columns * _grid.Rows - 2, slots.Count);
            Assert.IsFalse(slots.Contains(new Vector2Int(0, 0)));
            Assert.IsFalse(slots.Contains(new Vector2Int(3, 5)));
        }

        [Test]
        public void AllOccupiedSlots_EmptyGrid_ReturnsNone()
        {
            Assert.IsEmpty(_grid.AllOccupiedSlots());
        }

        [Test]
        public void AllOccupiedSlots_PartiallyFilled_ReturnsExactlyOccupied()
        {
            PlaceAt(0, 0);
            PlaceAt(3, 5);

            var slots = _grid.AllOccupiedSlots().ToList();

            Assert.AreEqual(2, slots.Count);
            CollectionAssert.Contains(slots, new Vector2Int(0, 0));
            CollectionAssert.Contains(slots, new Vector2Int(3, 5));
        }

        [Test]
        public void IsUnbalanced_BalloonAboveStaticActor_ReturnsFalse()
        {
            // Row 1 (odd): needs direct support at (2,0) AND diagonal at (3,0).
            // Static actor provides the direct support — verifies it counts the same as any occupant.
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 0));
            PlaceAt(3, 0);
            PlaceAt(2, 1);

            Assert.IsFalse(_balanceQuery.IsUnbalanced(2, 1));
        }

        [Test]
        public void IsUnbalanced_BalloonAboveStaticActor_DiagonalSupport_ReturnsFalse()
        {
            // Even-row balloon at (2,2): diagonal shiftedCol = 1, row 1.
            // Static provides diagonal support; balloon provides direct.
            _grid.Place(new StaticActorModel(), null, new Vector2Int(1, 1));
            PlaceAt(2, 1);
            PlaceAt(2, 2);

            Assert.IsFalse(_balanceQuery.IsUnbalanced(2, 2));
        }

        [Test]
        public void IsUnbalanced_BalloonSupportedByTwoStaticActors_ReturnsFalse()
        {
            // Even-row balloon at (2,2): direct support (2,1) and diagonal support (1,1) are both static.
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 1));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(1, 1));
            PlaceAt(2, 2);

            Assert.IsFalse(_balanceQuery.IsUnbalanced(2, 2));
        }


        [Test]
        public void IsUnbalanced_OddRow_BalloonBetweenTwoStatics_ReturnsFalse()
        {
            _grid.Place(new StaticActorModel(), null, new Vector2Int(0, 0));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(1, 0));
            PlaceAt(0, 1);

            Assert.IsFalse(_balanceQuery.IsUnbalanced(0, 1));
        }

        [Test]
        public void IsUnbalanced_OddRow_BalloonAdjacentToStatics_OnlyHalfSupport_ReturnsTrue()
        {
            // (1,1) needs (1,0) and (2,0). (1,0) is static but (2,0) is empty.
            _grid.Place(new StaticActorModel(), null, new Vector2Int(0, 0));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(1, 0));
            PlaceAt(1, 1);

            Assert.IsTrue(_balanceQuery.IsUnbalanced(1, 1));
        }

        [Test]
        public void IsUnbalanced_OddRow_BalloonBeyondStatics_ReturnsTrue()
        {
            // (2,1) needs (2,0) which is empty — no static reaches it.
            _grid.Place(new StaticActorModel(), null, new Vector2Int(0, 0));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(1, 0));
            PlaceAt(2, 1);

            Assert.IsTrue(_balanceQuery.IsUnbalanced(2, 1));
        }

        [Test]
        public void IsUnbalanced_Col0_EvenRow_DiagonalOutOfBounds_OnlyDirectSupportNeeded()
        {
            // Col 0, even row: shiftedCol = -1 (out of bounds).
            // The diagonal check short-circuits — direct support alone is enough.
            PlaceAt(0, 1);
            PlaceAt(0, 2);

            Assert.IsFalse(_balanceQuery.IsUnbalanced(0, 2));
        }

        [Test]
        public void IsUnbalanced_Col0_OddRow_DiagonalInBounds_BothSupportsRequired()
        {
            // Col 0, odd row: shiftedCol = 1 (in bounds). Both (0,0) and (1,0) must be occupied.
            PlaceAt(0, 0);
            PlaceAt(0, 1);

            Assert.IsTrue(_balanceQuery.IsUnbalanced(0, 1));
        }

        [Test]
        public void IsUnbalanced_LastCol_OddRow_DiagonalOutOfBounds_OnlyDirectSupportNeeded()
        {
            // Col 5 (last), odd row: shiftedCol = 6 (out of bounds, Columns == 6).
            // The diagonal check short-circuits — direct support alone is enough.
            PlaceAt(5, 0);
            PlaceAt(5, 1);

            Assert.IsFalse(_balanceQuery.IsUnbalanced(5, 1));
        }

        [Test]
        public void IsUnbalanced_LastCol_EvenRow_DiagonalInBounds_BothSupportsRequired()
        {
            // Col 5 (last), even row: shiftedCol = 4 (in bounds), so (4,1) is also checked.
            PlaceAt(5, 1);
            PlaceAt(5, 2);

            Assert.IsTrue(_balanceQuery.IsUnbalanced(5, 2));
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

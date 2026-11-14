using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class FormationPlannerTests
    {
        private SlotGrid _grid;
        private IUnbreakableFormationSettings _settings;
        private FormationPlanner _planner;
        private List<IWriteableFormationMember> _candidates;
        private List<FormationAssignment> _assignments;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<IUnbreakableFormationSettings>();
            _settings.MaxMembers.Returns(5);
            _settings.BottomClearance.Returns(2);
            _settings.SpotChoices.Returns(1);
            _planner = new FormationPlanner();
            _candidates = new List<IWriteableFormationMember>();
            _assignments = new List<FormationAssignment>();
        }

        [Test]
        public void Horizontal_LinesUpConsecutiveSlotsOnOneRow()
        {
            BuildGrid(7, 8);
            AddMembers(new Vector2Int(0, 0), new Vector2Int(6, 2), new Vector2Int(3, 5));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            var targets = Targets().OrderBy(t => t.x).ToList();
            Assert.AreEqual(3, targets.Count);
            Assert.IsTrue(targets.All(t => t.y == targets[0].y), "one row");
            Assert.AreEqual(targets[0].x + 1, targets[1].x);
            Assert.AreEqual(targets[1].x + 1, targets[2].x);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Diagonal_EachSlotIsTheDiagonalNeighbourOfTheOneAbove(bool right)
        {
            var shape = right ? FormationShape.DiagonalDownRight : FormationShape.DiagonalDownLeft;
            BuildGrid(7, 8);
            AddMembers(new Vector2Int(0, 0), new Vector2Int(6, 0), new Vector2Int(3, 5), new Vector2Int(1, 4));

            Assert.IsTrue(Plan(shape));

            var targets = Targets().OrderBy(t => t.y).ToList();
            Assert.AreEqual(4, targets.Count);
            for (var i = 1; i < targets.Count; i++)
            {
                Assert.AreEqual(HexCoordinates.DiagonalDown(targets[i - 1], right), targets[i]);
            }
        }

        [Test]
        public void NeverUsesTheBottomClearanceRows()
        {
            BuildGrid(5, 8);

            // A five-long diagonal needs rows 0-4 or 1-5; the clearance keeps it off rows 6 and 7.
            AddMembers(
                new Vector2Int(0, 7), new Vector2Int(1, 7), new Vector2Int(2, 7), new Vector2Int(3, 7),
                new Vector2Int(4, 7));

            Assert.IsTrue(Plan(FormationShape.DiagonalDownRight));

            Assert.IsTrue(Targets().All(t => t.y <= 5));
        }

        [Test]
        public void Horizontal_StaysAboveItsOwnBottomClearance()
        {
            // Four rows kept clear for a line: on eight rows, row 3 is the lowest it may take, however low the
            // members start.
            _settings.HorizontalBottomClearance.Returns(4);
            BuildGrid(5, 8);
            AddMembers(new Vector2Int(0, 7), new Vector2Int(2, 7), new Vector2Int(4, 7));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            Assert.IsTrue(Targets().All(t => t.y == 3), "as low as it may go, the least travel");
        }

        [Test]
        public void Horizontal_TakesAtMostItsOwnMaximum()
        {
            _settings.HorizontalMaxMembers.Returns(3);
            BuildGrid(7, 8);
            AddMembers(new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(4, 0), new Vector2Int(6, 0),
                new Vector2Int(3, 2));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            Assert.AreEqual(3, _assignments.Count);
        }

        [Test]
        public void Diagonal_KeepsTheFullMaximum()
        {
            _settings.HorizontalMaxMembers.Returns(3);
            BuildGrid(7, 8);
            AddMembers(new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(4, 0), new Vector2Int(6, 0),
                new Vector2Int(3, 2));

            Assert.IsTrue(Plan(FormationShape.DiagonalDownRight));

            Assert.AreEqual(5, _assignments.Count);
        }

        [Test]
        public void Snake_FormsAsARowOfAtMostItsMaximum_AboveTheRowClearance()
        {
            _settings.SnakeMaxMembers.Returns(4);
            _settings.HorizontalBottomClearance.Returns(4);
            BuildGrid(7, 8);
            AddMembers(new Vector2Int(0, 7), new Vector2Int(1, 7), new Vector2Int(2, 7), new Vector2Int(4, 7),
                new Vector2Int(6, 7));

            Assert.IsTrue(Plan(FormationShape.Snake));

            var targets = Targets().OrderBy(t => t.x).ToList();
            Assert.AreEqual(4, targets.Count);
            Assert.IsTrue(targets.All(t => t.y == 3), "one row, as low as a line may stand");
            for (var i = 1; i < targets.Count; i++)
            {
                Assert.AreEqual(targets[i - 1].x + 1, targets[i].x, "each member next to the one before");
            }
        }

        [Test]
        public void Diagonal_KeepsTheShorterBottomClearance()
        {
            _settings.HorizontalBottomClearance.Returns(4);
            BuildGrid(5, 8);
            AddMembers(new Vector2Int(0, 7), new Vector2Int(1, 7), new Vector2Int(2, 7));

            Assert.IsTrue(Plan(FormationShape.DiagonalDownRight));

            Assert.AreEqual(5, Targets().Max(t => t.y), "a diagonal may still reach row 5");
        }

        [Test]
        public void SkipsSlotsHeldByOtherBalloons()
        {
            BuildGrid(5, 6);

            // Rows 0, 1 and 3 are full of ordinary balloons, so row 2 is the only row a line fits on.
            FillRow(0);
            FillRow(1);
            FillRow(3);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(2, 4), new Vector2Int(4, 5));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            Assert.IsTrue(Targets().All(t => t.y == 2));
        }

        [Test]
        public void SkipsSlotsHeldByUnbreakablesThatAreNotCandidates()
        {
            BuildGrid(5, 6);

            // Rows 0, 1 and 3 hold another formation's unbreakables, so row 2 is the only row a line fits on.
            FillRowWithOutsiders(0);
            FillRowWithOutsiders(1);
            FillRowWithOutsiders(3);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(2, 4), new Vector2Int(4, 5));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            Assert.IsTrue(Targets().All(t => t.y == 2));
        }

        [Test]
        public void Horizontal_NeverFillsARowOtherUnbreakablesHalfHold()
        {
            // Row 2 is the only row a line fits on, and another formation's four already stand at its west end:
            // three more there would wall the board side to side.
            _settings.HorizontalMaxMembers.Returns(3);
            BuildGrid(7, 6);
            FillRow(0);
            FillRow(1);
            FillRow(3);
            FillWithOutsiders(2, 0, 3);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(2, 4), new Vector2Int(4, 5));

            Assert.IsFalse(Plan(FormationShape.Horizontal));
        }

        [Test]
        public void Horizontal_BesideOtherUnbreakables_LeavesTheRowAGap()
        {
            _settings.HorizontalMaxMembers.Returns(3);
            BuildGrid(7, 6);
            FillRow(0);
            FillRow(1);
            FillRow(3);
            FillWithOutsiders(2, 0, 2);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(2, 4), new Vector2Int(4, 5));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            var row = Enumerable.Range(0, _grid.Columns).Count(col => !_grid.IsEmpty(col, 2)) + Targets().Count;
            Assert.Less(row, _grid.Columns, "the row keeps a slot open");
        }

        [Test]
        public void AMemberStandingOnAFormationSlotKeepsIt()
        {
            BuildGrid(7, 8);
            var first = AddMember(new Vector2Int(1, 3));
            var second = AddMember(new Vector2Int(2, 3));
            AddMember(new Vector2Int(5, 5));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            Assert.AreEqual(new Vector2Int(1, 3), TargetOf(first));
            Assert.AreEqual(new Vector2Int(2, 3), TargetOf(second));
        }

        [Test]
        public void TakesNoMoreThanMaxMembers_TheNearestOnes()
        {
            _settings.MaxMembers.Returns(3);
            BuildGrid(7, 8);
            AddMembers(new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 3));
            var far = AddMember(new Vector2Int(6, 0));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            Assert.AreEqual(3, _assignments.Count);
            Assert.IsFalse(_assignments.Any(a => a.Member == far));
        }

        [Test]
        public void ItemCover_RingsTheItemWithAllSixNeighbours()
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 2));
            AddSixMembers();

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            CollectionAssert.AreEquivalent(HexCoordinates.HexNeighborIndices(3, 2), Targets());
        }

        // Directions (HexCoordinates.Step): 1 and 2 below the item, toward the cannon; 0 and 3 its sides; 4 and 5
        // above it. A cover fills below first, then the sides, then above.
        [Test]
        public void ItemCover_ThreeMembers_TakeTheTwoBelowAndOneSide()
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            AddMembers(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            var targets = Targets();
            Assert.AreEqual(3, targets.Count);
            CollectionAssert.IsSubsetOf(Around(item, 1, 2), targets, "both slots below");
            Assert.AreEqual(1, targets.Intersect(Around(item, 0, 3)).Count(), "one side");
        }

        [Test]
        public void ItemCover_FourMembers_TakeBelowAndBothSides()
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            AddMembers(new Vector2Int(0, 5), new Vector2Int(2, 5), new Vector2Int(4, 5), new Vector2Int(6, 5));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            CollectionAssert.AreEquivalent(Around(item, 1, 2, 0, 3), Targets());
        }

        [Test]
        public void ItemCover_FiveMembers_LeaveOneSlotAboveOpen()
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            AddMembers(new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(4, 5),
                new Vector2Int(5, 5));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            var targets = Targets();
            Assert.AreEqual(5, targets.Count);
            CollectionAssert.IsSubsetOf(Around(item, 1, 2, 0, 3), targets);
            Assert.AreEqual(1, targets.Intersect(Around(item, 4, 5)).Count(), "one above");
        }

        [Test]
        public void ItemCover_WithFewerThanThreeMembers_HasNoSpot()
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 2));
            AddMembers(new Vector2Int(0, 5), new Vector2Int(6, 5));

            Assert.IsFalse(Plan(FormationShape.ItemCover));
        }

        // The edge shields the side it's on, so the cover takes every neighbour still on the board.
        [TestCase(0, 2, 3)]
        [TestCase(3, 0, 4)]
        [TestCase(6, 3, 3)]
        public void ItemCover_AtTheBoardsEdge_TakesTheNeighboursOnTheBoard(int col, int row, int onBoard)
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(col, row);
            PlaceItem(item);
            AddSixMembers();

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            var expected = Around(item, 0, 1, 2, 3, 4, 5).Where(_grid.InBounds).ToList();
            Assert.AreEqual(onBoard, expected.Count);
            CollectionAssert.AreEquivalent(expected, Targets());
        }

        // The odd cover can lean east or west; the spot that moves its members least wins (SpotChoices 1).
        [TestCase(4, 2, 2)]
        [TestCase(2, 2, 4)]
        public void ItemCover_OddCount_TakesTheSideAMemberAlreadyStandsOn(int sideCol, int sideRow, int otherCol)
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            var side = new Vector2Int(sideCol, sideRow);
            AddMembers(side, new Vector2Int(0, 5), new Vector2Int(6, 5));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            CollectionAssert.Contains(Targets(), side);
            CollectionAssert.DoesNotContain(Targets(), new Vector2Int(otherCol, 2));
            Assert.AreEqual(side, TargetOf(_candidates[0]), "the member on the side stays put");
        }

        [Test]
        public void ItemCover_OddCount_WithOneSideTaken_UsesTheOther()
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            var taken = HexCoordinates.Step(item, 0);
            _grid.Place(new BalloonModel(), null, taken);
            AddMembers(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            CollectionAssert.AreEquivalent(Around(item, 1, 2, 3), Targets());
        }

        [Test]
        public void ItemCover_WithABelowSlotTaken_HasNoSpot()
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            _grid.Place(new BalloonModel(), null, HexCoordinates.Step(item, 1));
            AddMembers(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Assert.IsFalse(Plan(FormationShape.ItemCover));
        }

        [Test]
        public void ItemCover_WhoseBelowSlotsFallInTheBottomClearance_HasNoSpot()
        {
            // Eight rows with two clear: row 5 is the lowest a slot may take, so an item on row 5 would have its
            // below slots on row 6, and a cover never skips those for the sides.
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 5));
            AddMembers(new Vector2Int(0, 1), new Vector2Int(3, 1), new Vector2Int(6, 1));

            Assert.IsFalse(Plan(FormationShape.ItemCover));
        }

        [Test]
        public void ItemCover_OnTheBottomRowWithNoClearance_TakesTheSidesAndAbove()
        {
            _settings.BottomClearance.Returns(0);
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 7);
            PlaceItem(item);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(3, 4), new Vector2Int(6, 4));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            var targets = Targets();
            Assert.AreEqual(3, targets.Count);
            Assert.IsTrue(targets.All(_grid.InBounds));
            CollectionAssert.IsSubsetOf(Around(item, 0, 3), targets, "both sides");
            Assert.AreEqual(1, targets.Intersect(Around(item, 4, 5)).Count(), "one above");
        }

        [Test]
        public void ItemCover_WithMoreThanSixCandidates_TakesSixWhateverMaxMembersSays()
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            AddSixMembers();
            AddMember(new Vector2Int(3, 6));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            CollectionAssert.AreEquivalent(Around(item, 0, 1, 2, 3, 4, 5), Targets());
        }

        [Test]
        public void ItemCover_WithFewerCandidatesThanMinMembers_HasNoSpot()
        {
            _settings.MinMembers.Returns(4);
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 2));
            AddMembers(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Assert.IsFalse(Plan(FormationShape.ItemCover));
        }

        [TestCase(ItemType.Bomb, true)]
        [TestCase(ItemType.Snipe, true)]
        [TestCase(ItemType.Lightning, true)]
        [TestCase(ItemType.Laser, false)]
        [TestCase(ItemType.Paint, false)]
        [TestCase(ItemType.Shield, false)]
        public void ItemCover_OnlyShieldsABombASnipeOrALightning(ItemType item, bool covered)
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 2), item);
            AddSixMembers();

            Assert.AreEqual(covered, Plan(FormationShape.ItemCover));
        }

        [Test]
        public void ItemCover_IgnoresTheHorizontalLimits()
        {
            // Row 4 of eight is under a line's four clear rows, and six is past its three.
            _settings.HorizontalBottomClearance.Returns(4);
            _settings.HorizontalMaxMembers.Returns(3);
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 4));
            AddSixMembers();

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            Assert.AreEqual(6, _assignments.Count);
        }

        [Test]
        public void ItemCover_RingIntoTheBottomClearance_HasNoSpot()
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 5));
            AddSixMembers();

            Assert.IsFalse(Plan(FormationShape.ItemCover), "row 6 is in the bottom two");
        }

        [Test]
        public void ItemCover_NeverPushesABalloonOffTheRing()
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 2));
            _grid.Place(new BalloonModel(), null, new Vector2Int(2, 2));
            AddSixMembers();

            Assert.IsFalse(Plan(FormationShape.ItemCover));
        }

        [Test]
        public void ItemCover_WithoutAnItemOnTheBoard_HasNoSpot()
        {
            BuildGrid(7, 8);
            _grid.Place(new BalloonModel(), null, new Vector2Int(3, 1));
            AddMembers(new Vector2Int(0, 5), new Vector2Int(6, 5), new Vector2Int(3, 5));

            Assert.IsFalse(Plan(FormationShape.ItemCover));
            Assert.AreEqual(0, _assignments.Count);
        }

        [Test]
        public void NoSpotWideEnough_ReturnsFalse()
        {
            BuildGrid(2, 8);
            AddMembers(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1));

            Assert.IsFalse(Plan(FormationShape.Horizontal));
        }

        private bool Plan(FormationShape shape)
        {
            return _planner.TryPlan(_grid, shape, _candidates, _settings, _ => 0, _assignments);
        }

        private List<Vector2Int> Targets()
        {
            return _assignments.Select(a => a.Target).ToList();
        }

        private Vector2Int TargetOf(IWriteableFormationMember member)
        {
            return _assignments.Single(a => a.Member == member).Target;
        }

        private void PlaceItem(Vector2Int slot, ItemType item = ItemType.Bomb)
        {
            var host = new BalloonModel();
            host.Item.Value = item;
            _grid.Place(host, null, slot);
        }

        private static List<Vector2Int> Around(Vector2Int slot, params int[] directions)
        {
            return directions.Select(d => HexCoordinates.Step(slot, d)).ToList();
        }

        private void AddSixMembers()
        {
            AddMembers(new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(4, 5),
                new Vector2Int(5, 5), new Vector2Int(6, 5));
        }

        private void AddMembers(params Vector2Int[] slots)
        {
            foreach (var slot in slots)
            {
                AddMember(slot);
            }
        }

        private UnbreakableBalloonModel AddMember(Vector2Int slot)
        {
            var member = new UnbreakableBalloonModel(new BalloonModelConfig());
            _grid.Place(member, null, slot);
            _candidates.Add(member);
            return member;
        }

        private void FillRow(int row)
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                _grid.Place(new BalloonModel(), null, new Vector2Int(col, row));
            }
        }

        private void FillRowWithOutsiders(int row)
        {
            FillWithOutsiders(row, 0, _grid.Columns - 1);
        }

        private void FillWithOutsiders(int row, int fromColumn, int toColumn)
        {
            for (var col = fromColumn; col <= toColumn; col++)
            {
                _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, new Vector2Int(col, row));
            }
        }

        private void BuildGrid(int columns, int rows)
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(columns, rows));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());
        }
    }
}

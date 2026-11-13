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

        [Test]
        public void ItemCover_WithFewerThanSixMembers_HasNoSpot()
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 2));
            AddMembers(new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(4, 5),
                new Vector2Int(5, 5));

            Assert.IsFalse(Plan(FormationShape.ItemCover));
        }

        [TestCase(0, 2)]
        [TestCase(3, 0)]
        [TestCase(6, 3)]
        public void ItemCover_AtTheBoardsEdge_HasNoSpot(int col, int row)
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(col, row));
            AddSixMembers();

            Assert.IsFalse(Plan(FormationShape.ItemCover));
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

        private void PlaceItem(Vector2Int slot)
        {
            var host = new BalloonModel();
            host.Item.Value = ItemType.Bomb;
            _grid.Place(host, null, slot);
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
            for (var col = 0; col < _grid.Columns; col++)
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

using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class FormationSpinnerTests
    {
        private static readonly Vector2Int Centre = new(3, 3);

        private SlotGrid _grid;
        private List<IWriteableFormationMember> _members;
        private List<SlotMove> _moves;

        [SetUp]
        public void SetUp()
        {
            BuildGrid(7, 8);
            _members = new List<IWriteableFormationMember>();
            _moves = new List<SlotMove>();
        }

        [Test]
        public void TryTurn_TheLineTurnsSixtyDegreesAboutItsMiddle_AndTheMiddleNeverMoves(
            [Values(0, 1, 2)] int axis, [Values(1, 2)] int arm, [Values(true, false)] bool clockwise)
        {
            var line = AddLine(Centre, axis, arm);
            var turn = clockwise ? 1 : -1;

            Assert.IsTrue(FormationSpinner.TryTurn(_grid, Centre, _members, clockwise, null, _moves));

            Assert.AreEqual(2 * arm, _moves.Count, "every member but the middle one");
            Assert.IsFalse(_moves.Any(m => ReferenceEquals(m.Actor, line[arm])), "the middle member stays");
            foreach (var move in _moves)
            {
                var (direction, distance) = Locate(move.Actor.SlotIndex.Value);
                Assert.AreEqual(Along(Centre, direction + turn, distance), move.Target, "60 degrees round, same distance");
            }
        }

        [TestCase(1, true)]
        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(2, false)]
        public void TryTurn_TurnedRepeatedly_GoesFlatToOneDiagonalToTheOther_AndBackToItsOwnSlotsAfterSix(
            int arm, bool clockwise)
        {
            AddLine(Centre, 0, arm);
            var turn = clockwise ? 1 : -1;
            var start = _members.ToDictionary(m => m, m => Locate(m.SlotIndex.Value));

            for (var turns = 1; turns <= 6; turns++)
            {
                Assert.IsTrue(FormationSpinner.TryTurn(_grid, Centre, _members, clockwise, null, _moves), "turn " + turns);
                Apply(_moves);

                // After three turns the line is flat again, its two ends swapped; after six every member is home.
                foreach (var member in _members)
                {
                    var (direction, distance) = start[member];
                    Assert.AreEqual(
                        Along(Centre, direction + (turns * turn), distance), member.SlotIndex.Value,
                        "member at distance " + distance + " after " + turns + " turns");
                    Assert.AreSame(member, _grid.At(member.SlotIndex.Value));
                }
            }

            foreach (var member in _members)
            {
                var (direction, distance) = start[member];
                Assert.AreEqual(Along(Centre, direction, distance), member.SlotIndex.Value);
            }
        }

        [TestCase(1, true)]
        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(2, false)]
        public void TryTurn_ABalloonInTheSlotAnArmTurnsInto_GoesToTheSlotThatArmMemberLeft(int arm, bool clockwise)
        {
            AddLine(Centre, 0, arm);
            var turn = clockwise ? 1 : -1;
            var balloon = PlaceBalloon(Along(Centre, turn, arm));

            Assert.IsTrue(FormationSpinner.TryTurn(_grid, Centre, _members, clockwise, null, _moves));

            Assert.AreEqual(2 * arm + 1, _moves.Count);
            Assert.AreEqual(Along(Centre, 0, arm), _moves.Single(m => ReferenceEquals(m.Actor, balloon)).Target);

            Apply(_moves);

            Assert.AreSame(balloon, _grid.At(Along(Centre, 0, arm)));
            Assert.IsTrue(_members.All(m => _grid.At(m.SlotIndex.Value) == m), "every member on its slot");
        }

        [Test]
        public void TryTurn_BalloonsInBothArmsWay_EachTakesTheSlotOfItsOwnMember()
        {
            AddLine(Centre, 0, 2);
            var east = PlaceBalloon(Along(Centre, 1, 1));
            var west = PlaceBalloon(Along(Centre, 4, 1));

            Assert.IsTrue(FormationSpinner.TryTurn(_grid, Centre, _members, true, null, _moves));

            Assert.AreEqual(6, _moves.Count);
            Assert.AreEqual(Along(Centre, 0, 1), _moves.Single(m => ReferenceEquals(m.Actor, east)).Target);
            Assert.AreEqual(Along(Centre, 3, 1), _moves.Single(m => ReferenceEquals(m.Actor, west)).Target);
            Assert.AreEqual(_moves.Count, _moves.Select(m => m.Target).Distinct().Count(), "every slot taken once");
        }

        [Test]
        public void TryTurn_BlockedBySomethingItCannotTradeWith_ReturnsFalseWithNoMoves(
            [Values("unbreakable", "tough", "anchored")] string blocker, [Values(1, 2)] int arm)
        {
            AddLine(Centre, 0, arm);
            _grid.Place(Blocker(blocker), null, Along(Centre, 1, arm));
            _moves.Add(new SlotMove(_members[0], Centre));

            Assert.IsFalse(FormationSpinner.TryTurn(_grid, Centre, _members, true, null, _moves));

            Assert.AreEqual(0, _moves.Count);
            Assert.IsTrue(FormationSpinner.TryTurn(_grid, Centre, _members, false, null, _moves), "the other way is free");
        }

        [Test]
        public void TryTurn_AMemberWouldTurnOffTheBoard_ReturnsFalseWithNoMoves()
        {
            // A centre on the east edge: the member to its south-east turns counter-clockwise into east, off the board.
            var edge = new Vector2Int(6, 2);
            Assert.IsFalse(_grid.InBounds(Along(edge, 0, 1)), "control: east of the edge is off the board");
            AddMember(edge);
            AddMember(Along(edge, 1, 1));

            Assert.IsFalse(FormationSpinner.TryTurn(_grid, edge, _members, false, null, _moves));
            Assert.AreEqual(0, _moves.Count);
            Assert.IsTrue(FormationSpinner.TryTurn(_grid, edge, _members, true, null, _moves), "clockwise stays on the board");
        }

        [Test]
        public void TryTurn_AMemberOnNoneOfTheLines_ReturnsFalseWithNoMoves()
        {
            AddLine(Centre, 0, 1);
            AddMember(new Vector2Int(0, 7));
            _moves.Add(new SlotMove(_members[0], Centre));

            Assert.IsFalse(FormationSpinner.TryTurn(_grid, Centre, _members, true, null, _moves));

            Assert.AreEqual(0, _moves.Count);
        }

        [Test]
        public void TryTurn_AMemberOnALineBeyondTheLongestSpinner_ReturnsFalse()
        {
            AddLine(Centre, 0, 1);
            AddMember(Along(Centre, 0, 3));

            Assert.IsFalse(FormationSpinner.TryTurn(_grid, Centre, _members, true, null, _moves));
            Assert.AreEqual(0, _moves.Count);
        }

        [Test]
        public void TryTurn_ATurnThatWouldCloseAShotWall_ReturnsFalse()
        {
            // Three columns, a diagonal about (1, 3). Counter-clockwise it goes flat across row 3 from the west side
            // to the east side, a wall; clockwise it goes to the other diagonal, which never reaches the west side.
            BuildGrid(3, 8);
            var centre = new Vector2Int(1, 3);
            AddLine(centre, 1, 1);
            var wall = new ShotWall();
            wall.Load(_grid);
            Assert.IsFalse(wall.Spans(), "control: the board is open before the turn");

            Assert.IsFalse(FormationSpinner.TryTurn(_grid, centre, _members, false, wall, _moves));
            Assert.AreEqual(0, _moves.Count);
            Assert.IsFalse(wall.Spans(), "the check leaves the wall as it found it");

            Assert.IsTrue(FormationSpinner.TryTurn(_grid, centre, _members, true, wall, _moves));
            Assert.IsFalse(wall.Spans());
            Assert.IsTrue(FormationSpinner.TryTurn(_grid, centre, _members, false, null, _moves), "a null wall checks nothing");
        }

        [Test]
        public void IsCentre_WithEveryNeighbourWithinTheArmOnTheBoard_IsTrue([Values(1, 2)] int arm)
        {
            Assert.IsTrue(FormationSpinner.IsCentre(_grid, Centre, arm, 7, 7));
        }

        [Test]
        public void IsCentre_ACentreHoldingAMember_IsTrue_ForTheMiddleMemberStandsThere()
        {
            AddMember(Centre);

            Assert.IsTrue(FormationSpinner.IsCentre(_grid, Centre, 1, 7, 7));
        }

        [TestCase(0, 3, 1)]
        [TestCase(6, 3, 1)]
        [TestCase(3, 0, 1)]
        [TestCase(3, 7, 1)]
        [TestCase(-1, 3, 1)]
        [TestCase(1, 3, 2)]
        [TestCase(3, 1, 2)]
        public void IsCentre_WhenASlotWithinTheArmIsOffTheBoard_IsFalse(int col, int row, int arm)
        {
            Assert.IsFalse(FormationSpinner.IsCentre(_grid, new Vector2Int(col, row), arm, 7, 7));
        }

        [Test]
        public void IsCentre_ArmOneAgainstArmTwo_OnTheSameBoard()
        {
            var nearTheEdge = new Vector2Int(1, 3);

            Assert.IsTrue(FormationSpinner.IsCentre(_grid, nearTheEdge, 1, 7, 7), "one step out is the board's first column");
            Assert.IsFalse(FormationSpinner.IsCentre(_grid, nearTheEdge, 2, 7, 7), "two steps out is off the board");
        }

        [Test]
        public void IsCentre_ASlotWithinTheArmDeeperThanTheLastRow_IsFalse()
        {
            var low = new Vector2Int(3, 4);

            Assert.IsFalse(FormationSpinner.IsCentre(_grid, low, 1, 4, 7), "its south neighbours are on row 5");
            Assert.IsTrue(FormationSpinner.IsCentre(_grid, low, 1, 5, 7));
            Assert.IsFalse(FormationSpinner.IsCentre(_grid, low, 2, 5, 7), "two steps south is row 6");
            Assert.IsTrue(FormationSpinner.IsCentre(_grid, low, 2, 6, 7));
        }

        [Test]
        public void IsCentre_ACentreDeeperThanTheLastFlatRow_IsFalse()
        {
            Assert.IsFalse(FormationSpinner.IsCentre(_grid, Centre, 1, 7, 2));
            Assert.IsTrue(FormationSpinner.IsCentre(_grid, Centre, 1, 7, 3));
        }

        [Test]
        public void IsCentre_TheLastFlatRowBoundsOnlyTheCentre_NotTheArmsAbove()
        {
            // The slots a turn reaches may lie below the flat row, up to the last row.
            Assert.IsTrue(FormationSpinner.IsCentre(_grid, Centre, 2, 5, 3));
        }

        [Test]
        public void IsCentre_ACentreOffTheBoard_IsFalse()
        {
            Assert.IsFalse(FormationSpinner.IsCentre(_grid, new Vector2Int(3, -1), 1, 7, 7));
        }

        [Test]
        public void Out_StepsTheDistanceInTheDirection(
            [Values(0, 1, 2, 3, 4, 5)] int direction, [Values(2, 3)] int centreRow)
        {
            var centre = new Vector2Int(3, centreRow);

            Assert.AreEqual(centre, FormationSpinner.Out(centre, direction, 0));
            Assert.AreEqual(HexCoordinates.Step(centre, direction), FormationSpinner.Out(centre, direction, 1));
            Assert.AreEqual(
                HexCoordinates.Step(HexCoordinates.Step(centre, direction), direction),
                FormationSpinner.Out(centre, direction, 2));
        }

        [Test]
        public void TryLocate_OfASlotOut_ReturnsItsDirectionAndDistance(
            [Values(0, 1, 2, 3, 4, 5)] int direction, [Values(1, 2)] int distance, [Values(2, 3)] int centreRow)
        {
            var centre = new Vector2Int(3, centreRow);
            var slot = FormationSpinner.Out(centre, direction, distance);

            Assert.IsTrue(FormationSpinner.TryLocate(centre, slot, 2, out var foundDirection, out var foundDistance));

            Assert.AreEqual(direction, foundDirection);
            Assert.AreEqual(distance, foundDistance);
        }

        [Test]
        public void TryLocate_BeyondTheReach_IsFalse()
        {
            var slot = FormationSpinner.Out(Centre, 0, 2);

            Assert.IsTrue(FormationSpinner.TryLocate(Centre, slot, 2, out _, out _));
            Assert.IsFalse(FormationSpinner.TryLocate(Centre, slot, 1, out _, out _));
        }

        [Test]
        public void TryLocate_TheCentreItself_IsFalse()
        {
            Assert.IsFalse(FormationSpinner.TryLocate(Centre, Centre, 2, out var direction, out var distance));

            Assert.AreEqual(-1, direction);
            Assert.AreEqual(0, distance);
        }

        [Test]
        public void TryLocate_ASlotOnNoLine_IsFalse()
        {
            // East, then south-east of that: two steps off, but not in a straight line from the centre.
            var bent = HexCoordinates.Step(HexCoordinates.Step(Centre, 0), 1);

            Assert.IsFalse(FormationSpinner.TryLocate(Centre, bent, 2, out _, out _));
            Assert.IsFalse(FormationSpinner.TryLocate(Centre, new Vector2Int(0, 7), 2, out _, out _));
        }

        // The slot distance steps from centre in direction, walked a step at a time.
        private static Vector2Int Along(Vector2Int centre, int direction, int distance)
        {
            var slot = centre;
            for (var i = 0; i < distance; i++)
            {
                slot = HexCoordinates.Step(slot, direction);
            }

            return slot;
        }

        // A slot's direction and distance from the middle; (-1, 0) for the middle itself.
        private static (int direction, int distance) Locate(Vector2Int slot)
        {
            if (slot == Centre)
            {
                return (-1, 0);
            }

            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                for (var distance = 1; distance <= 2; distance++)
                {
                    if (Along(Centre, direction, distance) == slot)
                    {
                        return (direction, distance);
                    }
                }
            }

            Assert.Fail("slot " + slot + " is on none of the lines through the centre");
            return (-1, 0);
        }

        private static IWriteableDynamicSlotActor Blocker(string kind)
        {
            switch (kind)
            {
                case "tough":
                    return new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: 3));

                case "anchored":
                    var balloon = new BalloonModel();
                    balloon.SetHeldByCover(true);
                    return balloon;

                default:
                    return new UnbreakableBalloonModel(new BalloonModelConfig());
            }
        }

        private BalloonModel PlaceBalloon(Vector2Int slot)
        {
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, slot);
            return balloon;
        }

        // From one end to the other along the axis: arm members on the far side, the middle one, arm on the near side.
        private List<IWriteableFormationMember> AddLine(Vector2Int centre, int axis, int arm)
        {
            var line = new List<IWriteableFormationMember>();
            for (var k = -arm; k <= arm; k++)
            {
                line.Add(AddMember(k < 0 ? Along(centre, axis + 3, -k) : Along(centre, axis, k)));
            }

            return line;
        }

        private IWriteableFormationMember AddMember(Vector2Int slot)
        {
            var member = new UnbreakableBalloonModel(new BalloonModelConfig());
            _grid.Place(member, null, slot);
            _members.Add(member);
            return member;
        }

        // As the balancer does it: every actor leaves its slot before any lands.
        private void Apply(IReadOnlyList<SlotMove> moves)
        {
            foreach (var move in moves)
            {
                _grid.Remove(move.Actor.SlotIndex.Value);
            }

            foreach (var move in moves)
            {
                _grid.Place(move.Actor, null, move.Target);
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

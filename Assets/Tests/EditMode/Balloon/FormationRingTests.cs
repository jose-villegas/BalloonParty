using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class FormationRingTests
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

        [TestCase(true, new[] { 1, 2, 3 })]
        [TestCase(false, new[] { 5, 0, 1 })]
        public void TryTurn_EveryMemberMovesToTheNextNeighbourRound(bool clockwise, int[] expected)
        {
            AddMembersAround(Centre, 0, 1, 2);

            Assert.IsTrue(FormationRing.TryTurn(_grid, Centre, _members, clockwise, null, _moves));

            Assert.AreEqual(3, _moves.Count);
            for (var i = 0; i < _members.Count; i++)
            {
                Assert.AreSame(_members[i], _moves[i].Actor);
                Assert.AreEqual(HexCoordinates.Step(Centre, expected[i]), _moves[i].Target);
            }
        }

        // The open slot (3) is filled by the arc's head and the tail's slot opens: one step on round the centre.
        [TestCase(true, 4)]
        [TestCase(false, 2)]
        public void TryTurn_FiveMembers_TheOpenSlotMovesOneStep(bool clockwise, int expectedOpen)
        {
            AddMembersAround(Centre, 0, 1, 2, 4, 5);

            Assert.IsTrue(FormationRing.TryTurn(_grid, Centre, _members, clockwise, null, _moves));

            Assert.AreEqual(5, _moves.Count);
            var taken = _moves.Select(m => HexCoordinates.DirectionBetween(Centre, m.Target)).ToList();
            Assert.AreEqual(5, taken.Distinct().Count(), "no two members land on one slot");
            var open = Enumerable.Range(0, HexCoordinates.Directions).Single(d => !taken.Contains(d));
            Assert.AreEqual(expectedOpen, open);
        }

        [Test]
        public void TryTurn_ABalloonInTheSlotTheHeadTurnsInto_GoesToTheSlotTheTailLeaves()
        {
            AddMembersAround(Centre, 0, 1, 2);
            var balloon = PlaceBalloon(HexCoordinates.Step(Centre, 3));

            Assert.IsTrue(FormationRing.TryTurn(_grid, Centre, _members, true, null, _moves));

            Assert.AreEqual(4, _moves.Count);
            var move = _moves.Single(m => ReferenceEquals(m.Actor, balloon));
            Assert.AreEqual(HexCoordinates.Step(Centre, 0), move.Target, "the tail's slot, which the ring leaves");
        }

        [Test]
        public void TryTurn_TwoSeparateArcs_EachBalloonGetsASlotOfItsOwn()
        {
            // Arcs 0-1 and 3-4, turning clockwise: they head into slots 2 and 5, and leave 0 and 3.
            AddMembersAround(Centre, 0, 1, 3, 4);
            var first = PlaceBalloon(HexCoordinates.Step(Centre, 2));
            var second = PlaceBalloon(HexCoordinates.Step(Centre, 5));

            Assert.IsTrue(FormationRing.TryTurn(_grid, Centre, _members, true, null, _moves));

            var firstTarget = _moves.Single(m => ReferenceEquals(m.Actor, first)).Target;
            var secondTarget = _moves.Single(m => ReferenceEquals(m.Actor, second)).Target;
            Assert.AreNotEqual(firstTarget, secondTarget);
            CollectionAssert.AreEquivalent(
                new[] { HexCoordinates.Step(Centre, 0), HexCoordinates.Step(Centre, 3) },
                new[] { firstTarget, secondTarget });
            Assert.AreEqual(_moves.Count, _moves.Select(m => m.Target).Distinct().Count(), "every slot taken once");
        }

        [TestCase("unbreakable")]
        [TestCase("tough")]
        [TestCase("anchored")]
        public void TryTurn_BlockedBySomethingItCannotTradeWith_ReturnsFalseWithNoMoves(string blocker)
        {
            AddMembersAround(Centre, 0, 1, 2);
            _grid.Place(Blocker(blocker), null, HexCoordinates.Step(Centre, 3));

            Assert.IsFalse(FormationRing.TryTurn(_grid, Centre, _members, true, null, _moves));

            Assert.AreEqual(0, _moves.Count);
            Assert.IsTrue(FormationRing.TryTurn(_grid, Centre, _members, false, null, _moves), "the other way is free");
        }

        [Test]
        public void TryTurn_AMemberOffTheBoardEdge_ReturnsFalse()
        {
            // A centre at the board's east edge: the member at 4 would turn clockwise into slot 5, off the board.
            var edge = new Vector2Int(6, 3);
            AddMembersAround(edge, 3, 4);

            Assert.IsFalse(FormationRing.TryTurn(_grid, edge, _members, true, null, _moves));
            Assert.AreEqual(0, _moves.Count);
        }

        [Test]
        public void TryTurn_AMemberNotBesideTheCentre_ReturnsFalseWithNoMoves()
        {
            AddMembersAround(Centre, 0, 1, 2);
            AddMember(new Vector2Int(0, 7));

            Assert.IsFalse(FormationRing.TryTurn(_grid, Centre, _members, true, null, _moves));

            Assert.AreEqual(0, _moves.Count);
        }

        [Test]
        public void TryTurn_ATurnThatWouldCloseAShotWall_ReturnsFalse()
        {
            // Three columns: an unbreakable at (0, 2) holds the west side, and a clockwise turn takes the ring from
            // (0, 3) and (1, 2) to (1, 2) and (2, 2), linking it to the east side. Counter-clockwise it stays short.
            BuildGrid(3, 8);
            var centre = new Vector2Int(1, 3);
            AddMembersAround(centre, 3, 4);
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, new Vector2Int(0, 2));
            var wall = new ShotWall();
            wall.Load(_grid);
            Assert.IsFalse(wall.Spans(), "control: the board is open before the turn");

            Assert.IsFalse(FormationRing.TryTurn(_grid, centre, _members, true, wall, _moves));
            Assert.AreEqual(0, _moves.Count);
            Assert.IsFalse(wall.Spans(), "the check leaves the wall as it found it");

            Assert.IsTrue(FormationRing.TryTurn(_grid, centre, _members, false, wall, _moves));
            Assert.IsTrue(FormationRing.TryTurn(_grid, centre, _members, true, null, _moves), "a null wall checks nothing");
        }

        [Test]
        public void IsCentre_EmptyCentre_WithRoundItOnTheBoard_IsTrue()
        {
            Assert.IsTrue(FormationRing.IsCentre(_grid, Centre, 5));
        }

        [Test]
        public void IsCentre_ACentreHoldingAPlainBalloon_IsTrue()
        {
            PlaceBalloon(Centre);

            Assert.IsTrue(FormationRing.IsCentre(_grid, Centre, 5));
        }

        [TestCase(0, 3)]
        [TestCase(6, 3)]
        [TestCase(3, 0)]
        [TestCase(3, 7)]
        public void IsCentre_AtAnEdge_IsFalse(int col, int row)
        {
            Assert.IsFalse(FormationRing.IsCentre(_grid, new Vector2Int(col, row), 7));
        }

        [Test]
        public void IsCentre_ANeighbourDeeperThanTheLastRow_IsFalse()
        {
            var low = new Vector2Int(3, 5);

            Assert.IsFalse(FormationRing.IsCentre(_grid, low, 5), "its south neighbours are on row 6");
            Assert.IsTrue(FormationRing.IsCentre(_grid, low, 6));
        }

        [Test]
        public void IsCentre_OffTheBoard_IsFalse()
        {
            Assert.IsFalse(FormationRing.IsCentre(_grid, new Vector2Int(-1, 3), 7));
        }

        [TestCase("unbreakable")]
        [TestCase("tough")]
        [TestCase("anchored")]
        public void IsCentre_ACentreHoldingAMemberOrAnAnchoredActor_IsFalse(string occupant)
        {
            _grid.Place(Blocker(occupant), null, Centre);

            Assert.IsFalse(FormationRing.IsCentre(_grid, Centre, 5));
        }

        [TestCase(0, true, 2)]
        [TestCase(0, false, 4)]
        [TestCase(2, true, 4)]
        [TestCase(5, true, 1)]
        [TestCase(1, false, 5)]
        public void Tangent_RunsAlongTheRing_NotOutFromTheCentre(int around, bool clockwise, int expected)
        {
            Assert.AreEqual(expected, FormationRing.Tangent(around, clockwise));
        }

        [Test]
        public void Tangent_MovesAMemberToItsNextNeighbourRound()
        {
            // The east neighbour moving clockwise lands on the south-east one, a south-west step on from it.
            var from = HexCoordinates.Step(Centre, 0);
            var to = HexCoordinates.Step(Centre, 1);

            Assert.AreEqual(to, HexCoordinates.Step(from, FormationRing.Tangent(0, true)));
        }

        [TestCase(2, true, true)]
        [TestCase(0, true, false)]
        public void LeadsAt_AtAnEndOfTheArc_PointsIntoTheOpenSide(int target, bool expectedLeads, bool expectedClockwise)
        {
            var arc = Arc(0, 1, 2);

            var leads = FormationRing.LeadsAt(Centre, arc, HexCoordinates.Step(Centre, target), out var clockwise);

            Assert.AreEqual(expectedLeads, leads);
            Assert.AreEqual(expectedClockwise, clockwise);
        }

        [Test]
        public void LeadsAt_InTheMiddleOfTheArc_DoesNotLead()
        {
            Assert.IsFalse(FormationRing.LeadsAt(Centre, Arc(0, 1, 2), HexCoordinates.Step(Centre, 1), out _));
        }

        [Test]
        public void LeadsAt_AloneInTheRing_DoesNotLead_ForBothSidesAreOpen()
        {
            Assert.IsFalse(FormationRing.LeadsAt(Centre, Arc(0), HexCoordinates.Step(Centre, 0), out _));
        }

        [Test]
        public void LeadsAt_ATargetNotBesideTheCentre_DoesNotLead()
        {
            Assert.IsFalse(FormationRing.LeadsAt(Centre, Arc(0, 1, 2), new Vector2Int(0, 0), out _));
        }

        [TestCase(2, true, true)]
        [TestCase(4, true, false)]
        [TestCase(0, false, false)]
        public void LeadsAt_FiveMembers_OnlyTheTwoEndsBesideTheGapLead(int target, bool expectedLeads, bool expectedClockwise)
        {
            // Slot 3 is the one open slot: the arc runs 4, 5, 0, 1, 2.
            var arc = Arc(4, 5, 0, 1, 2);

            var leads = FormationRing.LeadsAt(Centre, arc, HexCoordinates.Step(Centre, target), out var clockwise);

            Assert.AreEqual(expectedLeads, leads);
            if (expectedLeads)
            {
                Assert.AreEqual(expectedClockwise, clockwise);
            }
        }

        private static List<FormationAssignment> Arc(params int[] directions)
        {
            return directions.Select(d => new FormationAssignment(null, HexCoordinates.Step(Centre, d))).ToList();
        }

        private IWriteableDynamicSlotActor Blocker(string kind)
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

        private void AddMembersAround(Vector2Int centre, params int[] directions)
        {
            foreach (var direction in directions)
            {
                AddMember(HexCoordinates.Step(centre, direction));
            }
        }

        private void AddMember(Vector2Int slot)
        {
            var member = new UnbreakableBalloonModel(new BalloonModelConfig());
            _grid.Place(member, null, slot);
            _members.Add(member);
        }

        private void BuildGrid(int columns, int rows)
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(columns, rows));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());
        }
    }
}

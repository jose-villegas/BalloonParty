using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
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
        public void PrefersRoom_OverSlotsHeldByBalloons()
        {
            BuildGrid(5, 6);

            // Rows 0, 1 and 3 are full of ordinary balloons, so row 2 is the only row with room for a line; row 3,
            // nearer, would cost three balloons moved.
            FillRow(0);
            FillRow(1);
            FillRow(3);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(2, 4), new Vector2Int(4, 5));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            Assert.IsTrue(Targets().All(t => t.y == 2));
        }

        [Test]
        public void OnAFullBoard_PlansOverBalloons()
        {
            BuildGrid(5, 6);
            for (var row = 0; row < 4; row++)
            {
                FillRow(row);
            }

            AddMembers(new Vector2Int(0, 4), new Vector2Int(2, 4), new Vector2Int(4, 5));

            Assert.IsTrue(Plan(FormationShape.Horizontal), "a full board doesn't keep them apart");
            Assert.AreEqual(3, Targets().Count);
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
            // Row 2 is the only row a line fits on (toughs, which a formation never trades out, fill the others), and
            // another formation's four already stand at its west end: three more there would wall the board side to
            // side.
            _settings.HorizontalMaxMembers.Returns(3);
            BuildGrid(7, 6);
            FillRowWithToughs(0);
            FillRowWithToughs(1);
            FillRowWithToughs(3);
            FillWithOutsiders(2, 0, 3);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(2, 4), new Vector2Int(4, 5));

            Assert.IsFalse(Plan(FormationShape.Horizontal));
        }

        [Test]
        public void Horizontal_BesideOtherUnbreakables_LeavesTheRowAGap()
        {
            _settings.HorizontalMaxMembers.Returns(3);
            BuildGrid(7, 6);
            FillRowWithToughs(0);
            FillRowWithToughs(1);
            FillRowWithToughs(3);
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
        public void ItemCover_WithABelowSlotHeldByABalloon_TakesItAll_TheSame()
        {
            // A balloon is no shield: the cover trades it out rather than leave the item open below.
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            _grid.Place(new BalloonModel(), null, HexCoordinates.Step(item, 1));
            AddMembers(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Assert.IsTrue(Plan(FormationShape.ItemCover));
            CollectionAssert.Contains(Targets(), HexCoordinates.Step(item, 1));
        }

        [Test]
        public void ItemCover_WithABelowSlotHeldByAnOutsider_HasNoSpot()
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, HexCoordinates.Step(item, 1));
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
        public void ItemCover_NeverTakesTheRing_FromAnOutsider()
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 2));
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, new Vector2Int(2, 2));
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

        [TestCase(BalloonType.Golden)]
        [TestCase(BalloonType.Heart)]
        [TestCase(BalloonType.Disco)]
        [TestCase(BalloonType.Nuke)]
        public void ItemCover_CoversAGiftBalloon(BalloonType type)
        {
            BuildGrid(7, 8);
            var gift = new Vector2Int(3, 2);
            PlaceGift(gift, type);
            AddSixMembers();

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            CollectionAssert.AreEquivalent(Around(gift, 0, 1, 2, 3, 4, 5), Targets());
            Assert.AreEqual(gift, _planner.CoveredHost);
        }

        // The item carrier and the gift mirror each other across the board's middle column, and so do the members,
        // so the two covers cost the same. With no preference the first spot found wins, the item carrier's;
        // any preference gives it to the gift.
        [TestCase(0f, false)]
        [TestCase(2f, true)]
        public void ItemCover_BetweenAGiftAndAnItemCarrierAtEqualCost_ThePreferenceDecides(
            float preference, bool coversTheGift)
        {
            _settings.GiftCoverPreference.Returns(preference);
            BuildGrid(7, 8);
            var item = new Vector2Int(1, 2);
            var gift = new Vector2Int(5, 2);
            PlaceItem(item);
            PlaceGift(gift);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(3, 4), new Vector2Int(6, 4));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            Assert.AreEqual(coversTheGift ? gift : item, _planner.CoveredHost);
        }

        // The three already stand as a cover round the bomb (no travel); the gift three columns along would take 7
        // slots of travel between them. The preference is for each member: at 2 the gift counts 6 cheaper, which is
        // not enough, and at 3 it counts 9 cheaper, which is.
        [TestCase(2f, false)]
        [TestCase(3f, true)]
        public void ItemCover_AMuchCheaperItemSpot_StillWins_UntilThePreferenceForEachMemberOutweighsIt(
            float preference, bool coversTheGift)
        {
            _settings.GiftCoverPreference.Returns(preference);
            BuildGrid(7, 8);
            var item = new Vector2Int(1, 2);
            var gift = new Vector2Int(4, 2);
            PlaceItem(item);
            PlaceGift(gift);
            AddMembers(HexCoordinates.Step(item, 1), HexCoordinates.Step(item, 2), HexCoordinates.Step(item, 0));

            Assert.IsTrue(Plan(FormationShape.ItemCover));

            Assert.AreEqual(coversTheGift ? gift : item, _planner.CoveredHost);
        }

        // The planner picks at random among its few cheapest spots, and here those are the gift's two (an odd
        // cover leans east or west) and then the item carrier's. The pick must not undo the preference.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ItemCover_WithAGiftsSpotCheapest_ThePickAmongTheFewCheapestStaysOnTheGift(int pick)
        {
            _settings.GiftCoverPreference.Returns(2f);
            _settings.SpotChoices.Returns(3);
            BuildGrid(7, 8);
            var gift = new Vector2Int(5, 2);
            PlaceItem(new Vector2Int(1, 2));
            PlaceGift(gift);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(3, 4), new Vector2Int(6, 4));

            Assert.IsTrue(_planner.TryPlan(
                _grid, FormationShape.ItemCover, _candidates, _settings, choices => Mathf.Min(pick, choices - 1),
                _assignments));

            Assert.AreEqual(gift, _planner.CoveredHost);
        }

        // With no preference set, the pick among the few cheapest is as it always was: any of them, a gift's or not.
        // The board mirrors itself again, so the gift's cheaper spot and the item carrier's cost the same; the
        // gift's is found first and the item carrier's is the second of the few.
        [Test]
        public void ItemCover_WithNoPreference_ThePickAmongTheFewCheapestMayLandOnTheItemCarrier()
        {
            _settings.SpotChoices.Returns(3);
            BuildGrid(7, 8);
            var item = new Vector2Int(5, 2);
            PlaceGift(new Vector2Int(1, 2));
            PlaceItem(item);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(3, 4), new Vector2Int(6, 4));

            Assert.IsTrue(_planner.TryPlan(
                _grid, FormationShape.ItemCover, _candidates, _settings, _ => 1, _assignments));

            Assert.AreEqual(item, _planner.CoveredHost);
        }

        [Test]
        public void ItemCover_ReportsTheSlotOfTheBalloonItStandsRound_AndALineReportsNone()
        {
            BuildGrid(7, 8);
            var item = new Vector2Int(3, 2);
            PlaceItem(item);
            AddMembers(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Assert.IsTrue(Plan(FormationShape.ItemCover));
            Assert.AreEqual(item, _planner.CoveredHost);

            Assert.IsTrue(Plan(FormationShape.Horizontal));
            Assert.IsNull(_planner.CoveredHost, "a line stands round nothing");
        }

        [Test]
        public void ItemCover_WithNoSpot_ReportsNoBalloon_WhateverTheLastPlanCovered()
        {
            BuildGrid(7, 8);
            PlaceItem(new Vector2Int(3, 2));
            AddMembers(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Assert.IsTrue(Plan(FormationShape.ItemCover));

            _candidates.RemoveAt(0);

            Assert.IsFalse(Plan(FormationShape.ItemCover));
            Assert.IsNull(_planner.CoveredHost);
        }

        // One cover at most holds a balloon, so one letting go never frees a balloon another still stands round.
        [Test]
        public void ItemCover_NeverStandsRoundABalloonAnotherCoverHolds()
        {
            BuildGrid(7, 8);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Bomb;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddSixMembers();

            host.SetHeldByCover(true);
            Assert.IsFalse(Plan(FormationShape.ItemCover));

            host.SetHeldByCover(false);
            Assert.IsTrue(Plan(FormationShape.ItemCover), "control: let go, it is a balloon to cover again");
        }

        // A full board, and the nearest row a line may take (3) is all balloons that covers hold, as are rows 0
        // and 1: the line trades out row 2's free balloons, further off, sooner than move a held one.
        [Test]
        public void ALine_NeverPlansOverABalloonACoverHolds()
        {
            BuildGrid(5, 6);
            FillRow(0, heldByACover: true);
            FillRow(1, heldByACover: true);
            FillRow(2);
            FillRow(3, heldByACover: true);
            AddMembers(new Vector2Int(0, 4), new Vector2Int(2, 4), new Vector2Int(4, 5));

            Assert.IsTrue(Plan(FormationShape.Horizontal));

            Assert.IsTrue(Targets().All(t => t.y == 2));
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

        private void PlaceGift(Vector2Int slot, BalloonType type = BalloonType.Golden)
        {
            _grid.Place(Gift(type), null, slot);
        }

        private static IWriteableSlotActor Gift(BalloonType type)
        {
            var config = new BalloonModelConfig(type, scoreValue: 0, hitsToPop: type == BalloonType.Nuke ? 3 : 1);
            return type switch
            {
                BalloonType.Heart => new HeartBalloonModel(config),
                BalloonType.Disco => new DiscoBalloonModel(config),
                BalloonType.Nuke => new NukeBalloonModel(config),
                _ => new GoldenBalloonModel(config),
            };
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

        private void FillRow(int row, bool heldByACover = false)
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                var balloon = new BalloonModel();
                balloon.SetHeldByCover(heldByACover);
                _grid.Place(balloon, null, new Vector2Int(col, row));
            }
        }

        // A tough never walls, and no formation of unbreakables trades it out of a slot.
        private void FillRowWithToughs(int row)
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                _grid.Place(new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: 1)), null,
                    new Vector2Int(col, row));
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

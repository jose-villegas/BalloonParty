using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
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
    public class UnbreakableFormationServiceTests
    {
        private SlotGrid _grid;
        private IUnbreakableFormationSettings _settings;
        private UnbreakableFormationService _service;
        private List<UnbreakableBalloonModel> _unbreakables;
        private int _relocations;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(7, 8));
            gridConfig.SlotSeparation.Returns(new Vector2(0.5f, 0.85f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _settings = Substitute.For<IUnbreakableFormationSettings>();
            _settings.Enabled.Returns(true);
            _settings.MinMembers.Returns(3);
            _settings.MaxMembers.Returns(5);
            _settings.HoldWaves.Returns(3);
            _settings.BottomClearance.Returns(2);
            _settings.SpotChoices.Returns(1);
            _settings.HorizontalWeight.Returns(1f);
            _settings.DiagonalWeight.Returns(1f);
            _settings.ItemCoverWeight.Returns(2f);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.UnbreakableFormation.Returns(_settings);

            _service = new UnbreakableFormationService(_grid, balloonsConfig)
            {
                Random01 = () => 0f,
            };
            _service.Start();

            _unbreakables = new List<UnbreakableBalloonModel>();
            _relocations = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void FewerThanTheMinimum_NeverForm()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4));

            Arrange();

            Assert.AreEqual(0, _service.Members.Count);
            Assert.IsNull(_service.CurrentShape);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        [Test]
        public void ThreeUnbreakables_FormAndAnchor()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(3, _service.Members.Count);
            Assert.IsNotNull(_service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored && u.InFormation.Value));
            Assert.IsTrue(_unbreakables.All(u => _grid.At(u.SlotIndex.Value) == u), "every member is on its slot");
        }

        [Test]
        public void AFormation_HoldsForHoldWaves_ThenChangesToADifferentShape()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            var firstShape = _service.CurrentShape;
            var held = _unbreakables.Select(u => u.SlotIndex.Value).ToList();
            var relocationsWhenFormed = _relocations;

            Arrange();
            Arrange();

            Assert.AreEqual(relocationsWhenFormed, _relocations, "nothing moves while the formation holds");
            CollectionAssert.AreEqual(held, _unbreakables.Select(u => u.SlotIndex.Value).ToList());
            Assert.AreEqual(firstShape, _service.CurrentShape);

            Arrange();

            Assert.IsNotNull(_service.CurrentShape);
            Assert.AreNotEqual(firstShape, _service.CurrentShape, "the next formation is a different shape");
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored));
        }

        [Test]
        public void EveryMember_SharesTheFormationsHeading()
        {
            _settings.DiagonalWeight.Returns(0f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape);
            var expected = (Vector2)(_grid.IndexToWorldPosition(new Vector2Int(1, 0))
                - _grid.IndexToWorldPosition(Vector2Int.zero));
            Assert.IsTrue(_unbreakables.All(u => Vector2.Angle(u.FormationHeading, expected) < 0.01f),
                "a horizontal formation runs along its row");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ADiagonalFormation_HeadsDownTheDiagonal(bool right)
        {
            _settings.HorizontalWeight.Returns(0f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            _service.Random01 = () => right ? 0f : 0.99f;

            Arrange();

            var shape = _service.CurrentShape;
            Assert.AreEqual(right ? FormationShape.DiagonalDownRight : FormationShape.DiagonalDownLeft, shape);
            var heading = _unbreakables[0].FormationHeading;
            var downRight = shape == FormationShape.DiagonalDownRight;
            Assert.AreEqual(downRight, heading.x > 0f, "the heading leans the diagonal's way");
            var down = _grid.IndexToWorldPosition(new Vector2Int(0, 1)).y
                < _grid.IndexToWorldPosition(Vector2Int.zero).y;
            Assert.AreEqual(down, heading.y < 0f, "the heading follows the rows downward");
            Assert.IsTrue(_unbreakables.All(u => u.FormationHeading == heading));
        }

        [Test]
        public void AnItemCover_HeadsUpAtTheItem()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Bomb;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(4, 5),
                new Vector2Int(5, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.IsTrue(_unbreakables.All(u => u.FormationHeading == Vector2.up));
        }

        [Test]
        public void LosingAMemberBelowTheMinimum_BreaksTheFormationAtOnce()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();

            var popped = _unbreakables[0];
            _grid.Remove(popped.SlotIndex.Value);

            Assert.AreEqual(0, _service.Members.Count);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        [Test]
        public void LosingAMemberAboveTheMinimum_KeepsTheRestInFormation()
        {
            AddUnbreakables(
                new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(5, 0));
            Arrange();

            var popped = _unbreakables[0];
            _grid.Remove(popped.SlotIndex.Value);

            Assert.AreEqual(3, _service.Members.Count);
            Assert.IsFalse(popped.IsAnchored);
            Assert.IsTrue(_unbreakables.Skip(1).All(u => u.IsAnchored));
        }

        [Test]
        public void MoreThanMaxMembers_TheRestStayFree()
        {
            _settings.MaxMembers.Returns(3);
            AddUnbreakables(
                new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(5, 0));

            Arrange();

            Assert.AreEqual(3, _service.Members.Count);
            Assert.AreEqual(1, _unbreakables.Count(u => !u.IsAnchored));
        }

        [Test]
        public void ANewcomerDuringAHold_StaysFreeUntilTheNextChange()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            var newcomer = AddUnbreakable(new Vector2Int(6, 0));

            Arrange();

            Assert.IsFalse(newcomer.IsAnchored);
            Assert.AreEqual(3, _service.Members.Count);
        }

        [Test]
        public void SixUnbreakables_FormTwoFormations_OnSeparateSlots()
        {
            _settings.MaxMembers.Returns(3);
            AddSixSpreadOut();

            Arrange();

            Assert.AreEqual(2, _service.Formations.Count);
            Assert.IsTrue(_service.Formations.All(f => f.Members.Count == 3));
            Assert.IsEmpty(_service.Formations[0].Members.Intersect(_service.Formations[1].Members));
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored && _grid.At(u.SlotIndex.Value) == u));
            Assert.AreEqual(6, _unbreakables.Select(u => u.SlotIndex.Value).Distinct().Count());
        }

        [Test]
        public void FreeUnbreakablesPastAFullFormation_FormTheirOwn()
        {
            AddSixSpreadOut();
            AddUnbreakables(new Vector2Int(2, 2), new Vector2Int(4, 2));

            Arrange();

            CollectionAssert.AreEquivalent(new[] { 5, 3 }, _service.Formations.Select(f => f.Members.Count));
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored));
        }

        [Test]
        public void TooFewFreeUnbreakablesForASecondFormation_StayFree()
        {
            AddSixSpreadOut();

            Arrange();

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreEqual(1, _unbreakables.Count(u => !u.IsAnchored));
        }

        [Test]
        public void AReshape_TakesInAFreeUnbreakable_AndItStaysOutOfAnotherFormation()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            var newcomer = AddUnbreakable(new Vector2Int(6, 0));
            Arrange();
            Arrange();

            Arrange();

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreEqual(4, _service.Formations[0].Members.Count);
            Assert.IsTrue(newcomer.IsAnchored);

            AddUnbreakables(new Vector2Int(1, 5), new Vector2Int(5, 5));
            for (var i = 0; i < 3; i++)
            {
                Arrange();
            }

            Assert.IsTrue(_service.Formations.All(f => f.Members.Count >= 3));
            Assert.AreEqual(_service.Members.Count, _service.Members.Distinct().Count(), "no member in two formations");
        }

        [Test]
        public void PiercingOneFormationBelowTheMinimum_LeavesTheOtherStanding()
        {
            _settings.MaxMembers.Returns(3);
            AddSixSpreadOut();
            Arrange();
            var broken = _service.Formations[0];
            var standing = _service.Formations[1];
            var survivors = broken.Members.Skip(1).Cast<UnbreakableBalloonModel>().ToList();

            _grid.Remove(broken.Members[0].SlotIndex.Value);

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreSame(standing, _service.Formations[0]);
            Assert.IsTrue(standing.Members.Cast<UnbreakableBalloonModel>().All(m => m.IsAnchored));
            Assert.IsFalse(survivors.Any(m => m.IsAnchored));
            Assert.AreEqual(3, _service.Members.Count);
        }

        [Test]
        public void AnUntouchedFormation_CallsAReinforcementAtTheNextWaveStart()
        {
            _settings.ReinforcementChance.Returns(0.5f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(0, _service.TakeReinforcementCalls(), "the wave it formed in calls nobody");

            Arrange();

            Assert.AreEqual(1, _service.TakeReinforcementCalls());
            Assert.AreEqual(0, _service.TakeReinforcementCalls(), "a take clears the calls");
        }

        [Test]
        public void EveryUntouchedFormation_RollsOnItsOwn()
        {
            _settings.MaxMembers.Returns(3);
            _settings.ReinforcementChance.Returns(1f);
            AddSixSpreadOut();
            Arrange();

            Arrange();

            Assert.AreEqual(2, _service.TakeReinforcementCalls());
        }

        [Test]
        public void ARollAtOrAboveTheChance_CallsNobody()
        {
            _settings.ReinforcementChance.Returns(0.5f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            _service.Random01 = () => 0.5f;

            Arrange();

            Assert.AreEqual(0, _service.TakeReinforcementCalls());
        }

        [Test]
        public void APiercedFormation_CallsNoMoreReinforcements()
        {
            _settings.ReinforcementChance.Returns(1f);
            AddUnbreakables(
                new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(5, 0));
            Arrange();

            _grid.Remove(_unbreakables[0].SlotIndex.Value);
            Arrange();
            Arrange();
            Arrange();

            Assert.AreEqual(1, _service.Formations.Count, "three members still stand");
            Assert.AreEqual(0, _service.TakeReinforcementCalls());
        }

        [Test]
        public void ResetRun_DropsUntakenCalls()
        {
            _settings.ReinforcementChance.Returns(1f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            Arrange();

            _service.ResetRun(1);

            Assert.AreEqual(0, _service.TakeReinforcementCalls());
        }

        [Test]
        public void ItemCoverOnly_WithAnItemOnTheBoard_CoversIt()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Bomb;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(4, 5),
                new Vector2Int(5, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => HexCoordinates.Distance(u.SlotIndex.Value, new Vector2Int(3, 2)) == 1),
                "all six around the item");
        }

        [Test]
        public void ItemCoverOnly_WithThreeUnbreakables_CoversTheItemFromBelow()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var item = new Vector2Int(3, 2);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Snipe;
            _grid.Place(host, null, item);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored));
            Assert.IsTrue(_unbreakables.All(u => HexCoordinates.Distance(u.SlotIndex.Value, item) == 1));
            var slots = _unbreakables.Select(u => u.SlotIndex.Value).ToList();
            CollectionAssert.Contains(slots, HexCoordinates.Step(item, 1));
            CollectionAssert.Contains(slots, HexCoordinates.Step(item, 2));
        }

        [Test]
        public void ItemCoverOnly_WithAnItemItDoesNotShield_FormsNothing()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Laser;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.IsNull(_service.CurrentShape);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        [Test]
        public void ItemCoverWeighted_WithAnItemItDoesNotShield_PicksAnotherShape()
        {
            var host = new BalloonModel();
            host.Item.Value = ItemType.Laser;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.IsNotNull(_service.CurrentShape);
            Assert.AreNotEqual(FormationShape.ItemCover, _service.CurrentShape);
        }

        [Test]
        public void NoShapeFits_NobodyIsAnchored()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(6, 5), new Vector2Int(5, 0));

            Arrange();

            Assert.IsNull(_service.CurrentShape);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        [Test]
        public void Disabled_NeverForms()
        {
            _settings.Enabled.Returns(false);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(0, _service.Members.Count);
        }

        [Test]
        public void ResetRun_ReleasesTheFormation()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();

            _service.ResetRun(1);

            Assert.AreEqual(0, _service.Members.Count);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        private void AddSixSpreadOut()
        {
            AddUnbreakables(
                new Vector2Int(0, 0), new Vector2Int(6, 0), new Vector2Int(3, 1),
                new Vector2Int(0, 5), new Vector2Int(6, 5), new Vector2Int(3, 4));
        }

        [Test]
        public void ASnake_FormsAsALine_LedFromOneEnd()
        {
            SnakeOnly();
            AddUnbreakables(new Vector2Int(1, 0), new Vector2Int(3, 0), new Vector2Int(5, 0));

            Arrange();

            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape);
            var snake = _service.Formations[0];
            Assert.AreEqual(0, snake.Heading, "Random01 0 sends it east");
            var columns = snake.Members.Select(m => m.SlotIndex.Value.x).ToList();
            CollectionAssert.AreEqual(columns.OrderByDescending(x => x).ToList(), columns, "led by its eastmost");
        }

        [Test]
        public void ASnakeStep_MovesTheHeadOn_AndEachMemberIntoTheSlotAhead()
        {
            var snake = FormSnake();
            var before = snake.Members.Select(m => m.SlotIndex.Value).ToList();

            _service.StepSnakes(Relocate);

            var after = snake.Members.Select(m => m.SlotIndex.Value).ToList();
            Assert.AreEqual(HexCoordinates.Step(before[0], 0), after[0], "the head steps east");
            for (var i = 1; i < after.Count; i++)
            {
                Assert.AreEqual(before[i - 1], after[i], "each member takes the slot the one ahead left");
            }

            Assert.IsTrue(snake.Members.All(m => m.Walking && m.InFormation.Value && m is IBalanceAnchor { IsAnchored: true }));
        }

        [Test]
        public void ASnake_AtTheEdge_TurnsTheGentlestWayOpen()
        {
            var snake = FormSnake();
            for (var i = 0; i < 6 && snake.Members[0].SlotIndex.Value.x < _grid.Columns - 1; i++)
            {
                _service.StepSnakes(Relocate);
            }

            var head = snake.Members[0].SlotIndex.Value;
            _service.StepSnakes(Relocate);

            Assert.AreEqual(1, snake.Heading, "east is off the board: it turns right, south-east");
            Assert.AreEqual(HexCoordinates.Step(head, 1), snake.Members[0].SlotIndex.Value);
        }

        [Test]
        public void ASnake_BoxedIn_TurnsRoundAndItsTailLeads()
        {
            var snake = FormSnake();
            var head = snake.Members[0];
            var tail = snake.Members[snake.Members.Count - 1];
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = HexCoordinates.Step(head.SlotIndex.Value, direction);
                if (_grid.InBounds(slot) && _grid.IsEmpty(slot.x, slot.y))
                {
                    _grid.Place(new BalloonModel(), null, slot);
                }
            }

            var tailWas = tail.SlotIndex.Value;
            _service.StepSnakes(Relocate);

            Assert.AreSame(tail, snake.Members[0], "the tail leads now");
            Assert.AreEqual(1, HexCoordinates.Distance(tailWas, tail.SlotIndex.Value), "and took a step");
        }

        [Test]
        public void ASnake_NeverStepsWhereItWouldWallTheBoard()
        {
            // The snake holds row 2's west end. East of it stand three roamers, and one more sits above its tail:
            // stepping east, the head would link them all from wall to wall, so it turns south-east instead.
            var snake = FormSnake();
            Outsider(new Vector2Int(0, 1));
            Outsider(new Vector2Int(4, 2));
            Outsider(new Vector2Int(5, 2));
            Outsider(new Vector2Int(6, 2));
            var head = snake.Members[0].SlotIndex.Value;

            _service.StepSnakes(Relocate);

            Assert.AreEqual(1, snake.Heading);
            Assert.AreEqual(HexCoordinates.Step(head, 1), snake.Members[0].SlotIndex.Value);
        }

        [Test]
        public void DropWallClosingSlots_DropsTheSlotsThatWouldWallTheBoard()
        {
            for (var col = 0; col < 6; col++)
            {
                Outsider(new Vector2Int(col, 3));
            }

            var roamer = Outsider(new Vector2Int(2, 6));
            var slots = new List<Vector2Int> { new(6, 3), new(6, 0), new(5, 4) };

            _service.DropWallClosingSlots(roamer.SlotIndex.Value, slots);

            CollectionAssert.AreEqual(new[] { new Vector2Int(6, 0), new Vector2Int(5, 4) }, slots);
        }

        [Test]
        public void DropWallClosingSlots_OnABoardWalledAlready_DropsNothing()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                Outsider(new Vector2Int(col, 3));
            }

            var roamer = Outsider(new Vector2Int(2, 6));
            var slots = new List<Vector2Int> { new(6, 4), new(0, 0) };

            _service.DropWallClosingSlots(roamer.SlotIndex.Value, slots);

            Assert.AreEqual(2, slots.Count);
        }

        [Test]
        public void ASnakeMemberPopped_TheOneBehindClosesTheGap()
        {
            _settings.MinMembers.Returns(3);
            _settings.SnakeMaxMembers.Returns(4);
            var snake = FormSnake(4);
            var middle = snake.Members[1];
            var gap = middle.SlotIndex.Value;
            _grid.Remove(gap);
            var headWas = snake.Members[0].SlotIndex.Value;

            _service.StepSnakes(Relocate);

            Assert.AreEqual(3, snake.Members.Count);
            Assert.AreEqual(headWas, snake.Members[1].SlotIndex.Value, "the member behind the gap follows the head");
        }

        [Test]
        public void TakeSnakeBeat_ComesEverySnakeStepPulses_OnlyWithASnake()
        {
            Assert.IsFalse(_service.TakeSnakeBeat(), "no snake, no beat");

            FormSnake();
            _settings.SnakeStepPulses.Returns(2);

            Assert.IsFalse(_service.TakeSnakeBeat());
            Assert.IsTrue(_service.TakeSnakeBeat());
            Assert.IsFalse(_service.TakeSnakeBeat());
            Assert.IsTrue(_service.TakeSnakeBeat());
        }

        [Test]
        public void ASnakeMember_LeavingItsFormation_StopsWalking()
        {
            var snake = FormSnake();
            _service.StepSnakes(Relocate);
            var member = (UnbreakableBalloonModel)snake.Members[0];

            _service.ResetRun(1);

            Assert.IsFalse(member.Walking);
        }

        [Test]
        public void ASnakeJustArranged_WaitsForTheNextBeat_InTheBalanceThatFormedIt()
        {
            var snake = FormSnake();
            var formed = snake.Members.Select(m => m.SlotIndex.Value).ToList();

            _service.StepSnakes(Relocate, afterArranging: true);

            CollectionAssert.AreEqual(formed, snake.Members.Select(m => m.SlotIndex.Value).ToList());

            _service.StepSnakes(Relocate);

            CollectionAssert.AreNotEqual(formed, snake.Members.Select(m => m.SlotIndex.Value).ToList());
        }

        [Test]
        public void ASnake_ChangingToAnotherShape_StopsWalking()
        {
            var snake = FormSnake();
            _service.StepSnakes(Relocate);
            _settings.SnakeWeight.Returns(0f);
            _settings.HorizontalWeight.Returns(1f);
            for (var wave = 0; wave < 3; wave++)
            {
                Arrange();
            }

            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => u.InFormation.Value && !u.Walking));
        }

        // Horizontal and snake only, weight 1 each, and a roll of 0.4. Three unbreakables: 0.4 of a total of 2 lands
        // on the horizontal. Five, with the snake's weight up half for each of the two past the minimum: the snake
        // weighs 2, and 0.4 of a total of 3 lands past the horizontal's 1, on the snake.
        [TestCase(0.5f, 3, false)]
        [TestCase(0.5f, 5, true)]
        [TestCase(0f, 5, false)]
        public void TheSnakesWeight_GrowsWithTheUnbreakablesOnTheBoard(
            float perExtra, int unbreakables, bool snake)
        {
            _settings.DiagonalWeight.Returns(0f);
            _settings.ItemCoverWeight.Returns(0f);
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            _settings.SnakeWeightPerExtraUnbreakable.Returns(perExtra);
            _service.Random01 = () => 0.4f;
            var spots = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(1, 1),
                new Vector2Int(5, 3),
            };
            AddUnbreakables(spots.Take(unbreakables).ToArray());

            Arrange();

            Assert.AreEqual(snake ? FormationShape.Snake : FormationShape.Horizontal, _service.CurrentShape);
        }

        private void SnakeOnly()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            _settings.SnakeStepPulses.Returns(1);
        }

        // A snake of count along row 2 from the west edge, heading east (Random01 is 0). An even row, so at the
        // east edge the south-east slot is still on the board.
        private StandingFormation FormSnake(int count = 3)
        {
            SnakeOnly();
            for (var i = 0; i < count; i++)
            {
                AddUnbreakable(new Vector2Int(i, 2));
            }

            Arrange();
            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape);
            return _service.Formations[0];
        }

        private void Arrange()
        {
            _service.ArrangeAtWaveStart(Relocate);
        }

        private void Relocate(IWriteableDynamicSlotActor actor, Vector2Int target)
        {
            _relocations++;
            _grid.Remove(actor.SlotIndex.Value);
            _grid.Place(actor, null, target);
        }

        private void AddUnbreakables(params Vector2Int[] slots)
        {
            foreach (var slot in slots)
            {
                AddUnbreakable(slot);
            }
        }

        // An unbreakable placed after the formations arranged, so it stays out of them.
        private UnbreakableBalloonModel Outsider(Vector2Int slot)
        {
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
            _grid.Place(unbreakable, null, slot);
            return unbreakable;
        }

        private UnbreakableBalloonModel AddUnbreakable(Vector2Int slot)
        {
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
            _grid.Place(unbreakable, null, slot);
            _unbreakables.Add(unbreakable);
            return unbreakable;
        }
    }
}

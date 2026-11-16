using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Assist;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    // The board's toughs are one snake of their own, head to tail by armour (hits left), walking like an unbreakable
    // snake; every other tough joins at its tail.
    [TestFixture]
    public class ToughSnakeTests
    {
        private SlotGrid _grid;
        private IUnbreakableFormationSettings _settings;
        private UnbreakableFormationService _service;
        private List<ToughBalloonModel> _toughs;
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
            _settings.ToughSnakes.Returns(true);
            _settings.MinMembers.Returns(3);
            _settings.MaxMembers.Returns(5);
            _settings.SnakeMaxMembers.Returns(4);
            _settings.SnakeStepPulses.Returns(1);
            _settings.HoldWaves.Returns(3);
            _settings.BottomClearance.Returns(2);
            _settings.SpotChoices.Returns(1);
            _settings.HorizontalWeight.Returns(1f);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.UnbreakableFormation.Returns(_settings);

            _service = new UnbreakableFormationService(_grid, balloonsConfig,
                new ThreatEase(Substitute.For<IPlayerNeed>(), Substitute.For<IAssistSettings>()))
            {
                // 0 sends every snake east.
                Random01 = () => 0f,
            };
            _service.Start();

            _toughs = new List<ToughBalloonModel>();
            _relocations = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void FewerToughsThanTheMinimum_NeverSnake()
        {
            AddTough(new Vector2Int(0, 0), 3);
            AddTough(new Vector2Int(6, 4), 2);

            Arrange();

            Assert.AreEqual(0, _service.Formations.Count);
            Assert.IsFalse(_toughs.Any(t => t.IsAnchored));
        }

        [Test]
        public void WithToughSnakesOff_ToughsNeverSnake()
        {
            _settings.ToughSnakes.Returns(false);
            AddTough(new Vector2Int(0, 0), 1);
            AddTough(new Vector2Int(6, 4), 3);
            AddTough(new Vector2Int(3, 5), 2);

            Arrange();

            Assert.AreEqual(0, _service.Formations.Count);
        }

        [Test]
        public void ScatteredToughs_LineUpAsASnake_MostArmourAtTheHead()
        {
            AddTough(new Vector2Int(0, 0), 1);
            AddTough(new Vector2Int(6, 4), 3);
            AddTough(new Vector2Int(3, 5), 2);

            Arrange();

            var snake = SingleSnake();
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, Armour(snake), "head to tail by armour");
            Assert.AreEqual(0, snake.Heading, "heading east");
            var slots = snake.Members.Select(m => m.SlotIndex.Value).ToList();
            for (var i = 1; i < slots.Count; i++)
            {
                Assert.AreEqual(slots[0].y, slots[i].y, "one row");
                Assert.AreEqual(slots[i - 1].x - 1, slots[i].x, "the head at the east end, each behind it one west");
            }

            Assert.IsTrue(_toughs.All(t => t.IsAnchored && t.InFormation.Value), "anchored while in the snake");
            Assert.IsTrue(_toughs.All(t => _grid.At(t.SlotIndex.Value) == t), "every tough is on its slot");
        }

        [Test]
        public void ToughsInARow_InTheWrongOrder_TradePlacesIntoArmourOrder()
        {
            // Each one's slot in the armour order is held by another: they swap round in a ring.
            var three = AddTough(new Vector2Int(0, 2), 3);
            var one = AddTough(new Vector2Int(1, 2), 1);
            var two = AddTough(new Vector2Int(2, 2), 2);

            Arrange();

            var snake = SingleSnake();
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, Armour(snake));
            Assert.AreEqual(new Vector2Int(2, 2), three.SlotIndex.Value, "the toughest leads, at the east end");
            Assert.AreEqual(new Vector2Int(1, 2), two.SlotIndex.Value);
            Assert.AreEqual(new Vector2Int(0, 2), one.SlotIndex.Value);
            Assert.IsTrue(_toughs.All(t => _grid.At(t.SlotIndex.Value) == t), "the grid holds each where it ended");
        }

        [Test]
        public void ToughsInARow_WithEqualArmour_StayWhereTheyStand()
        {
            AddTough(new Vector2Int(0, 2), 2);
            AddTough(new Vector2Int(1, 2), 2);
            AddTough(new Vector2Int(2, 2), 2);

            Arrange();

            SingleSnake();
            Assert.AreEqual(0, _relocations, "already a line in armour order");
        }

        [Test]
        public void AToughSnakeStep_MovesTheHeadOn_AndEachMemberIntoTheSlotAhead()
        {
            var snake = FormRowSnake(3, 2, 1);
            var before = snake.Members.Select(m => m.SlotIndex.Value).ToList();

            _service.StepSnakes(Relocate);

            var after = snake.Members.Select(m => m.SlotIndex.Value).ToList();
            Assert.AreEqual(HexCoordinates.Step(before[0], 0), after[0], "the head steps east");
            for (var i = 1; i < after.Count; i++)
            {
                Assert.AreEqual(before[i - 1], after[i], "each member takes the slot the one ahead left");
            }

            Assert.IsTrue(snake.Members.All(m => m.Walking && m is IBalanceAnchor { IsAnchored: true }));
        }

        [Test]
        public void AToughHit_FallsBehindTheToughersAtTheNextStep()
        {
            var snake = FormRowSnake(3, 2, 1);
            var head = (ToughBalloonModel)snake.Members[0];
            var middle = (ToughBalloonModel)snake.Members[1];
            var tail = (ToughBalloonModel)snake.Members[2];
            var headWas = head.SlotIndex.Value;
            var middleWas = middle.SlotIndex.Value;
            head.HitsRemaining.Value = 1;

            _service.StepSnakes(Relocate);

            CollectionAssert.AreEqual(new[] { 2, 1, 1 }, Armour(snake), "the hit one drops back; equal armour keeps its order");
            Assert.AreSame(middle, snake.Members[0], "the tougher one leads now");
            Assert.AreEqual(HexCoordinates.Step(headWas, 0), middle.SlotIndex.Value, "into the slot ahead");
            Assert.AreEqual(headWas, head.SlotIndex.Value, "the hit one holds its slot, now second");
            Assert.AreEqual(middleWas, tail.SlotIndex.Value);
            Assert.IsTrue(_toughs.All(t => _grid.At(t.SlotIndex.Value) == t));
        }

        [Test]
        public void AToughSnake_BoxedIn_TurnsRound_AndTheToughestWorksBackToTheFront()
        {
            var snake = FormRowSnake(3, 2, 1);
            var toughest = snake.Members[0];
            var weakest = snake.Members[2];
            var tailWas = weakest.SlotIndex.Value;
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = HexCoordinates.Step(toughest.SlotIndex.Value, direction);
                if (_grid.InBounds(slot) && _grid.IsEmpty(slot.x, slot.y))
                {
                    _grid.Place(new BalloonModel(), null, slot);
                }
            }

            _service.StepSnakes(Relocate);

            // Turned round it runs 1, 2, 3; one place per step, the 2 passes the 1 at once.
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Armour(snake));
            Assert.AreEqual(1, HexCoordinates.Distance(tailWas, snake.Members[0].SlotIndex.Value),
                "the new head took the step past the old tail");
            Assert.AreEqual(tailWas, weakest.SlotIndex.Value, "the one passed waits a beat");
            Assert.IsTrue(_toughs.All(t => _grid.At(t.SlotIndex.Value) == t));

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, Armour(snake));

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, Armour(snake), "the toughest is back at the front");
            Assert.AreSame(toughest, snake.Members[0]);
        }

        [Test]
        public void TheToughs_AreOneBody_AndTheRestJoinAtTheTail()
        {
            _settings.SnakeMaxMembers.Returns(3);
            var far = AddTough(new Vector2Int(6, 5), 1);
            var near = AddTough(new Vector2Int(0, 5), 1);
            AddTough(new Vector2Int(4, 5), 1);
            var snake = FormRowSnake(3, 2, 1);
            var tailWas = snake.Members[2].SlotIndex.Value;

            Assert.AreEqual(3, snake.Members.Count, "three free toughs start no second snake");

            _service.StepSnakes(Relocate);

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreEqual(4, snake.Members.Count);
            Assert.AreSame(near, snake.Members[3], "the free tough nearest the tail joins first");
            Assert.AreEqual(tailWas, near.SlotIndex.Value, "into the slot the tail just left");
            Assert.IsTrue(near.IsAnchored && near.Walking);
            Assert.IsFalse(far.IsAnchored, "one joins per step");
            Assert.AreEqual(4, _service.Members.Count);

            _service.StepSnakes(Relocate);
            _service.StepSnakes(Relocate);

            Assert.AreEqual(6, snake.Members.Count);
            Assert.IsTrue(_toughs.All(t => t.IsAnchored && _grid.At(t.SlotIndex.Value) == t));
        }

        [Test]
        public void AToughStillLanding_WaitsToJoin()
        {
            _settings.SnakeMaxMembers.Returns(3);
            var landing = AddTough(new Vector2Int(0, 5), 1);
            var snake = FormRowSnake(3, 2, 1);
            landing.IsStable.Value = false;

            _service.StepSnakes(Relocate);

            Assert.AreEqual(3, snake.Members.Count);
            Assert.IsFalse(landing.IsAnchored);

            landing.IsStable.Value = true;
            _service.StepSnakes(Relocate);

            Assert.AreSame(landing, snake.Members[3]);
        }

        [Test]
        public void ATougherJoiner_WorksItsWayForward_OnePlacePerStep()
        {
            _settings.SnakeMaxMembers.Returns(3);
            var tougher = AddTough(new Vector2Int(0, 5), 3);
            var snake = FormRowSnake(3, 2, 1);

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 3 }, Armour(snake), "it joins at the tail");

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 2, 3, 1 }, Armour(snake));

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 3, 2, 1 }, Armour(snake));
            Assert.AreSame(tougher, snake.Members[1], "equal armour never trades, so it stays second");

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 3, 2, 1 }, Armour(snake));
            Assert.IsTrue(_toughs.All(t => _grid.At(t.SlotIndex.Value) == t));
        }

        [Test]
        public void ToughsAndUnbreakables_NeverShareAFormation()
        {
            _settings.SnakeWeight.Returns(0f);
            var unbreakables = new List<UnbreakableBalloonModel>();
            foreach (var slot in new[] { new Vector2Int(0, 0), new Vector2Int(6, 0), new Vector2Int(3, 1) })
            {
                var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
                _grid.Place(unbreakable, null, slot);
                unbreakables.Add(unbreakable);
            }

            AddTough(new Vector2Int(0, 5), 1);
            AddTough(new Vector2Int(6, 5), 3);
            AddTough(new Vector2Int(3, 4), 2);

            Arrange();

            Assert.AreEqual(2, _service.Formations.Count);
            var toughSnake = _service.Formations.Single(f => f.Armoured);
            var unbreakableFormation = _service.Formations.Single(f => !f.Armoured);
            Assert.AreEqual(FormationShape.Snake, toughSnake.Shape);
            Assert.IsTrue(toughSnake.Members.All(m => m is ToughBalloonModel));
            Assert.IsTrue(unbreakableFormation.Members.All(m => m is UnbreakableBalloonModel));
            Assert.IsTrue(unbreakables.All(u => u.IsAnchored));
        }

        [Test]
        public void AToughSnake_KeepsWalking_AndCallsNoReinforcements()
        {
            _settings.ReinforcementChance.Returns(1f);
            var snake = FormRowSnake(3, 2, 1);
            var members = snake.Members.ToList();

            for (var wave = 0; wave < 6; wave++)
            {
                Arrange();
            }

            Assert.AreSame(snake, SingleSnake(), "still the one snake");
            CollectionAssert.AreEqual(members, snake.Members, "never re-planned");
            Assert.AreEqual(0, _service.TakeReinforcementCalls());
        }

        [Test]
        public void AToughSnake_PoppedBelowTheMinimum_Breaks()
        {
            var snake = FormRowSnake(3, 2, 1);
            var popped = snake.Members[1];

            _grid.Remove(popped.SlotIndex.Value);

            Assert.AreEqual(0, _service.Formations.Count);
            Assert.IsFalse(_toughs.Where(t => t != popped).Any(t => t.IsAnchored || t.InFormation.Value));
        }

        [Test]
        public void AToughSnake_GetsTheSnakeBeat()
        {
            Assert.IsFalse(_service.TakeSnakeBeat(), "no snake, no beat");

            FormRowSnake(3, 2, 1);

            Assert.IsTrue(_service.TakeSnakeBeat());
        }

        // Three toughs along row 2 from the west edge, already in armour order for a snake heading east (the
        // toughest farthest east), so it forms where they stand.
        private StandingFormation FormRowSnake(params int[] armourHeadFirst)
        {
            for (var i = 0; i < armourHeadFirst.Length; i++)
            {
                AddTough(new Vector2Int(armourHeadFirst.Length - 1 - i, 2), armourHeadFirst[i]);
            }

            Arrange();
            var snake = SingleSnake();
            Assert.AreEqual(0, _relocations, "formed in place");
            return snake;
        }

        private StandingFormation SingleSnake()
        {
            Assert.AreEqual(1, _service.Formations.Count);
            var snake = _service.Formations[0];
            Assert.IsTrue(snake.Armoured);
            Assert.AreEqual(FormationShape.Snake, snake.Shape);
            return snake;
        }

        private static List<int> Armour(StandingFormation snake)
        {
            return snake.Members.Select(m => ((IArmouredFormationMember)m).Armour).ToList();
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

        private ToughBalloonModel AddTough(Vector2Int slot, int armour)
        {
            var tough = new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: armour));
            _grid.Place(tough, null, slot);
            _toughs.Add(tough);
            return tough;
        }
    }
}

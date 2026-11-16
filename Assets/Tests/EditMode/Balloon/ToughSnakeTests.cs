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
    // The board's toughs are one snake of their own, head to tail by armour (hits left). It starts from the toughest
    // where it stands, every other tough joins at its tail, and it walks through the balloons in its way.
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
            // 7 by 8 with a clearance of 2: a snake walks rows 0 to 5.
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
        public void FewerToughsThanTheMinimum_StartNoSnake()
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

            Assert.IsFalse(_service.TakeSnakeBeat());
            Assert.AreEqual(0, _service.Formations.Count);
        }

        [Test]
        public void AToughStillLanding_CountsToStartIt_ButCantLeadIt()
        {
            var head = AddTough(new Vector2Int(0, 0), 1);
            AddTough(new Vector2Int(6, 4), 3).IsStable.Value = false;
            AddTough(new Vector2Int(3, 5), 2).IsStable.Value = false;

            Arrange();

            Assert.AreSame(head, SingleSnake().Members[0], "the toughest that has landed");
            Assert.AreEqual(1, _service.Members.Count);
        }

        [Test]
        public void TheSnake_StartsFromTheToughest_WhereItStands()
        {
            AddTough(new Vector2Int(0, 0), 1);
            var toughest = AddTough(new Vector2Int(6, 4), 3);
            AddTough(new Vector2Int(3, 5), 2);

            Arrange();

            var snake = SingleSnake();
            Assert.AreEqual(1, snake.Members.Count);
            Assert.AreSame(toughest, snake.Members[0]);
            Assert.AreEqual(new Vector2Int(6, 4), toughest.SlotIndex.Value, "it starts where it stands");
            Assert.AreEqual(0, _relocations);
            Assert.IsTrue(toughest.IsAnchored);
            Assert.AreEqual(0, snake.Heading, "heading east");
        }

        [Test]
        public void TheHead_IsNeverOneInTheBottomRows()
        {
            AddTough(new Vector2Int(0, 7), 3);
            var head = AddTough(new Vector2Int(2, 3), 2);
            AddTough(new Vector2Int(4, 1), 1);

            Arrange();

            Assert.AreSame(head, SingleSnake().Members[0], "the toughest within the rows a snake walks");
        }

        [Test]
        public void TheRest_JoinAtTheTail_OnePerStep_TheNearestFirst()
        {
            var head = AddTough(new Vector2Int(1, 2), 3);
            var near = AddTough(new Vector2Int(1, 4), 2);
            var far = AddTough(new Vector2Int(5, 5), 1);
            Arrange();
            var snake = SingleSnake();

            _service.StepSnakes(Relocate);

            CollectionAssert.AreEqual(new[] { head, near }, snake.Members);
            Assert.AreEqual(new Vector2Int(2, 2), head.SlotIndex.Value, "the head steps east");
            Assert.AreEqual(new Vector2Int(1, 2), near.SlotIndex.Value, "into the slot the tail just left");
            Assert.IsTrue(near.IsAnchored && near.Walking);
            Assert.IsFalse(far.IsAnchored, "one joins per step");
            Assert.AreEqual(2, _service.Members.Count);

            _service.StepSnakes(Relocate);

            CollectionAssert.AreEqual(new[] { head, near, far }, snake.Members);
            CollectionAssert.AreEqual(
                new[] { new Vector2Int(3, 2), new Vector2Int(2, 2), new Vector2Int(1, 2) },
                snake.Members.Select(m => m.SlotIndex.Value).ToList(), "one line");
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void AToughStillLanding_WaitsToJoin()
        {
            var snake = GrowSnake(3, 2, 1);
            var landing = AddTough(new Vector2Int(6, 5), 1);
            landing.IsStable.Value = false;

            _service.StepSnakes(Relocate);

            Assert.AreEqual(3, snake.Members.Count);
            Assert.IsFalse(landing.IsAnchored);

            landing.IsStable.Value = true;
            _service.StepSnakes(Relocate);

            Assert.AreSame(landing, snake.Members[3]);
        }

        [Test]
        public void TheHead_WalksThroughABalloon_WhichGoesRoundToTheTail()
        {
            var snake = GrowSnake(3, 2, 1);
            var before = snake.Members.Select(m => m.SlotIndex.Value).ToList();
            var ahead = HexCoordinates.Step(before[0], 0);
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, ahead);

            _service.StepSnakes(Relocate);

            Assert.AreEqual(0, snake.Heading, "a balloon is no reason to turn");
            Assert.AreEqual(ahead, snake.Members[0].SlotIndex.Value);
            Assert.AreEqual(before[0], snake.Members[1].SlotIndex.Value);
            Assert.AreEqual(before[1], snake.Members[2].SlotIndex.Value);
            Assert.AreEqual(before[2], balloon.SlotIndex.Value, "the balloon takes the slot the tail left");
            Assert.AreSame(balloon, _grid.At(before[2]));
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void WhenAToughJoins_TheBalloonPassedTakesItsSlot()
        {
            var head = AddTough(new Vector2Int(1, 2), 3);
            AddTough(new Vector2Int(0, 0), 1);
            AddTough(new Vector2Int(6, 0), 1);
            Arrange();
            var joinerSlot = new Vector2Int(0, 0);
            var joiner = _grid.At(joinerSlot);
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, new Vector2Int(2, 2));

            _service.StepSnakes(Relocate);

            Assert.AreEqual(new Vector2Int(2, 2), head.SlotIndex.Value);
            Assert.AreSame(joiner, _grid.At(new Vector2Int(1, 2)), "the joiner falls in behind the head");
            Assert.AreEqual(joinerSlot, balloon.SlotIndex.Value, "and the balloon passed takes its slot");
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void AnyFormationMember_OrAnAnchoredActor_BlocksTheWay()
        {
            var snake = GrowSnake(3, 2, 1);
            var head = snake.Members[0].SlotIndex.Value;
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, HexCoordinates.Step(head, 0));

            _service.StepSnakes(Relocate);

            Assert.AreEqual(1, snake.Heading, "east is blocked: it turns south-east");
            Assert.AreEqual(HexCoordinates.Step(head, 1), snake.Members[0].SlotIndex.Value);
        }

        [Test]
        public void AToughHit_FallsBackOnePlace_AtTheNextStep()
        {
            var snake = GrowSnake(3, 2, 1);
            var head = (ToughBalloonModel)snake.Members[0];
            var middle = (ToughBalloonModel)snake.Members[1];
            var tail = (ToughBalloonModel)snake.Members[2];
            var headWas = head.SlotIndex.Value;
            var middleWas = middle.SlotIndex.Value;
            head.HitsRemaining.Value = 1;

            _service.StepSnakes(Relocate);

            CollectionAssert.AreEqual(new[] { 2, 1, 1 }, Armour(snake), "equal armour keeps its order");
            Assert.AreSame(middle, snake.Members[0], "the tougher one leads now");
            Assert.AreEqual(HexCoordinates.Step(headWas, 0), middle.SlotIndex.Value, "into the slot ahead");
            Assert.AreEqual(headWas, head.SlotIndex.Value, "the hit one waits a beat, now second");
            Assert.AreEqual(middleWas, tail.SlotIndex.Value);
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void ATougherJoiner_WorksItsWayForward_OnePlacePerStep()
        {
            var snake = GrowSnake(3, 2, 1);
            var tougher = AddTough(new Vector2Int(6, 5), 3);

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 3 }, Armour(snake), "it joins at the tail");

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 2, 3, 1 }, Armour(snake));

            _service.StepSnakes(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 3, 2, 1 }, Armour(snake));
            Assert.AreSame(tougher, snake.Members[1], "equal armour never trades, so it stays second");
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void ASnakeBoxedIn_TurnsRound_AndTheToughestWorksBackToTheFront()
        {
            var snake = GrowSnake(3, 2, 1);
            var toughest = snake.Members[0];
            var weakest = snake.Members[2];
            var tailWas = weakest.SlotIndex.Value;
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = HexCoordinates.Step(toughest.SlotIndex.Value, direction);
                if (_grid.InBounds(slot) && _grid.IsEmpty(slot.x, slot.y))
                {
                    _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, slot);
                }
            }

            _service.StepSnakes(Relocate);

            // Turned round it runs 1, 2, 3; one place per step, the 2 passes the 1 at once.
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Armour(snake));
            Assert.AreEqual(1, HexCoordinates.Distance(tailWas, snake.Members[0].SlotIndex.Value),
                "the new head took the step past the old tail");
            Assert.AreEqual(tailWas, weakest.SlotIndex.Value, "the one passed waits a beat");

            _service.StepSnakes(Relocate);
            _service.StepSnakes(Relocate);

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, Armour(snake), "the toughest is back at the front");
            Assert.AreSame(toughest, snake.Members[0]);
            Assert.IsTrue(AllOnTheirSlots());
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
            Assert.IsTrue(_service.Formations.Single(f => f.Armoured).Members.All(m => m is ToughBalloonModel));
            Assert.IsTrue(_service.Formations.Single(f => !f.Armoured).Members.All(m => m is UnbreakableBalloonModel));
            Assert.IsTrue(unbreakables.All(u => u.IsAnchored));
        }

        [Test]
        public void TheSnake_KeepsWalking_AndCallsNoReinforcements()
        {
            _settings.ReinforcementChance.Returns(1f);
            var snake = GrowSnake(3, 2, 1);
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
        public void TheSnake_WalksOnWithWhoeverIsLeft_UntilTheLastPops()
        {
            var snake = GrowSnake(3, 2, 1);
            var last = (ToughBalloonModel)snake.Members[0];

            _grid.Remove(snake.Members[2].SlotIndex.Value);
            _grid.Remove(snake.Members[1].SlotIndex.Value);

            Assert.AreSame(snake, SingleSnake());
            Assert.AreEqual(1, snake.Members.Count);
            Assert.IsTrue(last.IsAnchored);

            _grid.Remove(last.SlotIndex.Value);

            Assert.AreEqual(0, _service.Formations.Count);
            Assert.IsFalse(last.InFormation.Value);
        }

        [Test]
        public void TheSnake_GetsTheSnakeBeat()
        {
            Assert.IsFalse(_service.TakeSnakeBeat(), "no snake, no beat");

            GrowSnake(3, 2, 1);

            Assert.IsTrue(_service.TakeSnakeBeat());
        }

        [Test]
        public void ToughsLandedSinceTheWaveStart_StartTheSnakeOnTheBeat_AndItStepsOffAtOnce()
        {
            // A level can end before its next wave start; the toughs shouldn't wait for one.
            _settings.SnakeStepPulses.Returns(2);
            AddTough(new Vector2Int(0, 0), 1);
            var toughest = AddTough(new Vector2Int(6, 4), 3);
            AddTough(new Vector2Int(3, 5), 2);

            Assert.IsTrue(_service.TakeSnakeBeat(), "it steps on the beat that starts it");
            Assert.AreSame(toughest, SingleSnake().Members[0]);
            Assert.IsTrue(toughest.IsAnchored);
            Assert.AreEqual(1, _service.Members.Count);
            Assert.IsFalse(_service.TakeSnakeBeat(), "then keeps the beat's count");
        }

        [Test]
        public void ToughsStillLanding_StartNothingOnTheBeat()
        {
            AddTough(new Vector2Int(0, 0), 1).IsStable.Value = false;
            AddTough(new Vector2Int(6, 4), 3).IsStable.Value = false;
            AddTough(new Vector2Int(3, 5), 2).IsStable.Value = false;

            Assert.IsFalse(_service.TakeSnakeBeat());
            Assert.AreEqual(0, _service.Formations.Count);
        }

        // A snake of the given armour, head first, grown the way the game grows it: the head starts at the west end
        // of row 2 heading east, and the rest join at its tail one per step, nearest first, from row 5.
        private StandingFormation GrowSnake(params int[] armourHeadFirst)
        {
            AddTough(new Vector2Int(0, 2), armourHeadFirst[0]);
            for (var i = 1; i < armourHeadFirst.Length; i++)
            {
                AddTough(new Vector2Int(i - 1, 5), armourHeadFirst[i]);
            }

            Arrange();
            var snake = SingleSnake();
            for (var i = 1; i < armourHeadFirst.Length; i++)
            {
                _service.StepSnakes(Relocate);
            }

            CollectionAssert.AreEqual(armourHeadFirst, Armour(snake));
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

        private bool AllOnTheirSlots()
        {
            return _toughs.All(t => _grid.At(t.SlotIndex.Value) == t);
        }

        private void Arrange()
        {
            _service.ArrangeAtWaveStart(Relocate);
        }

        // As the balancer does it: every actor leaves its slot before any lands.
        private void Relocate(IReadOnlyList<SlotMove> moves)
        {
            _relocations += moves.Count;
            foreach (var move in moves)
            {
                _grid.Remove(move.Actor.SlotIndex.Value);
            }

            foreach (var move in moves)
            {
                _grid.Place(move.Actor, null, move.Target);
            }
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

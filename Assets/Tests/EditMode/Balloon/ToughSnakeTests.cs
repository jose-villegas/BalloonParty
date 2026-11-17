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
    // where it stands with every landed tough lined up behind it at once, pushing balloons out of the way; a tough
    // that lands later falls in behind the tail as soon as it lands; and it walks through the balloons in its way.
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

            Assert.IsFalse(_service.ToughsDueToGather());
            Assert.AreEqual(0, _service.Formations.Count);
        }

        [Test]
        public void AToughStillGlidingIn_HoldsTheStart_ThenAllFormAtOnce()
        {
            AddTough(new Vector2Int(0, 0), 1);
            var landing = AddTough(new Vector2Int(6, 4), 3);
            landing.IsStable.Value = false;
            AddTough(new Vector2Int(3, 5), 2);

            Arrange();

            Assert.AreEqual(0, _service.Formations.Count, "toughs rising together land together, so it waits");
            Assert.IsFalse(_service.ToughsDueToGather());

            landing.IsStable.Value = true;

            Assert.IsTrue(_service.ToughsDueToGather(), "the moment the last lands");
            _service.GatherToughSnake(Relocate);
            var snake = SingleSnake();
            Assert.AreSame(landing, snake.Members[0]);
            Assert.AreEqual(3, snake.Members.Count);
            Assert.IsTrue(IsOneLine(snake));
        }

        [Test]
        public void ToughsThatLandSideBySide_AreTheBody_AsTheyStand()
        {
            var head = AddTough(new Vector2Int(1, 3), 3);
            var second = AddTough(new Vector2Int(2, 3), 3);
            var third = AddTough(new Vector2Int(3, 3), 2);
            var fourth = AddTough(new Vector2Int(4, 3), 2);

            Arrange();

            var snake = SingleSnake();
            CollectionAssert.AreEqual(new IWriteableFormationMember[] { head, second, third, fourth }, snake.Members);
            Assert.AreEqual(0, _relocations, "nobody moves");
            Assert.AreEqual(3, snake.Heading, "it heads away from its body");
        }

        [Test]
        public void TheBody_BendsRoundTheToughs_WhereTheyStand()
        {
            // Two rows of a run, joined at the east end, as the initial fill deals more toughs than a line holds.
            var row = new List<ToughBalloonModel>();
            for (var col = 0; col < 7; col++)
            {
                row.Add(AddTough(new Vector2Int(col, 5), 3));
            }

            for (var col = 6; col >= 5; col--)
            {
                row.Add(AddTough(new Vector2Int(col, 4), 3));
            }

            Arrange();

            var snake = SingleSnake();
            CollectionAssert.AreEqual(row, snake.Members);
            Assert.AreEqual(0, _relocations, "nobody moves");
        }

        [Test]
        public void TheSnake_StartsFromTheToughest_WithTheRestLinedUpBehindIt_AtOnce()
        {
            var weakest = AddTough(new Vector2Int(0, 0), 1);
            var toughest = AddTough(new Vector2Int(6, 4), 3);
            var middle = AddTough(new Vector2Int(3, 5), 2);

            Arrange();

            var snake = SingleSnake();
            CollectionAssert.AreEqual(new IWriteableFormationMember[] { toughest, middle, weakest }, snake.Members);
            Assert.AreEqual(new Vector2Int(6, 4), toughest.SlotIndex.Value, "it starts where it stands");
            Assert.AreEqual(0, snake.Heading, "heading east, the body on the side with room");
            CollectionAssert.AreEqual(new[] { new Vector2Int(5, 4), new Vector2Int(4, 4) },
                new[] { middle.SlotIndex.Value, weakest.SlotIndex.Value }, "the rest in a line behind it");
            Assert.IsTrue(_toughs.All(t => t.IsAnchored));
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void TheLine_PushesTheBalloonsOnIt_OutToTheSlotsTheToughsLeft()
        {
            var head = AddTough(new Vector2Int(6, 2), 3);
            var second = AddTough(new Vector2Int(0, 0), 2);
            var third = AddTough(new Vector2Int(0, 5), 1);
            var first = new BalloonModel();
            var next = new BalloonModel();
            _grid.Place(first, null, new Vector2Int(5, 2));
            _grid.Place(next, null, new Vector2Int(4, 2));

            Arrange();

            var snake = SingleSnake();
            CollectionAssert.AreEqual(new IWriteableFormationMember[] { head, second, third }, snake.Members);
            Assert.AreEqual(new Vector2Int(5, 2), second.SlotIndex.Value);
            Assert.AreEqual(new Vector2Int(4, 2), third.SlotIndex.Value);
            Assert.AreEqual(new Vector2Int(0, 0), first.SlotIndex.Value, "each balloon swaps with a tough");
            Assert.AreEqual(new Vector2Int(0, 5), next.SlotIndex.Value);
            Assert.AreSame(first, _grid.At(new Vector2Int(0, 0)));
            Assert.AreSame(next, _grid.At(new Vector2Int(0, 5)));
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void TheLine_StopsWhereNothingTakesATough_AndTriesAgainWhenTheBoardChanges()
        {
            var head = AddTough(new Vector2Int(6, 2), 3);
            var blockers = new List<Vector2Int>();
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = HexCoordinates.Step(head.SlotIndex.Value, direction);
                if (_grid.InBounds(slot))
                {
                    _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, slot);
                    blockers.Add(slot);
                }
            }

            AddTough(new Vector2Int(0, 0), 2);
            AddTough(new Vector2Int(0, 5), 1);

            // Not a wave start, which would gather the unbreakables into a formation of their own.
            _service.GatherToughSnake(Relocate);

            Assert.AreEqual(1, SingleSnake().Members.Count, "walled in by unbreakables, nobody can fall in");
            Assert.IsFalse(_service.ToughsDueToGather(), "so it doesn't keep asking");

            _grid.Remove(blockers[0]);

            Assert.IsTrue(_service.ToughsDueToGather(), "a change on the board is worth another try");
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
        public void AToughThatLandsDuringAWalk_FallsInBehindTheNewTail_OnThatStep()
        {
            var snake = GrowSnake(3, 2, 1);
            var landing = AddTough(new Vector2Int(6, 5), 1);
            landing.IsStable.Value = false;

            _service.StepToughSnake(Relocate);

            Assert.AreEqual(3, snake.Members.Count, "one still landing waits");
            Assert.IsFalse(landing.IsAnchored);

            landing.IsStable.Value = true;
            _service.StepToughSnake(Relocate);

            Assert.AreSame(landing, snake.Members[3]);
            Assert.IsTrue(landing.IsAnchored);
            Assert.IsTrue(IsOneLine(snake));
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void TheHead_WalksThroughABalloon_WhichGoesRoundToTheTail()
        {
            var snake = GrowSnake(3, 2, 1);
            var before = snake.Members.Select(m => m.SlotIndex.Value).ToList();
            var ahead = HexCoordinates.Step(before[0], 0);
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, ahead);

            _service.StepToughSnake(Relocate);

            Assert.AreEqual(0, snake.Heading, "a balloon is no reason to turn");
            Assert.AreEqual(ahead, snake.Members[0].SlotIndex.Value);
            Assert.AreEqual(before[0], snake.Members[1].SlotIndex.Value);
            Assert.AreEqual(before[1], snake.Members[2].SlotIndex.Value);
            Assert.AreEqual(before[2], balloon.SlotIndex.Value, "the balloon takes the slot the tail left");
            Assert.AreSame(balloon, _grid.At(before[2]));
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void AnyFormationMember_OrAnAnchoredActor_BlocksTheWay()
        {
            var snake = GrowSnake(3, 2, 1);
            var head = snake.Members[0].SlotIndex.Value;
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, HexCoordinates.Step(head, 0));

            _service.StepToughSnake(Relocate);

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

            _service.StepToughSnake(Relocate);

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

            _service.StepToughSnake(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 3 }, Armour(snake), "it joins at the tail");

            _service.StepToughSnake(Relocate);
            CollectionAssert.AreEqual(new[] { 3, 2, 3, 1 }, Armour(snake));

            _service.StepToughSnake(Relocate);
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

            _service.StepToughSnake(Relocate);

            // Turned round it runs 1, 2, 3; one place per step, the 2 passes the 1 at once.
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Armour(snake));
            Assert.AreEqual(1, HexCoordinates.Distance(tailWas, snake.Members[0].SlotIndex.Value),
                "the new head took the step past the old tail");
            Assert.AreEqual(tailWas, weakest.SlotIndex.Value, "the one passed waits a beat");

            _service.StepToughSnake(Relocate);
            _service.StepToughSnake(Relocate);

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
        public void TheSnake_WalksOnAClockOfItsOwn_NotTheUnbreakablesBeat()
        {
            _settings.ToughSnakeStepSeconds.Returns(1.5f);
            Assert.IsFalse(_service.HasToughSnake);

            GrowSnake(3, 2, 1);

            Assert.IsTrue(_service.HasToughSnake);
            Assert.AreEqual(1.5f, _service.ToughSnakeStepSeconds);
            Assert.IsFalse(_service.TakeSnakeBeat(), "the unbreakables' beat isn't its");
        }

        [Test]
        public void AnUnbreakableSnakesStep_LeavesTheToughSnake()
        {
            var snake = GrowSnake(3, 2, 1);
            var before = snake.Members.Select(m => m.SlotIndex.Value).ToList();

            _service.StepSnakes(Relocate);

            CollectionAssert.AreEqual(before, snake.Members.Select(m => m.SlotIndex.Value).ToList());
        }

        [Test]
        public void ToughsThatHaveLanded_AreDueToFormUp_WithoutWaitingForAWaveStart()
        {
            AddTough(new Vector2Int(0, 0), 1);
            AddTough(new Vector2Int(6, 4), 3);
            AddTough(new Vector2Int(3, 5), 2);

            Assert.IsTrue(_service.ToughsDueToGather());
            _service.GatherToughSnake(Relocate);

            Assert.AreEqual(3, SingleSnake().Members.Count);
            Assert.IsTrue(IsOneLine(SingleSnake()));
            Assert.IsFalse(_service.ToughsDueToGather(), "all in");
        }

        [Test]
        public void ToughsAllStillLanding_AreNotDue()
        {
            AddTough(new Vector2Int(0, 0), 1).IsStable.Value = false;
            AddTough(new Vector2Int(6, 4), 3).IsStable.Value = false;
            AddTough(new Vector2Int(3, 5), 2).IsStable.Value = false;

            Assert.IsFalse(_service.ToughsDueToGather());
        }

        // A snake of the given armour, head first, formed the way the game forms it: the head starts mid row 2 heading
        // east, and the rest, from row 5, line up west of it at once.
        private StandingFormation GrowSnake(params int[] armourHeadFirst)
        {
            AddTough(new Vector2Int(3, 2), armourHeadFirst[0]);
            for (var i = 1; i < armourHeadFirst.Length; i++)
            {
                AddTough(new Vector2Int(i - 1, 5), armourHeadFirst[i]);
            }

            Arrange();
            var snake = SingleSnake();
            Assert.AreEqual(0, snake.Heading);
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

        private static bool IsOneLine(StandingFormation snake)
        {
            for (var i = 1; i < snake.Members.Count; i++)
            {
                if (HexCoordinates.Distance(snake.Members[i - 1].SlotIndex.Value, snake.Members[i].SlotIndex.Value) != 1)
                {
                    return false;
                }
            }

            return true;
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

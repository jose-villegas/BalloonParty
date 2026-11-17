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
    // The board's toughs are one snake of their own. It starts from the deepest where it stands with every landed
    // tough lined up behind it at once, pushing balloons out of the way; a tough that lands later falls in behind
    // the tail as soon as it lands; and it walks through the balloons in its way, keeping its order and shape.
    [TestFixture]
    public class ToughSnakeTests
    {
        // Where GrowSnake's toughs after the head start: on row 0, none touching another, each farther from the head
        // than the last.
        private static readonly Vector2Int[] Joiners = { new(3, 0), new(5, 0), new(0, 0) };

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
            _settings.ToughSnakeMinMembers.Returns(3);
            _settings.MaxMembers.Returns(5);
            _settings.SnakeMaxMembers.Returns(4);
            _settings.BeatSeconds.Returns(1.7f);
            _settings.SnakeHoldCycles.Returns(3);
            _settings.LineHoldCycles.Returns(3);
            _settings.ItemCoverHoldCycles.Returns(3);
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
        public void APairOfToughers_IsASnake_WithItsOwnMinimum()
        {
            // Apart from MinMembers (3 here), which the unbreakables' formations keep.
            _settings.ToughSnakeMinMembers.Returns(2);
            var west = AddTough(new Vector2Int(2, 2), 5);
            var east = AddTough(new Vector2Int(3, 2), 5);

            Assert.IsTrue(_service.ToughsDueToGather());
            _service.GatherToughSnake(Relocate);

            CollectionAssert.AreEqual(new IWriteableFormationMember[] { west, east }, SingleSnake().Members);
            Assert.AreEqual(0, _relocations, "side by side, they're the body as they stand");
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
        public void ToughsStillGlidingIn_FormUpAtOnce_WhereTheyAreHeading()
        {
            AddTough(new Vector2Int(0, 0), 1).IsStable.Value = false;
            var rising = AddTough(new Vector2Int(3, 5), 3);
            rising.IsStable.Value = false;
            AddTough(new Vector2Int(6, 4), 2).IsStable.Value = false;

            Assert.IsTrue(_service.ToughsDueToGather(), "no waiting for them to land");
            _service.GatherToughSnake(Relocate);

            var snake = SingleSnake();
            Assert.AreSame(rising, snake.Members[0], "the deepest leads, though still rising");
            Assert.AreEqual(3, snake.Members.Count);
            Assert.IsTrue(IsOneLine(snake));
            Assert.IsTrue(_toughs.All(t => t.IsAnchored), "anchored at once, so the balancer leaves them be");
            Assert.IsFalse(_service.ToughSnakeLanded, "its first step waits for its head to land");

            rising.IsStable.Value = true;

            Assert.IsTrue(_service.ToughSnakeLanded, "the rest still rising hold nothing up");
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
        public void TheSnake_StartsFromTheDeepest_NotTheToughest_WithTheRestLinedUpBehindIt_AtOnce()
        {
            var toughest = AddTough(new Vector2Int(0, 0), 3);
            var deepest = AddTough(new Vector2Int(6, 5), 1);
            var middle = AddTough(new Vector2Int(3, 4), 2);

            Arrange();

            var snake = SingleSnake();
            CollectionAssert.AreEqual(new IWriteableFormationMember[] { deepest, middle, toughest }, snake.Members,
                "the nearest falls in first; hits left don't count");
            Assert.AreEqual(new Vector2Int(6, 5), deepest.SlotIndex.Value, "it starts where it stands");
            Assert.AreEqual(0, snake.Heading, "heading east, the body on the side with room");
            CollectionAssert.AreEqual(new[] { new Vector2Int(5, 5), new Vector2Int(4, 5) },
                new[] { middle.SlotIndex.Value, toughest.SlotIndex.Value }, "the rest in a line behind it");
            Assert.IsTrue(_toughs.All(t => t.IsAnchored));
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void TheBiggestGroup_Leads_NotADeeperToughOnItsOwn()
        {
            var west = AddTough(new Vector2Int(2, 3), 3);
            AddTough(new Vector2Int(3, 3), 3);
            AddTough(new Vector2Int(4, 3), 3);
            AddTough(new Vector2Int(0, 5), 3);

            Arrange();

            var snake = SingleSnake();
            Assert.AreSame(west, snake.Members[0], "the run's west end, though (0,5) is deeper");
            Assert.AreEqual(4, snake.Members.Count);
            Assert.IsTrue(IsOneLine(snake));
        }

        [Test]
        public void ASnakeBoxedIn_WithMoreToughsFreeThanItHolds_StartsOverFromThem()
        {
            // A pair walled in at the bottom corner, the snake; a run of three free above that can't reach it.
            var pocket = new[] { AddTough(new Vector2Int(0, 5), 3), AddTough(new Vector2Int(1, 5), 3) };
            _settings.ToughSnakeMinMembers.Returns(2);
            _service.GatherToughSnake(Relocate);
            Assert.AreEqual(2, SingleSnake().Members.Count);
            foreach (var slot in new[] { new Vector2Int(0, 4), new Vector2Int(1, 4), new Vector2Int(2, 4), new Vector2Int(2, 5) })
            {
                _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, slot);
            }

            var run = new[] { AddTough(new Vector2Int(3, 1), 3), AddTough(new Vector2Int(4, 1), 3), AddTough(new Vector2Int(5, 1), 3) };
            _service.GatherToughSnake(Relocate);

            var snake = SingleSnake();
            Assert.AreSame(run[0], snake.Members[0], "the bigger group leads now");
            CollectionAssert.IsSubsetOf(run, snake.Members);
            Assert.IsTrue(IsOneLine(snake));
            Assert.IsTrue(pocket.All(t => !t.IsAnchored || snake.Members.Contains(t)));
        }

        [Test]
        public void TheLine_PushesTheBalloonsOnIt_OutToTheSlotsTheToughsLeft()
        {
            var head = AddTough(new Vector2Int(6, 2), 3);
            var second = AddTough(new Vector2Int(3, 0), 2);
            var third = AddTough(new Vector2Int(0, 1), 1);
            var first = new BalloonModel();
            var next = new BalloonModel();
            _grid.Place(first, null, new Vector2Int(5, 2));
            _grid.Place(next, null, new Vector2Int(4, 2));

            Arrange();

            var snake = SingleSnake();
            CollectionAssert.AreEqual(new IWriteableFormationMember[] { head, second, third }, snake.Members);
            Assert.AreEqual(new Vector2Int(5, 2), second.SlotIndex.Value);
            Assert.AreEqual(new Vector2Int(4, 2), third.SlotIndex.Value);
            Assert.AreEqual(new Vector2Int(3, 0), first.SlotIndex.Value, "each balloon swaps with a tough");
            Assert.AreEqual(new Vector2Int(0, 1), next.SlotIndex.Value);
            Assert.AreSame(first, _grid.At(new Vector2Int(3, 0)));
            Assert.AreSame(next, _grid.At(new Vector2Int(0, 1)));
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
            AddTough(new Vector2Int(3, 0), 1);

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

            Assert.AreSame(head, SingleSnake().Members[0], "the deepest within the rows a snake walks");
        }

        [Test]
        public void AToughStillRising_FallsInBehindTheTail_WithoutWaitingToLand()
        {
            var snake = GrowSnake(3, 2, 1);
            var landing = AddTough(new Vector2Int(6, 5), 1);
            landing.IsStable.Value = false;

            Assert.IsTrue(_service.ToughsDueToGather());
            _service.StepToughSnake(Relocate);

            Assert.AreSame(landing, snake.Members[3], "it joins on the step, still rising");
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
        public void AToughHit_KeepsItsPlace_AndTheBodyItsShape()
        {
            var snake = GrowSnake(3, 2, 1);
            var members = snake.Members.ToList();
            var before = members.Select(m => m.SlotIndex.Value).ToList();
            ((ToughBalloonModel)members[0]).HitsRemaining.Value = 1;

            _service.StepToughSnake(Relocate);

            CollectionAssert.AreEqual(members, snake.Members, "no re-ordering by hits left");
            Assert.AreEqual(HexCoordinates.Step(before[0], 0), members[0].SlotIndex.Value, "into the slot ahead");
            Assert.AreEqual(before[0], members[1].SlotIndex.Value, "each in the slot the one ahead left");
            Assert.AreEqual(before[1], members[2].SlotIndex.Value);
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void ATougherJoiner_StaysAtTheTail()
        {
            var snake = GrowSnake(3, 2, 1);
            var tougher = AddTough(new Vector2Int(6, 5), 3);

            for (var step = 0; step < 3; step++)
            {
                _service.StepToughSnake(Relocate);

                Assert.AreSame(tougher, snake.Members[3], "it joins at the tail and walks there");
                Assert.IsTrue(IsOneLine(snake));
            }

            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void ASnakeBoxedIn_TurnsRound_ItsTailLeading()
        {
            var snake = GrowSnake(3, 2, 1);
            var members = snake.Members.ToList();
            var before = members.Select(m => m.SlotIndex.Value).ToList();
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = HexCoordinates.Step(before[0], direction);
                if (_grid.InBounds(slot) && _grid.IsEmpty(slot.x, slot.y))
                {
                    _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, slot);
                }
            }

            _service.StepToughSnake(Relocate);

            members.Reverse();
            CollectionAssert.AreEqual(members, snake.Members, "the same body, walked the other way");
            Assert.AreEqual(1, HexCoordinates.Distance(before[2], members[0].SlotIndex.Value),
                "the old tail took the step past itself");
            Assert.AreEqual(before[2], members[1].SlotIndex.Value, "each in the slot the one ahead left");
            Assert.AreEqual(before[1], members[2].SlotIndex.Value);
            Assert.IsTrue(IsOneLine(snake));
            Assert.IsTrue(AllOnTheirSlots());
        }

        [Test]
        public void AMemberPopped_TheBodyClosesUp_AtOnce()
        {
            var snake = GrowSnake(3, 2, 1, 1);
            var before = snake.Members.Select(m => m.SlotIndex.Value).ToList();
            var behind = snake.Members.Skip(2).ToList();

            _grid.Remove(before[1]);

            Assert.IsTrue(_service.ToughsDueToGather(), "a gap is due to close");
            _service.GatherToughSnake(Relocate);

            Assert.AreEqual(3, snake.Members.Count);
            Assert.AreEqual(before[1], behind[0].SlotIndex.Value, "into the slot the popped one left");
            Assert.AreEqual(before[2], behind[1].SlotIndex.Value, "and the one behind into its slot");
            Assert.IsTrue(IsOneLine(snake));
            Assert.IsFalse(_service.ToughsDueToGather(), "one body again");
        }

        [Test]
        public void ATailBoxedIn_TheToughsFallInAtTheHead_TheSnakeTurnedRound()
        {
            var snake = GrowSnake(3, 2, 1);
            var members = snake.Members.ToList();
            var tail = members[2].SlotIndex.Value;
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = HexCoordinates.Step(tail, direction);
                if (_grid.InBounds(slot) && _grid.IsEmpty(slot.x, slot.y))
                {
                    _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, slot);
                }
            }

            var joiner = AddTough(new Vector2Int(6, 5), 2);
            _service.GatherToughSnake(Relocate);

            members.Reverse();
            members.Add(joiner);
            CollectionAssert.AreEqual(members, snake.Members, "the old head's end is the tail now");
            Assert.AreEqual(3, snake.Heading, "it walks the other way");
            Assert.IsTrue(IsOneLine(snake));
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
        public void TheBody_ListsTheToughSnake_HeadFirst_AndNothingOnceItsGone()
        {
            IToughSnakeBody body = _service;
            Assert.AreEqual(0, body.ToughSnakeMembers.Count);

            var snake = GrowSnake(3, 2, 1);

            CollectionAssert.AreEqual(snake.Members, body.ToughSnakeMembers);

            foreach (var member in snake.Members.ToList())
            {
                _grid.Remove(member.SlotIndex.Value);
            }

            Assert.AreEqual(0, body.ToughSnakeMembers.Count);
        }

        [Test]
        public void TheSnake_WalksOnAClockOfItsOwn_NotTheUnbreakablesBeat()
        {
            _settings.ToughSnakeStepSeconds.Returns(1.5f);
            Assert.IsFalse(_service.HasToughSnake);

            GrowSnake(3, 2, 1);

            Assert.IsTrue(_service.HasToughSnake);
            Assert.AreEqual(1.5f, _service.ToughSnakeStepSeconds);
            Assert.IsFalse(_service.HasUnbreakableSnake, "the unbreakables' clock isn't its");
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
        public void TooFewToughs_EvenRising_AreNotDue()
        {
            AddTough(new Vector2Int(0, 0), 1).IsStable.Value = false;
            AddTough(new Vector2Int(6, 4), 3).IsStable.Value = false;

            Assert.IsFalse(_service.ToughsDueToGather());
        }

        // A snake of toughs with the given hits left, head first, formed the way the game forms it: the head starts
        // mid row 2 heading east, the deepest, and the rest (Joiners) line up west of it at once, in the order given.
        private StandingFormation GrowSnake(params int[] hitsHeadFirst)
        {
            var toughs = new List<ToughBalloonModel> { AddTough(new Vector2Int(3, 2), hitsHeadFirst[0]) };
            for (var i = 1; i < hitsHeadFirst.Length; i++)
            {
                toughs.Add(AddTough(Joiners[i - 1], hitsHeadFirst[i]));
            }

            Arrange();
            var snake = SingleSnake();
            Assert.AreEqual(0, snake.Heading);
            CollectionAssert.AreEqual(toughs, snake.Members);
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
            _service.ArrangeOnCycle(Relocate);
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

using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Level;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    // A ring is the only weighted shape unless a test says otherwise, so every formation here is one. The ring
    // forms where its unbreakables already stand: round (3, 3), on slots 0 to 3 (east round to west).
    [TestFixture]
    public class RingFormationServiceTests
    {
        private static readonly Vector2Int Centre = new(3, 3);

        private SlotGrid _grid;
        private IUnbreakableFormationSettings _settings;
        private UnbreakableFormationService _service;
        private ProjectileRegistry _projectiles;
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
            _settings.ReshapeEveryCycles.Returns(8);
            _settings.BottomClearance.Returns(2);
            _settings.SpotChoices.Returns(1);
            _settings.RingWeight.Returns(1f);
            _settings.RingMinMembers.Returns(4);
            _settings.RingMaxMembers.Returns(5);
            _settings.TurnReverseEveryCycles.Returns(4);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.UnbreakableFormation.Returns(_settings);

            var levelParameters = Substitute.For<ILevelParameters>();
            levelParameters.AllowedFormationShapes.Returns(FormationShapes.All);
            var level = Substitute.For<IActiveLevelParameters>();
            level.Current.Returns(levelParameters);

            _projectiles = new ProjectileRegistry();
            _service = new UnbreakableFormationService(_grid, balloonsConfig,
                new ThreatEase(Substitute.For<IPlayerNeed>(), Substitute.For<IAssistSettings>()), _projectiles,
                level)
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
        public void FourFreeUnbreakables_FormARingRoundTheSlotTheyStandAround()
        {
            var ring = FormRing();

            Assert.IsTrue(_service.HasTurningFormation);
            Assert.AreEqual(4, ring.MemberList.Count);
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored && u.InFormation.Value));
        }

        [Test]
        public void FiveFreeUnbreakables_FormARingOfFive_WithOneSlotOpen()
        {
            var ring = FormRing(5);

            Assert.AreEqual(5, ring.MemberList.Count);
            var held = ring.MemberList.Select(Direction).Distinct().Count();
            Assert.AreEqual(5, held, "five of the six neighbours");
        }

        [Test]
        public void ThreeFreeUnbreakables_FormNoRing()
        {
            AddUnbreakables(Around(0, 1, 2));

            Arrange();

            Assert.IsFalse(_service.HasTurningFormation);
            Assert.AreEqual(0, _service.Formations.Count, "a ring is the only shape in the pick");
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TurnRings_EachCallTurnsEveryMemberOneSlotRound(bool clockwise)
        {
            var ring = FormRing(4, clockwise);
            var before = ring.MemberList.Select(Direction).ToList();
            var turn = clockwise ? 1 : -1;

            _service.TurnOnTheBeat(Relocate);

            AssertDirections(ring, before.Select(d => HexCoordinates.Turn(d, turn)).ToList());

            _service.TurnOnTheBeat(Relocate);

            AssertDirections(ring, before.Select(d => HexCoordinates.Turn(d, 2 * turn)).ToList());
            Assert.IsTrue(_unbreakables.All(u => _grid.At(u.SlotIndex.Value) == u), "every member is on its slot");
        }

        [Test]
        public void TurnRings_EveryMemberHeadsAlongTheRing()
        {
            FormRing();
            var expected = _unbreakables.ToDictionary(u => u, u => AlongTheRing(u, true));

            _service.TurnOnTheBeat(Relocate);

            Assert.IsTrue(_unbreakables.All(u => Vector2.Angle(u.FormationHeading, expected[u]) < 1f));
        }

        [Test]
        public void TurnRings_AfterArranging_LeavesARingArrangedInThatPassForTheNextBeat()
        {
            FormRing();
            var relocations = _relocations;

            _service.TurnOnTheBeat(Relocate, afterArranging: true);

            Assert.AreEqual(relocations, _relocations, "formed this pass: it waits for the beat");

            _service.TurnOnTheBeat(Relocate, afterArranging: true);
            Assert.AreEqual(relocations, _relocations);

            _service.TurnOnTheBeat(Relocate);
            Assert.AreEqual(relocations + 4, _relocations, "the beat itself turns it");
        }

        [Test]
        public void TurnRings_AfterArranging_TurnsARingFormedInAnEarlierPass()
        {
            FormRing();
            Arrange();
            var relocations = _relocations;

            _service.TurnOnTheBeat(Relocate, afterArranging: true);

            Assert.AreEqual(relocations + 4, _relocations);
        }

        // The run counts its cycles, not the formation: forming the ring is cycle 1, and every fourth cycle of the
        // run turns every ring and spinner standing the other way.
        [Test]
        public void TheRing_TurnsTheOtherWay_OnEveryTurnReverseEveryCyclesCycleOfTheRun()
        {
            var ring = FormRing();
            Assert.IsTrue(ring.Clockwise);

            Arrange();
            Arrange();

            Assert.IsTrue(ring.Clockwise, "cycles 2 and 3 of the run");

            Arrange();

            Assert.IsFalse(ring.Clockwise, "cycle 4 turns it round");
            var before = ring.MemberList.Select(Direction).ToList();
            _service.TurnOnTheBeat(Relocate);
            AssertDirections(ring, before.Select(d => HexCoordinates.Turn(d, -1)).ToList());
        }

        [Test]
        public void TheRing_TurnsBackAgain_OnCycleEightOfTheRun()
        {
            _settings.ReshapeEveryCycles.Returns(12);
            var ring = FormRing();

            for (var cycle = 2; cycle <= 7; cycle++)
            {
                Arrange();
                Assert.AreEqual(cycle < 4, ring.Clockwise, "turned round on cycle 4 and not again before 8: cycle " + cycle);
            }

            Arrange();

            Assert.IsTrue(ring.Clockwise, "round and back");
        }

        [Test]
        public void TheRing_NeverTurnsTheOtherWay_WhenReversingIsOff()
        {
            _settings.TurnReverseEveryCycles.Returns(0);
            var ring = FormRing();

            for (var cycle = 0; cycle < 7; cycle++)
            {
                Arrange();
            }

            Assert.IsTrue(ring.Clockwise);
        }

        [Test]
        public void ARingAndASpinner_TurnBackTogether_EveryTurnReverseEveryCyclesCycleOfTheRun()
        {
            // Ring first in the pick (Random01 0), four round the centre; the three left over make a spinner.
            _settings.RingMaxMembers.Returns(4);
            _settings.SpinnerWeight.Returns(1f);
            _settings.SpinnerMaxMembers.Returns(3);
            var middle = new Vector2Int(1, 1);
            AddUnbreakables(HexCoordinates.Step(middle, 3), middle, HexCoordinates.Step(middle, 0));
            var ring = FormRing();
            var spinner = _service.Formations.Single(f => f.Shape == FormationShape.Spinner);
            Assert.AreEqual(middle, spinner.TurnCentre, "control: the spinner formed on the same reshape");

            Arrange();
            Arrange();
            Assert.IsTrue(ring.Clockwise);
            Assert.IsTrue(spinner.Clockwise);

            Arrange();

            Assert.IsFalse(ring.Clockwise, "cycle 4");
            Assert.IsFalse(spinner.Clockwise, "cycle 4");

            for (var cycle = 5; cycle <= 7; cycle++)
            {
                Arrange();
            }

            Arrange();

            Assert.IsTrue(ring.Clockwise, "cycle 8");
            Assert.IsTrue(spinner.Clockwise, "cycle 8");
        }

        [Test]
        public void ResetRun_StartsTheCycleCountOver_SoTheNextRunsFirstReverseIsOnItsFourthCycle()
        {
            FormRing();
            _service.ResetRun(1);
            Assert.AreEqual(0, _service.Formations.Count, "control: the run's formations are let go");

            Arrange();
            var ring = _service.Formations[0];
            Assert.IsTrue(ring.Clockwise, "the first cycle of the new run");
            Arrange();
            Arrange();

            Assert.IsTrue(ring.Clockwise, "cycle 3 of the new run: it would be cycle 4 had the count carried on");

            Arrange();

            Assert.IsFalse(ring.Clockwise, "cycle 4 of the new run");
        }

        [Test]
        public void ARingBlockedOneWay_TurnsTheOtherWayTheSameBeat()
        {
            var ring = FormRing();
            var before = ring.MemberList.Select(Direction).ToList();
            Outsider(Slot(4));

            _service.TurnOnTheBeat(Relocate);

            Assert.IsFalse(ring.Clockwise, "it keeps the way that worked");
            AssertDirections(ring, before.Select(d => HexCoordinates.Turn(d, -1)).ToList());
        }

        [Test]
        public void ARingBlockedBothWays_MissesTheBeat_AndKeepsItsWay()
        {
            var ring = FormRing();
            var before = ring.MemberList.Select(Direction).ToList();
            var relocations = _relocations;
            Outsider(Slot(4));
            Outsider(Slot(5));

            _service.TurnOnTheBeat(Relocate);

            Assert.AreEqual(relocations, _relocations);
            Assert.IsTrue(ring.Clockwise);
            AssertDirections(ring, before);
        }

        [Test]
        public void ARingWithAMemberAwaitingALancesDischarge_StaysPut()
        {
            var ring = FormRing();
            var before = ring.MemberList.Select(Direction).ToList();
            Plow(_unbreakables[0]);

            _service.TurnOnTheBeat(Relocate);

            AssertDirections(ring, before);
        }

        [Test]
        public void ABalloonInTheRingsWay_TradesPlacesWithTheTurn()
        {
            FormRing();
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, Slot(4));

            _service.TurnOnTheBeat(Relocate);

            Assert.AreEqual(Slot(0), balloon.SlotIndex.Value, "it goes to the slot the tail left");
            Assert.IsTrue(_unbreakables.All(u => _grid.At(u.SlotIndex.Value) == u));
            Assert.AreSame(balloon, _grid.At(Slot(0)));
        }

        [Test]
        public void TheBalloonAtTheCentre_IsHeld_UntilTheRingEndsItsHold_AndDrawsNoRingOfLight()
        {
            _settings.ReshapeEveryCycles.Returns(2);
            var host = new BalloonModel();
            _grid.Place(host, null, Centre);
            FormRing();

            Assert.AreEqual(0, _service.CoveredHosts.Count, "only a cover's balloon gets a ring of light");
            Assert.IsTrue(host.IsAnchored, "held where it stands, so the ring turns round it");

            Arrange();
            Assert.IsTrue(host.IsAnchored, "still within the hold");

            _settings.RingWeight.Returns(0f);
            _settings.HorizontalWeight.Returns(1f);
            Arrange();

            Assert.AreNotEqual(FormationShape.Ring, _service.CurrentShape);
            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.IsFalse(host.IsAnchored, "let go with the ring");
        }

        [Test]
        public void ARingThatEndsWithNothingToChangeTo_IsPlannedAgain_AndHoldsItsCentreStill()
        {
            _settings.ReshapeEveryCycles.Returns(1);
            var host = new BalloonModel();
            _grid.Place(host, null, Centre);
            FormRing();

            Arrange();

            Assert.AreEqual(FormationShape.Ring, _service.CurrentShape, "no other shape is in the pick");
            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.IsTrue(host.IsAnchored);
        }

        [Test]
        public void ARingHoldsUntilTheNextReshape_ThenChangesToAnotherShape()
        {
            _settings.ReshapeEveryCycles.Returns(2);
            FormRing();
            _settings.RingWeight.Returns(0f);
            _settings.HorizontalWeight.Returns(1f);

            Arrange();
            Assert.AreEqual(FormationShape.Ring, _service.CurrentShape, "formed on the first cycle, held through the second");

            Arrange();
            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape);
            Assert.IsFalse(_service.HasTurningFormation);
        }

        [Test]
        public void ASnakeDoneHolding_LeansTowardTheRing()
        {
            _settings.RingWeightAfterSnake.Returns(9f);
            FormSnake();
            LetTheSnakeFinishItsHold();
            _service.Random01 = () => 0.4f;

            Arrange();

            Assert.AreEqual(FormationShape.Ring, _service.CurrentShape, "ring 9 against a line's 1, a roll of 0.4");
        }

        [Test]
        public void ASnakeDoneHolding_WithNoLeaning_PicksTheLineOnTheSameRoll()
        {
            _settings.RingWeightAfterSnake.Returns(1f);
            FormSnake();
            LetTheSnakeFinishItsHold();
            _service.Random01 = () => 0.4f;

            Arrange();

            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape, "ring 1 against a line's 1");
        }

        [Test]
        public void ARingFormedFromASnake_HoldsTheSlotOfItsHead()
        {
            _settings.RingWeightAfterSnake.Returns(9f);
            var snake = FormSnake();
            var head = snake.MemberList[0];
            var headSlot = head.SlotIndex.Value;
            LetTheSnakeFinishItsHold();
            _service.Random01 = () => 0.4f;

            Arrange();

            var ring = _service.Formations[0];
            Assert.AreEqual(FormationShape.Ring, ring.Shape);
            Assert.IsTrue(ring.MemberList.Any(m => m.SlotIndex.Value == headSlot), "the ring takes in the head's slot");
            Assert.GreaterOrEqual(HexCoordinates.DirectionBetween(ring.TurnCentre, headSlot), 0);
        }

        [Test]
        public void ARingFormedFromASnake_StartsTurningTheWayTheHeadLeads()
        {
            _settings.RingWeightAfterSnake.Returns(9f);
            var snake = FormSnake();
            var head = snake.MemberList[0];
            LetTheSnakeFinishItsHold();
            _service.Random01 = () => 0.4f;

            Arrange();

            var ring = _service.Formations[0];
            var assignments = ring.MemberList.Select(m => new FormationAssignment(m, m.SlotIndex.Value)).ToList();
            if (!FormationRing.LeadsAt(ring.TurnCentre, assignments, head.SlotIndex.Value, out var leadsClockwise))
            {
                Assert.Inconclusive("the head landed mid-arc, so no way leads");
            }

            Assert.AreEqual(leadsClockwise, ring.Clockwise);
        }

        // Round (3, 3), slot (0 east, then clockwise).
        private static Vector2Int Slot(int direction)
        {
            return HexCoordinates.Step(Centre, direction);
        }

        private static Vector2Int[] Around(params int[] directions)
        {
            return directions.Select(Slot).ToArray();
        }

        private static int Direction(IWriteableFormationMember member)
        {
            return HexCoordinates.DirectionBetween(Centre, member.SlotIndex.Value);
        }

        private Vector2 AlongTheRing(UnbreakableBalloonModel member, bool clockwise)
        {
            var slot = member.SlotIndex.Value;
            var tangent = FormationRing.Tangent(HexCoordinates.DirectionBetween(Centre, slot), clockwise);
            return _grid.IndexToWorldPosition(HexCoordinates.Step(slot, tangent)) - _grid.IndexToWorldPosition(slot);
        }

        private void AssertDirections(StandingFormation ring, IReadOnlyList<int> expected)
        {
            CollectionAssert.AreEqual(expected, ring.MemberList.Select(Direction).ToList());
        }

        // Unbreakables round (3, 3) on slots 0 to count - 1, where the plan costs nothing, so the ring forms there.
        private StandingFormation FormRing(int count = 4, bool clockwise = true)
        {
            _service.Random01 = () => clockwise ? 0f : 0.9f;
            AddUnbreakables(Around(Enumerable.Range(0, count).ToArray()));

            Arrange();

            var ring = _service.Formations[0];
            Assert.AreEqual(FormationShape.Ring, ring.Shape);
            Assert.AreEqual(Centre, ring.TurnCentre);
            Assert.AreEqual(clockwise, ring.Clockwise);
            return ring;
        }

        // A snake of four along row 2, formed alone in the pick; a line and a ring then both stand to change to.
        private StandingFormation FormSnake()
        {
            _settings.RingWeight.Returns(0f);
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            _settings.BeatSeconds.Returns(1.7f);
            AddUnbreakables(
                new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2));

            Arrange();

            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape);
            _settings.RingWeight.Returns(1f);
            _settings.HorizontalWeight.Returns(1f);
            return _service.Formations[0];
        }

        // The snake formed on the first cycle, a reshape; the cycles up to the next reshape hold it.
        private void LetTheSnakeFinishItsHold()
        {
            for (var cycle = 2; cycle <= _settings.ReshapeEveryCycles; cycle++)
            {
                Arrange();
            }

            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape, "held through its cycles");
        }

        private ProjectileModel Plow(object member)
        {
            var shot = new ProjectileModel { IsFree = true };
            _projectiles.Load(shot, null);
            _projectiles.Launch(shot);
            shot.Flight.PendingPierceHits.Add(new PendingPierceHit((IBalloonModel)member, Vector3.zero));
            return shot;
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

        private void AddUnbreakables(params Vector2Int[] slots)
        {
            foreach (var slot in slots)
            {
                var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
                _grid.Place(unbreakable, null, slot);
                _unbreakables.Add(unbreakable);
            }
        }

        // An unbreakable placed after the ring formed, so it stays out of it.
        private void Outsider(Vector2Int slot)
        {
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, slot);
        }
    }
}

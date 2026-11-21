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
    // A spinner is the only weighted shape unless a test says otherwise, so every formation here is one. It forms
    // where its unbreakables already stand: a line through (3, 3), flat, on slots 3, centre and 0 (west to east),
    // with two more each side for a spinner of five.
    [TestFixture]
    public class SpinnerFormationServiceTests
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
            _settings.SnakeHoldCycles.Returns(3);
            _settings.LineHoldCycles.Returns(3);
            _settings.BottomClearance.Returns(2);
            _settings.SpotChoices.Returns(1);
            _settings.SpinnerWeight.Returns(1f);
            _settings.SpinnerMaxMembers.Returns(5);
            _settings.SpinnerHoldCycles.Returns(6);
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
        public void ThreeFreeUnbreakables_FormASpinnerAboutTheMiddleOneOfTheLineTheyStandOn()
        {
            var spinner = FormSpinner();

            Assert.IsTrue(_service.HasTurningFormation);
            Assert.AreEqual(3, spinner.MemberList.Count);
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored && u.InFormation.Value));
            Assert.AreSame(_unbreakables[1], _grid.At(Centre), "the middle member stands on the centre");
        }

        [Test]
        public void FiveFreeUnbreakables_FormASpinnerOfFive()
        {
            var spinner = FormSpinner(5);

            Assert.AreEqual(5, spinner.MemberList.Count);
            Assert.IsTrue(_unbreakables.All(u => u.InFormation.Value));
        }

        [Test]
        public void FourFreeUnbreakables_FormASpinnerOfThree_AndLeaveOneFree()
        {
            _service.Random01 = () => 0f;
            AddUnbreakables(Line(1));
            AddUnbreakables(new Vector2Int(0, 7));

            Arrange();

            Assert.AreEqual(3, _service.Formations[0].MemberList.Count, "a spinner is three or five, never four");
            Assert.IsFalse(_unbreakables[3].InFormation.Value);
        }

        [Test]
        public void TwoFreeUnbreakables_FormNoSpinner()
        {
            AddUnbreakables(Slot(3, 1), Slot(0, 1));

            Arrange();

            Assert.IsFalse(_service.HasTurningFormation);
            Assert.AreEqual(0, _service.Formations.Count);
        }

        [Test]
        public void ASpinnerWeightOfZero_FormsNoSpinner()
        {
            _settings.SpinnerWeight.Returns(0f);
            AddUnbreakables(Line(1));

            Arrange();

            Assert.AreEqual(0, _service.Formations.Count, "a spinner is the only shape in the pick, and it has no weight");
        }

        [Test]
        public void TheSpinnersCentre_HoldsItsMiddleMember_AndNoHostIsHeldThere()
        {
            FormSpinner();

            Assert.AreEqual(0, _service.CoveredHosts.Count, "only a cover's balloon gets a ring of light");
            var middle = _unbreakables[1];
            Assert.AreEqual(Centre, middle.SlotIndex.Value);
            Assert.IsTrue(middle.InFormation.Value, "the centre is a member's slot, not a host's");
        }

        [TestCase(3, true)]
        [TestCase(3, false)]
        [TestCase(5, true)]
        [TestCase(5, false)]
        public void TurnOnTheBeat_EachCallTurnsTheLineSixtyDegrees_WithTheMiddleMemberInPlace(int count, bool clockwise)
        {
            FormSpinner(count, clockwise);
            var start = _unbreakables.ToDictionary(u => u, u => Locate(u.SlotIndex.Value));
            var turn = clockwise ? 1 : -1;

            for (var turns = 1; turns <= 3; turns++)
            {
                _service.TurnOnTheBeat(Relocate);

                foreach (var unbreakable in _unbreakables)
                {
                    var (direction, distance) = start[unbreakable];
                    Assert.AreEqual(
                        Slot(direction + (turns * turn), distance), unbreakable.SlotIndex.Value,
                        "after " + turns + " turns");
                    Assert.AreSame(unbreakable, _grid.At(unbreakable.SlotIndex.Value));
                }
            }

            Assert.AreEqual(Centre, _unbreakables[count / 2].SlotIndex.Value, "the middle member never moved");
        }

        [Test]
        public void TurnOnTheBeat_EveryOtherMemberHeadsTheWayItMoves_AndTheMiddleKeepsItsHeading()
        {
            FormSpinner(5);
            var middle = _unbreakables[2];
            var middleHeading = middle.FormationHeading;
            var before = _unbreakables.ToDictionary(u => u, u => _grid.IndexToWorldPosition(u.SlotIndex.Value));

            _service.TurnOnTheBeat(Relocate);

            foreach (var unbreakable in _unbreakables.Where(u => u != middle))
            {
                Vector2 moved = _grid.IndexToWorldPosition(unbreakable.SlotIndex.Value) - before[unbreakable];
                Assert.Less(Vector2.Angle(unbreakable.FormationHeading, moved), 1f);
            }

            Assert.AreEqual(middleHeading, middle.FormationHeading);
        }

        [Test]
        public void TurnOnTheBeat_AfterArranging_LeavesASpinnerArrangedInThatPassForTheNextBeat()
        {
            FormSpinner();
            var relocations = _relocations;

            _service.TurnOnTheBeat(Relocate, afterArranging: true);

            Assert.AreEqual(relocations, _relocations, "formed this pass: it waits for the beat");

            _service.TurnOnTheBeat(Relocate);
            Assert.AreEqual(relocations + 2, _relocations, "the beat itself turns the two members that move");
        }

        [Test]
        public void TurnOnTheBeat_AfterArranging_TurnsASpinnerFormedInAnEarlierPass()
        {
            FormSpinner();
            Arrange();
            var relocations = _relocations;

            _service.TurnOnTheBeat(Relocate, afterArranging: true);

            Assert.AreEqual(relocations + 2, _relocations);
        }

        [Test]
        public void ASpinnerBlockedOneWay_TurnsTheOtherWayTheSameBeat()
        {
            var spinner = FormSpinner();
            var start = _unbreakables.ToDictionary(u => u, u => Locate(u.SlotIndex.Value));
            Outsider(Slot(1, 1));

            _service.TurnOnTheBeat(Relocate);

            Assert.IsFalse(spinner.Clockwise, "it keeps the way that worked");
            AssertTurned(start, -1);
        }

        [Test]
        public void ASpinnerBlockedBothWays_MissesTheBeat_AndKeepsItsWay()
        {
            var spinner = FormSpinner();
            var start = _unbreakables.ToDictionary(u => u, u => Locate(u.SlotIndex.Value));
            var relocations = _relocations;
            Outsider(Slot(1, 1));
            Outsider(Slot(5, 1));

            _service.TurnOnTheBeat(Relocate);

            Assert.AreEqual(relocations, _relocations);
            Assert.IsTrue(spinner.Clockwise);
            AssertTurned(start, 0);
        }

        [Test]
        public void ASpinnerWithAMemberAwaitingALancesDischarge_StaysPut()
        {
            FormSpinner();
            var start = _unbreakables.ToDictionary(u => u, u => Locate(u.SlotIndex.Value));
            Plow(_unbreakables[0]);

            _service.TurnOnTheBeat(Relocate);

            AssertTurned(start, 0);
        }

        [Test]
        public void ABalloonInASpinnersWay_TradesPlacesWithTheTurn()
        {
            FormSpinner();
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, Slot(1, 1));

            _service.TurnOnTheBeat(Relocate);

            Assert.AreEqual(Slot(0, 1), balloon.SlotIndex.Value, "it goes to the slot the member left");
            Assert.IsTrue(_unbreakables.All(u => _grid.At(u.SlotIndex.Value) == u));
            Assert.AreSame(balloon, _grid.At(Slot(0, 1)));
        }

        [Test]
        public void ASpinner_TurnsTheOtherWayOnTheReverseCycle()
        {
            var spinner = FormSpinner();
            Arrange();
            Arrange();
            Assert.IsTrue(spinner.Clockwise, "cycles 2 and 3 of every 4");

            Arrange();

            Assert.IsFalse(spinner.Clockwise, "cycle 4 turns it round");
            var start = _unbreakables.ToDictionary(u => u, u => Locate(u.SlotIndex.Value));
            _service.TurnOnTheBeat(Relocate);
            AssertTurned(start, -1);
        }

        [Test]
        public void ASpinnerHoldsForItsHoldCycles_ThenChangesToAnotherShape()
        {
            FormSpinner();
            _settings.SpinnerWeight.Returns(0f);
            _settings.HorizontalWeight.Returns(1f);

            for (var cycle = 2; cycle <= 6; cycle++)
            {
                Arrange();
                Assert.AreEqual(FormationShape.Spinner, _service.CurrentShape, "formed on the first cycle, held to the sixth");
            }

            Arrange();

            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape);
            Assert.IsFalse(_service.HasTurningFormation);
        }

        [Test]
        public void ASpinnerThatEndsWithNothingToChangeTo_IsPlannedAgain()
        {
            _settings.SpinnerHoldCycles.Returns(1);
            FormSpinner();

            Arrange();

            Assert.AreEqual(FormationShape.Spinner, _service.CurrentShape, "no other shape is in the pick");
            Assert.IsTrue(_service.HasTurningFormation);
            Assert.AreEqual(Centre, _service.Formations[0].TurnCentre);
        }

        // The slot distance steps along direction from (3, 3): 0 east, then clockwise.
        private static Vector2Int Slot(int direction, int distance)
        {
            var slot = Centre;
            for (var i = 0; i < distance; i++)
            {
                slot = HexCoordinates.Step(slot, direction);
            }

            return slot;
        }

        // West to east along the flat line: arm slots each side of the centre.
        private static Vector2Int[] Line(int arm)
        {
            var slots = new List<Vector2Int>();
            for (var k = -arm; k <= arm; k++)
            {
                slots.Add(k < 0 ? Slot(3, -k) : Slot(0, k));
            }

            return slots.ToArray();
        }

        // A slot's direction and distance from the centre; (-1, 0) for the centre itself.
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
                    if (Slot(direction, distance) == slot)
                    {
                        return (direction, distance);
                    }
                }
            }

            Assert.Fail("slot " + slot + " is on none of the lines through the centre");
            return (-1, 0);
        }

        // Every member a number of 60 degree turns on from where it started (a negative number counts the other way).
        private void AssertTurned(IReadOnlyDictionary<UnbreakableBalloonModel, (int direction, int distance)> start, int turns)
        {
            foreach (var unbreakable in _unbreakables)
            {
                var (direction, distance) = start[unbreakable];
                Assert.AreEqual(Slot(direction + turns, distance), unbreakable.SlotIndex.Value);
                Assert.AreSame(unbreakable, _grid.At(unbreakable.SlotIndex.Value));
            }
        }

        // Unbreakables on the flat line through (3, 3), where the plan costs nothing, so the spinner forms there.
        private StandingFormation FormSpinner(int count = 3, bool clockwise = true)
        {
            _service.Random01 = () => clockwise ? 0f : 0.9f;
            AddUnbreakables(Line(count / 2));

            Arrange();

            var spinner = _service.Formations[0];
            Assert.AreEqual(FormationShape.Spinner, spinner.Shape);
            Assert.AreEqual(Centre, spinner.TurnCentre);
            Assert.AreEqual(clockwise, spinner.Clockwise);
            return spinner;
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

        // An unbreakable placed after the spinner formed, so it stays out of it.
        private void Outsider(Vector2Int slot)
        {
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, slot);
        }
    }
}

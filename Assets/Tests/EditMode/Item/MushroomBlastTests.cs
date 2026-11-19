using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Snipe;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using BalloonParty.Tests.Slots;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     Blasts and the meadow's mushrooms (<see cref="MushroomBlast" /> over the real
    ///     <see cref="MushroomBoard" />, <c>Plans/PLAN-MeadowMushrooms.md</c>, "Blasts take caps off"): a bomb
    ///     blows off the caps in the six slots next to it, where it would break an unbreakable, and sways the
    ///     others its blast reaches; a nuke's blast blows off the cap of each mushroom it is told of; a blown cap
    ///     is a cut one for everything that follows.
    /// </summary>
    [TestFixture]
    public class MushroomBlastTests
    {
        private const float Tolerance = 1e-4f;
        private const float BallRadius = 0.125f;

        // The shipped bomb's kill radius (ItemConfiguration's _bombRadius).
        private const float KillRadius = 1.25f;

        private static readonly Vector2Int BombSlot = new(3, 4);

        // Two of the six slots next to the bomb: the same row, and the row above.
        private static readonly Vector2Int Beside = new(2, 4);
        private static readonly Vector2Int Above = new(3, 3);

        // The second ring: two rows down (1.30 away) and two columns along (1.50 away).
        private static readonly Vector2Int TwoRowsDown = new(3, 6);
        private static readonly Vector2Int TwoAlong = new(5, 4);

        // Three columns along, 2.25 away: past the blast.
        private static readonly Vector2Int Far = new(0, 4);

        private SlotGrid _grid;
        private MushroomSettings _settings;
        private MushroomBoard _board;
        private List<MushroomCutMessage> _cuts;
        private List<MushroomSwayedMessage> _sways;
        private MushroomBlast _blast;

        [SetUp]
        public void SetUp()
        {
            _grid = new SlotGrid(new GridConfig(), new BalancePathHolder());
            _settings = new MushroomSettings();

            // The rule is held at the blast's own reach, a share of 1, where its distances are easy to follow.
            // The shipped share is wider, so the sway is seen; TheSwaysShare_... is what holds the widening.
            SetSwayShare(1f);
            _board = new MushroomBoard(_grid, _settings, new Origin());
            _cuts = new List<MushroomCutMessage>();
            _sways = new List<MushroomSwayedMessage>();
            _blast = NewBlast(_board, _settings);
        }

        [Test]
        public void TheBoardTheseTestsStandOn_IsTheShippedOne()
        {
            Assert.AreEqual(0.75f, Distance(BombSlot, Beside), Tolerance, "slots next to each other");
            Assert.AreEqual(0.75f, Distance(BombSlot, Above), 1e-3f);
            Assert.AreEqual(1.3f, Distance(BombSlot, TwoRowsDown), Tolerance);
            Assert.AreEqual(1.5f, Distance(BombSlot, TwoAlong), Tolerance);
            Assert.AreEqual(2.25f, Distance(BombSlot, Far), Tolerance);
            Assert.AreEqual(0.3f, _settings.Radius, Tolerance);
        }

        [Test]
        public void ABomb_BlowsOffTheCapsNextToIt_AndLeavesTheOthersOn()
        {
            PlaceAll();

            Bomb();

            CollectionAssert.AreEquivalent(new[] { Beside, Above }, _cuts.Select(cut => cut.Slot));
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, _board.Bumpers.Select(cap => cap.Id), "the rest are whole");
            Assert.AreEqual(2, _board.CutCount, "and the two are cut caps, to grow back as cut caps do");
        }

        [Test]
        public void ABlownCap_IsAnnouncedAsABlast_AtItsOwnCentre_BlownAwayFromTheBomb()
        {
            PlaceAll();

            Bomb();

            foreach (var cut in _cuts)
            {
                var cap = Centre(cut.Slot);
                Assert.AreEqual(MushroomCutCause.Blast, cut.Cause);
                Assert.AreEqual(0, cut.Charges, "a blast banks nothing");
                AssertVector(cap, cut.Point, "the cap's centre: a blast has no line");
                AssertVector((cap - Centre(BombSlot)).normalized, cut.Direction, "from the bomb to the cap");
                Assert.AreEqual(1f, cut.Direction.magnitude, Tolerance);
            }

            AssertVector(Vector2.left, _cuts.Single(cut => cut.Slot == Beside).Direction, "the cap to its left, leftward");
        }

        [Test]
        public void TheCapsItReachesWithoutTakingThem_Sway_AwayFromTheBomb_FainterWithDistance()
        {
            PlaceAll();

            Bomb();

            CollectionAssert.AreEquivalent(new[] { TwoRowsDown, TwoAlong }, _sways.Select(sway => sway.Slot));
            var near = _sways.Single(sway => sway.Slot == TwoRowsDown);
            var far = _sways.Single(sway => sway.Slot == TwoAlong);

            // The reach is the kill radius and the cap's own: 1.55. 1 at 0.75, 0 at 1.55.
            Assert.AreEqual((1.55f - 1.3f) / (1.55f - 0.75f), near.Strength, 1e-3f);
            Assert.AreEqual((1.55f - 1.5f) / (1.55f - 0.75f), far.Strength, 1e-3f);
            AssertVector(Vector2.down, near.Direction, "two rows down, pushed down");
            AssertVector(Vector2.right, far.Direction, "two along, pushed along");
            Assert.AreEqual(5, _board.Bumpers.Count + _board.CutCount, "a sway takes nothing off");
        }

        [Test]
        public void ACapPastTheBlast_NeitherGoesNorSways()
        {
            _grid.Place(new MushroomBumperModel(0), null, Far);

            Bomb();

            Assert.AreEqual(0, _cuts.Count);
            Assert.AreEqual(0, _sways.Count);
            Assert.AreEqual(1, _board.Bumpers.Count);
        }

        // The sway's share widens the sway and nothing else: which caps go is the bomb's rule, not a number of
        // the mushrooms'.
        [TestCase(2f, 3)]
        [TestCase(1f, 2)]
        [TestCase(0.9f, 1)]
        [TestCase(0f, 0)]
        public void TheSwaysShare_WidensOrSwitchesOffTheSway_AndTakesNoMoreCaps(float share, int swaying)
        {
            SetSwayShare(share);
            PlaceAll();

            Bomb();

            Assert.AreEqual(swaying, _sways.Count);
            CollectionAssert.AreEquivalent(new[] { Beside, Above }, _cuts.Select(cut => cut.Slot));
            foreach (var sway in _sways)
            {
                Assert.Greater(sway.Strength, 0f);
                Assert.LessOrEqual(sway.Strength, 1f);
            }
        }

        // A normal bomb breaks an unbreakable in the slots next to it and nowhere else, however near a balloon
        // of another slot drifted. So with caps: a mushroom two rows off is not taken by a bomb that went off
        // nearer to it than its slot's centre.
        [Test]
        public void ANormalBomb_TakesByTheSlot_NotByTheDistance()
        {
            _grid.Place(new MushroomBumperModel(0), null, TwoRowsDown);
            var nearer = Centre(BombSlot) + (Vector2.down * 0.2f);

            _blast.Bomb(nearer, BombSlot, false, KillRadius);

            Assert.AreEqual(0, _cuts.Count, "1.1 away, inside the kill radius, and not next to the bomb's slot");
            Assert.AreEqual(1, _sways.Count);
        }

        // A rainbow bomb breaks whatever has its centre inside the kill radius, and takes caps the same way.
        [Test]
        public void ARainbowBomb_TakesByItsKillRadius_AsItBreaksUnbreakables()
        {
            _grid.Place(new MushroomBumperModel(0), null, TwoRowsDown);
            _grid.Place(new MushroomBumperModel(1), null, Beside);
            _grid.Place(new MushroomBumperModel(2), null, TwoAlong);
            var nearer = Centre(BombSlot) + (Vector2.down * 0.2f);

            _blast.Bomb(nearer, BombSlot, true, KillRadius);

            CollectionAssert.AreEquivalent(new[] { TwoRowsDown, Beside }, _cuts.Select(cut => cut.Slot));
            CollectionAssert.AreEquivalent(new[] { TwoAlong }, _sways.Select(sway => sway.Slot), "1.51 away: swayed");
        }

        [Test]
        public void OnTheShippedLattice_ARainbowBombOnItsSlot_TakesTheSameSixAsANormalOne()
        {
            var id = 0;
            foreach (var slot in MushroomTestBoard.AllSlots())
            {
                if (slot != BombSlot)
                {
                    _grid.Place(new MushroomBumperModel(id++), null, slot);
                }
            }

            _blast.Bomb(Centre(BombSlot), BombSlot, true, KillRadius);
            var rainbow = _cuts.Select(cut => cut.Slot).ToArray();

            Assert.AreEqual(6, rainbow.Length);
            CollectionAssert.AreEquivalent(HexCoordinates.HexNeighborIndices(BombSlot.x, BombSlot.y), rainbow);
            Assert.AreEqual(12, _sways.Count, "and the ring past them sways");
        }

        [Test]
        public void ACapAlreadyGone_IsLeftAsItIs_NeitherAnnouncedNorSwayed()
        {
            PlaceAll();

            // Beside's cap, and then the second of the four left: the one two rows down.
            _board.Cut(0);
            _board.Cut(1);

            Bomb();

            CollectionAssert.AreEqual(new[] { Above }, _cuts.Select(cut => cut.Slot), "the one whole cap next to it");
            CollectionAssert.AreEqual(new[] { TwoAlong }, _sways.Select(sway => sway.Slot), "a bare stem does not sway");
            Assert.AreEqual(3, _board.CutCount);
        }

        [Test]
        public void ABoardWithoutMushrooms_HearsNothing()
        {
            Bomb();
            _blast.BlowOff(Beside, Centre(BombSlot));

            Assert.AreEqual(0, _cuts.Count);
            Assert.AreEqual(0, _sways.Count);
        }

        [Test]
        public void ABombAtTheBoardsEdge_TakesTheNeighboursThatExist()
        {
            var corner = new Vector2Int(5, 0);
            _grid.Place(new MushroomBumperModel(0), null, new Vector2Int(4, 0));
            _grid.Place(new MushroomBumperModel(1), null, new Vector2Int(5, 2));

            _blast.Bomb(Centre(corner), corner, false, KillRadius);

            CollectionAssert.AreEqual(new[] { new Vector2Int(4, 0) }, _cuts.Select(cut => cut.Slot));
            CollectionAssert.AreEqual(new[] { new Vector2Int(5, 2) }, _sways.Select(sway => sway.Slot));
        }

        // The nuke's part: one mushroom, told by the blast as its edge arrives.
        [Test]
        public void BlowOff_TakesTheCapOfTheMushroomInThatSlot_AwayFromTheBlastsCentre()
        {
            PlaceAll();
            var centre = new Vector2(-3f, -4f);

            _blast.BlowOff(Far, centre);

            Assert.AreEqual(1, _cuts.Count);
            Assert.AreEqual(Far, _cuts[0].Slot);
            Assert.AreEqual(MushroomCutCause.Blast, _cuts[0].Cause);
            AssertVector(Centre(Far), _cuts[0].Point, "the cap's centre");
            AssertVector((Centre(Far) - centre).normalized, _cuts[0].Direction, "from the nuke to the cap");
            Assert.AreEqual(4, _board.Bumpers.Count);
            Assert.AreEqual(0, _sways.Count, "a nuke sways nothing");
        }

        [Test]
        public void BlowOff_OfABareStem_OrOfASlotWithoutAMushroom_DoesNothing()
        {
            PlaceAll();
            _blast.BlowOff(Far, Vector2.zero);
            _cuts.Clear();

            _blast.BlowOff(Far, Vector2.zero);
            _blast.BlowOff(new Vector2Int(1, 9), Vector2.zero);

            Assert.AreEqual(0, _cuts.Count);
            Assert.AreEqual(1, _board.CutCount);
        }

        [Test]
        public void BlowOff_OfEveryMushroom_LeavesTheBoardBare_EachCapToldOnce()
        {
            PlaceAll();

            foreach (var slot in new[] { TwoAlong, Beside, Far, Above, TwoRowsDown })
            {
                _blast.BlowOff(slot, Vector2.zero);
            }

            Assert.AreEqual(0, _board.Bumpers.Count);
            Assert.AreEqual(5, _cuts.Count);
            CollectionAssert.AllItemsAreUnique(_cuts.Select(cut => cut.Slot));
        }

        // A blast that sits on a cap has no way to push it: straight up, a unit all the same.
        [Test]
        public void ABlastCentredOnACap_BlowsItStraightUp()
        {
            _grid.Place(new MushroomBumperModel(0), null, Far);

            _blast.BlowOff(Far, Centre(Far));

            AssertVector(Vector2.up, _cuts[0].Direction, "up");
        }

        // A field that keeps the cap (it could not cut it) leaves nothing to announce.
        [Test]
        public void ACapTheFieldDidNotCut_IsNotAnnounced()
        {
            var field = new StubbornField(
                new BumperCircle(0, Centre(Beside), MushroomTestBoard.ContactRadius), Beside);
            var blast = NewBlast(field, _settings);

            blast.Bomb(Centre(BombSlot), BombSlot, false, KillRadius);
            blast.BlowOff(Beside, Vector2.zero);

            Assert.AreEqual(2, field.CutsAsked);
            Assert.AreEqual(0, _cuts.Count);
        }

        // Everything that follows a cut follows a blast: the cap is back on the board's fifth beat, announced
        // by the regrow as any cut cap is.
        [Test]
        public void ABlownCap_GrowsBackOnTheFifthBeat_AsACutOneDoes()
        {
            PlaceAll();
            var regrow = new Regrow(_board);
            Bomb();
            regrow.Frame();

            for (var beat = 1; beat <= 4; beat++)
            {
                regrow.BeatFalls();
                Assert.AreEqual(3, _board.Bumpers.Count, $"still bare after beat {beat}");
            }

            regrow.BeatFalls();

            Assert.AreEqual(5, _board.Bumpers.Count);
            CollectionAssert.AreEquivalent(new[] { Beside, Above }, regrow.Regrown);
        }

        // A cap a lance cut and a bomb then reached: the bomb neither announces it again nor starts its wait
        // again, so it is back on its own fifth beat while the cap the bomb took waits for its own.
        [Test]
        public void ABlastOverACutCap_DoesNotStartItsWaitAgain()
        {
            _grid.Place(new MushroomBumperModel(0), null, Beside);
            _grid.Place(new MushroomBumperModel(1), null, Above);
            var regrow = new Regrow(_board);
            _board.Cut(0);
            regrow.Frame();
            regrow.BeatFalls();
            regrow.BeatFalls();
            regrow.BeatFalls();

            Bomb();
            regrow.Frame();

            CollectionAssert.AreEqual(new[] { Above }, _cuts.Select(cut => cut.Slot), "only the cap that was whole");
            Assert.AreEqual(0, _sways.Count, "a bare stem does not sway");

            regrow.BeatFalls();
            regrow.BeatFalls();
            CollectionAssert.AreEqual(new[] { Beside }, regrow.Regrown, "the lance's, on its fifth");

            regrow.BeatFalls();
            regrow.BeatFalls();
            Assert.AreEqual(1, regrow.Regrown.Count, "the bomb's has had four");
            regrow.BeatFalls();
            CollectionAssert.AreEqual(new[] { Beside, Above }, regrow.Regrown);
        }

        // A level's end, a retry and a reset clear the board and place new mushrooms: nothing blown on the old
        // board is cut on the new one, and nothing regrows onto it.
        [Test]
        public void ALevelChange_LeavesNothingCut()
        {
            PlaceAll();
            var regrow = new Regrow(_board);
            Bomb();
            _blast.BlowOff(Far, Vector2.zero);
            regrow.Frame();
            regrow.BeatFalls();

            foreach (var slot in new[] { Beside, Above, TwoRowsDown, TwoAlong, Far })
            {
                _grid.Remove(slot);
            }

            _grid.Place(new MushroomBumperModel(0), null, Above);
            _grid.Place(new MushroomBumperModel(1), null, Far);
            for (var beat = 0; beat < 8; beat++)
            {
                regrow.BeatFalls();
            }

            Assert.AreEqual(2, _board.Bumpers.Count, "the new level's caps, whole");
            Assert.AreEqual(0, _board.CutCount);
            Assert.AreEqual(0, regrow.Regrown.Count);
        }

        private void Bomb()
        {
            _blast.Bomb(Centre(BombSlot), BombSlot, false, KillRadius);
        }

        // Ids 0 to 4: the two next to the bomb, the two of the second ring, the far one.
        private void PlaceAll()
        {
            _grid.Place(new MushroomBumperModel(0), null, Beside);
            _grid.Place(new MushroomBumperModel(1), null, Above);
            _grid.Place(new MushroomBumperModel(2), null, TwoRowsDown);
            _grid.Place(new MushroomBumperModel(3), null, TwoAlong);
            _grid.Place(new MushroomBumperModel(4), null, Far);
        }

        private MushroomBlast NewBlast(IBumperField field, IMushroomSettings settings)
        {
            var cutPublisher = Substitute.For<IPublisher<MushroomCutMessage>>();
            cutPublisher.Publish(Arg.Do<MushroomCutMessage>(_cuts.Add));
            var swayedPublisher = Substitute.For<IPublisher<MushroomSwayedMessage>>();
            swayedPublisher.Publish(Arg.Do<MushroomSwayedMessage>(_sways.Add));
            return new MushroomBlast(field, _grid, settings, cutPublisher, swayedPublisher);
        }

        private void SetSwayShare(float share)
        {
            typeof(MushroomSettings)
                .GetField("_blast", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(_settings, new MushroomBlastTuning(share));
        }

        private Vector2 Centre(Vector2Int slot)
        {
            return _grid.IndexToWorldPosition(slot);
        }

        private float Distance(Vector2Int a, Vector2Int b)
        {
            return Vector2.Distance(Centre(a), Centre(b));
        }

        private static void AssertVector(Vector2 expected, Vector2 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, message);
            Assert.AreEqual(expected.y, actual.y, Tolerance, message);
        }

        // The regrow's controller over the board, with a beat the test lets fall.
        private sealed class Regrow
        {
            private readonly Beat _beat = new() { Count = 40 };
            private readonly MushroomRegrowController _controller;

            internal List<Vector2Int> Regrown { get; } = new();

            internal Regrow(MushroomBoard board)
            {
                var balls = Substitute.For<IProjectileRegistry>();
                balls.InFlightCount.Returns(0);
                var publisher = Substitute.For<IPublisher<MushroomRegrownMessage>>();
                publisher.Publish(Arg.Do<MushroomRegrownMessage>(message => Regrown.Add(message.Slot)));
                _controller = new MushroomRegrowController(board, _beat, balls, publisher);
            }

            internal void Frame()
            {
                _controller.Tick();
            }

            internal void BeatFalls()
            {
                _beat.Count++;
                _controller.Tick();
            }
        }

        // A field of one cap that never lets it go.
        private sealed class StubbornField : IBumperField
        {
            private readonly BumperCircle[] _caps;
            private readonly Vector2Int _slot;

            internal int CutsAsked { get; private set; }

            public IReadOnlyList<BumperCircle> Bumpers => _caps;

            internal StubbornField(BumperCircle cap, Vector2Int slot)
            {
                _caps = new[] { cap };
                _slot = slot;
            }

            public Vector2Int SlotAt(int index)
            {
                return _slot;
            }

            public void Cut(int index)
            {
                CutsAsked++;
            }
        }

        private sealed class GridConfig : ISlotGridConfig
        {
            public Vector2Int SlotsSize => MushroomTestBoard.GridSize;
            public Vector2 SlotSeparation => new(0.375f, 0.65f);
            public Vector2 SlotsOffset => new(5f, 3.25f);
        }

        private sealed class Origin : IShotOrigin
        {
            public bool IsAvailable => true;
            public Vector2 RestingPivot => Vector2.zero;
            public float ProjectileContactRadius => BallRadius;
        }

        private sealed class Beat : IBoardBeat
        {
            internal int Count;

            public int OffBeats(float share)
            {
                return Count;
            }
        }
    }
}

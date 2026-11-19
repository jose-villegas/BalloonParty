using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     The mushrooms as the ball meets them (<see cref="MushroomBoard" />): one bumper circle for every
    ///     cap on the grid, numbered by its id and kept in that order, each with its wall, rebuilt when the grid
    ///     or the ball's radius changes.
    /// </summary>
    [TestFixture]
    public class MushroomBoardTests
    {
        private const float Tolerance = 1e-5f;
        private const float BallRadius = 0.125f;

        private static readonly Vector2Int First = new(4, 3);
        private static readonly Vector2Int Second = new(1, 6);
        private static readonly Vector2Int Third = new(2, 2);

        private SlotGrid _grid;
        private MushroomSettings _settings;
        private Origin _shotOrigin;
        private MushroomBoard _board;

        [SetUp]
        public void SetUp()
        {
            _grid = new SlotGrid(new GridConfig(), new BalancePathHolder());
            _settings = new MushroomSettings();
            _shotOrigin = new Origin { ProjectileContactRadius = BallRadius };
            _board = new MushroomBoard(_grid, _settings, _shotOrigin);
        }

        [Test]
        public void Bumpers_OnABoardWithoutMushrooms_IsEmpty()
        {
            _grid.Place(new BushObstacleModel(), null, new Vector2Int(2, 4));
            _grid.Place(new BalloonModel(), null, new Vector2Int(3, 0));

            Assert.AreEqual(0, _board.Bumpers.Count, "only mushrooms are bumpers");
        }

        [Test]
        public void Bumpers_AreInChainOrder_WhereverTheMushroomsStandOnTheGrid()
        {
            // The grid is walked column by column, which would find them Second, Third, First.
            _grid.Place(new MushroomBumperModel(2), null, Third);
            _grid.Place(new MushroomBumperModel(0), null, First);
            _grid.Place(new MushroomBumperModel(1), null, Second);

            var bumpers = _board.Bumpers;

            Assert.AreEqual(3, bumpers.Count);
            for (var i = 0; i < bumpers.Count; i++)
            {
                Assert.AreEqual(i, bumpers[i].Id, "a bumper's id is its cap's own");
                Assert.AreEqual(i, bumpers[i].Wall, "and a cap placed by itself is its own wall");
            }

            Assert.AreEqual(First, _board.SlotAt(0));
            Assert.AreEqual(Second, _board.SlotAt(1));
            Assert.AreEqual(Third, _board.SlotAt(2));
        }

        [Test]
        public void Bumpers_StandAtTheSlotsCentre_WithTheBallsRadiusAddedToTheMushrooms()
        {
            _grid.Place(new MushroomBumperModel(0), null, First);

            var bumper = _board.Bumpers[0];

            var center = (Vector2)_grid.IndexToWorldPosition(First);
            Assert.AreEqual(center.x, bumper.Center.x, Tolerance);
            Assert.AreEqual(center.y, bumper.Center.y, Tolerance);
            Assert.AreEqual(_settings.Radius + BallRadius, bumper.Radius, Tolerance,
                "the radius is where the ball's centre is at the touch");
        }

        [Test]
        public void Bumpers_AfterTheGridChanges_AreRebuilt()
        {
            _grid.Place(new MushroomBumperModel(0), null, First);
            _grid.Place(new MushroomBumperModel(1), null, Second);
            Assert.AreEqual(2, _board.Bumpers.Count);

            _grid.Remove(Second);
            Assert.AreEqual(1, _board.Bumpers.Count, "a mushroom taken off the grid is gone from the board");
            Assert.AreEqual(First, _board.SlotAt(0));

            _grid.Remove(First);
            Assert.AreEqual(0, _board.Bumpers.Count, "a cleared level has no bumpers");

            _grid.Place(new MushroomBumperModel(0), null, Third);
            Assert.AreEqual(1, _board.Bumpers.Count, "and the next level's are picked up");
            Assert.AreEqual(Third, _board.SlotAt(0));
        }

        [Test]
        public void Bumpers_AGridChangeThatIsNotAMushroom_LeavesTheSameCircles()
        {
            _grid.Place(new MushroomBumperModel(0), null, First);
            _grid.Place(new MushroomBumperModel(1), null, Second);
            var before = _board.Bumpers;
            var firstCenter = before[0].Center;

            // A balloon arriving and one popping are grid changes too: the circles are read off the grid again.
            _grid.Place(new BalloonModel(), null, new Vector2Int(0, 0));
            _grid.Remove(new Vector2Int(0, 0));
            var after = _board.Bumpers;

            Assert.AreSame(before, after, "the board's own list, refilled in place");
            Assert.AreEqual(2, after.Count);
            Assert.AreEqual(firstCenter, after[0].Center);
            Assert.AreEqual(1, after[1].Id);
        }

        [Test]
        public void Bumpers_WhenTheBallsRadiusBecomesKnown_TakeIt()
        {
            // The thrower fills the radius in when it starts; a board read before that has it as zero.
            _shotOrigin.ProjectileContactRadius = 0f;
            _grid.Place(new MushroomBumperModel(0), null, First);
            Assert.AreEqual(_settings.Radius, _board.Bumpers[0].Radius, Tolerance);

            _shotOrigin.ProjectileContactRadius = BallRadius;

            Assert.AreEqual(_settings.Radius + BallRadius, _board.Bumpers[0].Radius, Tolerance,
                "no grid change was needed");
        }

        [Test]
        public void Bumpers_CarryTheWallEachCapStandsIn()
        {
            // Two caps of one wall and a cap of another, placed out of order.
            _grid.Place(new MushroomBumperModel(2, 1), null, Third);
            _grid.Place(new MushroomBumperModel(0, 0), null, First);
            _grid.Place(new MushroomBumperModel(1, 0), null, Second);

            var bumpers = _board.Bumpers;

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, new[] { bumpers[0].Id, bumpers[1].Id, bumpers[2].Id });
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, new[] { bumpers[0].Wall, bumpers[1].Wall, bumpers[2].Wall });
        }

        // A cut cap is no bumper: it has left the list when Cut returns, the list being the same one the caller
        // holds, and the caps left keep their ids, their walls and their slots. Its slot stays taken.
        [Test]
        public void Cut_TakesTheCapOutOfTheBumpersAtOnce_AndLeavesItsSlotTaken()
        {
            PlaceTwoWalls();
            var held = _board.Bumpers;

            _board.Cut(1);

            Assert.AreSame(held, _board.Bumpers);
            Assert.AreEqual(2, held.Count, "gone from the list a caller already holds");
            CollectionAssert.AreEqual(new[] { 0, 2 }, new[] { held[0].Id, held[1].Id });
            CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { held[0].Wall, held[1].Wall }, "a wall keeps its number");
            Assert.AreEqual(First, _board.SlotAt(0));
            Assert.AreEqual(Third, _board.SlotAt(1));
            Assert.IsInstanceOf<MushroomBumperModel>(_grid.At(Second), "the stem stands in its slot");
            Assert.AreEqual(1, _board.CutCount);
            Assert.AreEqual(Second, _board.CutSlotAt(0));
        }

        [Test]
        public void Cut_StaysCut_ThroughAGridChange()
        {
            PlaceTwoWalls();
            _board.Cut(0);

            _grid.Place(new BalloonModel(), null, new Vector2Int(0, 0));
            _grid.Remove(new Vector2Int(0, 0));

            CollectionAssert.AreEqual(new[] { 1, 2 }, new[] { _board.Bumpers[0].Id, _board.Bumpers[1].Id });
            Assert.AreEqual(1, _board.CutCount);
        }

        [Test]
        public void Cut_EveryCap_LeavesNoBumperAndEveryCapWaiting()
        {
            PlaceTwoWalls();

            _board.Cut(0);
            _board.Cut(0);
            _board.Cut(0);

            Assert.AreEqual(0, _board.Bumpers.Count);
            Assert.AreEqual(3, _board.CutCount);
        }

        [Test]
        public void Cut_AnIndexThatIsNoCap_DoesNothing()
        {
            PlaceTwoWalls();

            _board.Cut(3);
            _board.Cut(-1);

            Assert.AreEqual(3, _board.Bumpers.Count);
            Assert.AreEqual(0, _board.CutCount);
        }

        // The board keeps no clock: it is told each beat, and a cap is due once it has been told as many as the
        // settings ask (5 as shipped), counted from its own cut.
        [Test]
        public void CountBeat_ACutCapIsDue_AfterTheSettingsBeats_CountedFromItsOwnCut()
        {
            PlaceTwoWalls();
            Assert.AreEqual(5, _settings.RegrowBeats);
            _board.Cut(0);
            _board.CountBeat(_board.CutCount);
            _board.CountBeat(_board.CutCount);
            _board.Cut(0);

            for (var beat = 3; beat <= 4; beat++)
            {
                _board.CountBeat(_board.CutCount);
                Assert.IsFalse(_board.IsDueAt(0), $"beat {beat} of the first cap");
            }

            _board.CountBeat(_board.CutCount);
            Assert.IsTrue(_board.IsDueAt(0), "the fifth beat after its cut");
            Assert.IsFalse(_board.IsDueAt(1), "the second cap has seen three");

            _board.CountBeat(_board.CutCount);
            _board.CountBeat(_board.CutCount);
            Assert.IsTrue(_board.IsDueAt(1));
            Assert.IsTrue(_board.IsDueAt(0), "a cap that is due stays due until it is regrown");
        }

        // The cut caps are kept in the order they were cut, and a beat can be counted for the first of them
        // alone: the ones cut since the beat's reader last looked are not a beat nearer.
        [Test]
        public void CountBeat_ForTheFirstCapsOnly_LeavesTheOnesCutAfterThemTheirWholeWait()
        {
            PlaceTwoWalls();
            _board.Cut(2);
            _board.Cut(0);
            Assert.AreEqual(Third, _board.CutSlotAt(0), "the first cut is the first of the list");
            Assert.AreEqual(First, _board.CutSlotAt(1));

            for (var beat = 0; beat < _settings.RegrowBeats; beat++)
            {
                _board.CountBeat(1);
            }

            Assert.IsTrue(_board.IsDueAt(0));
            Assert.IsFalse(_board.IsDueAt(1), "no beat was counted for the second");
            _board.CountBeat(0);
            _board.CountBeat(-1);
            _board.CountBeat(9);
            Assert.IsFalse(_board.IsDueAt(1), "one beat, for however many caps were asked");
        }

        [Test]
        public void Regrow_PutsTheCapBack_WithItsIdItsWallItsSlotAndItsCircle()
        {
            PlaceTwoWalls();
            var whole = _board.Bumpers[1];
            _board.Cut(1);
            var waiting = _board.CutCircleAt(0);

            _board.Regrow(0);

            Assert.AreEqual(0, _board.CutCount);
            var bumpers = _board.Bumpers;
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, new[] { bumpers[0].Id, bumpers[1].Id, bumpers[2].Id });
            Assert.AreEqual(whole.Wall, bumpers[1].Wall);
            Assert.AreEqual(whole.Center, bumpers[1].Center);
            Assert.AreEqual(whole.Radius, bumpers[1].Radius);
            Assert.AreEqual(Second, _board.SlotAt(1));
            Assert.AreEqual(whole.Center, waiting.Center, "the circle a ball is looked for in while it waits");
            Assert.AreEqual(whole.Radius, waiting.Radius);
        }

        // A cap regrows whether or not the rest of its wall is whole: here the whole wall was cut.
        [Test]
        public void Regrow_ACapOfAWallThatWasCutWhole_ComesBackByItself()
        {
            PlaceTwoWalls();
            _board.Cut(0);
            _board.Cut(0);

            _board.Regrow(1);

            CollectionAssert.AreEqual(new[] { 1, 2 }, new[] { _board.Bumpers[0].Id, _board.Bumpers[1].Id });
            Assert.AreEqual(0, _board.Bumpers[0].Wall);
            Assert.AreEqual(First, _board.CutSlotAt(0), "the other is still waiting");
        }

        // A level's end, a retry and a reset take the caps off the grid and place new ones: nothing is left cut,
        // and nothing is waiting to grow back onto the new board.
        [Test]
        public void ACutCap_ThatLeavesTheGrid_IsForgotten_AndTheNextLevelsCapsAreWhole()
        {
            PlaceTwoWalls();
            _board.Cut(0);
            _board.Cut(0);

            _grid.Remove(First);
            _grid.Remove(Second);
            _grid.Remove(Third);
            _grid.Place(new MushroomBumperModel(0, 0), null, First);
            _grid.Place(new MushroomBumperModel(1, 1), null, Third);

            Assert.AreEqual(0, _board.CutCount);
            Assert.AreEqual(2, _board.Bumpers.Count, "a new cap in a slot whose cap was cut is whole");
            _board.CountBeat(_board.CutCount);
            Assert.AreEqual(2, _board.Bumpers.Count);
        }

        [Test]
        public void WithNothingCut_AskingAndCountingAllocateNothing()
        {
            PlaceTwoWalls();
            _board.Cut(0);
            _board.Regrow(0);
            var bumpers = AskAThousandTimes();

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            bumpers = AskAThousandTimes();
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
            Assert.AreEqual(3, bumpers.Count);
        }

        [Test]
        public void CuttingCountingAndRegrowing_AllocateNothing_OnceTheBoardHasBeenRead()
        {
            PlaceTwoWalls();
            CutCountAndRegrow(200);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            CutCountAndRegrow(200);
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
            Assert.AreEqual(3, _board.Bumpers.Count);
        }

        private IReadOnlyList<BumperCircle> AskAThousandTimes()
        {
            var bumpers = _board.Bumpers;
            for (var i = 0; i < 1000; i++)
            {
                if (_board.CutCount != 0)
                {
                    _board.CountBeat(_board.CutCount);
                }

                bumpers = _board.Bumpers;
            }

            return bumpers;
        }

        private void CutCountAndRegrow(int times)
        {
            for (var i = 0; i < times; i++)
            {
                _board.Cut(i % 3);
                _board.CountBeat(_board.CutCount);
                _board.Regrow(0);
            }
        }

        // Two caps of one wall and a cap of another: ids 0 and 1 in wall 0, id 2 in wall 1.
        private void PlaceTwoWalls()
        {
            _grid.Place(new MushroomBumperModel(0, 0), null, First);
            _grid.Place(new MushroomBumperModel(1, 0), null, Second);
            _grid.Place(new MushroomBumperModel(2, 1), null, Third);
        }

        // A plain config: a substitute would allocate on every read, and the grid reads it for every position.
        private sealed class GridConfig : ISlotGridConfig
        {
            public Vector2Int SlotsSize => MushroomTestBoard.GridSize;
            public Vector2 SlotSeparation => new(0.375f, 0.65f);
            public Vector2 SlotsOffset => new(5f, 3.25f);
        }

        // A plain origin: a substitute's would allocate on every read, and the board reads it on every ask.
        private sealed class Origin : IShotOrigin
        {
            public bool IsAvailable => true;
            public Vector2 RestingPivot => Vector2.zero;
            public float ProjectileContactRadius { get; set; }
        }
    }
}

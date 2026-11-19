using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Item.Snipe;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
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
    ///     A cut cap growing back on the board's beat (<see cref="MushroomRegrowController" /> over the real
    ///     <see cref="MushroomBoard" />, <c>Plans/PLAN-MeadowMushrooms.md</c>, decision J): on the fifth beat after
    ///     its cut, a beat later for each beat a ball is in its way, announced once, and nothing at all while no
    ///     cap is cut.
    /// </summary>
    [TestFixture]
    public class MushroomRegrowControllerTests
    {
        private const float BallRadius = 0.125f;

        private static readonly Vector2Int First = new(4, 3);
        private static readonly Vector2Int Second = new(1, 6);

        private SlotGrid _grid;
        private MushroomSettings _settings;
        private MushroomBoard _board;
        private Beat _beat;
        private IProjectileRegistry _balls;
        private List<Vector3> _ballsInFlight;
        private List<Vector2Int> _regrown;
        private MushroomRegrowController _controller;

        [SetUp]
        public void SetUp()
        {
            _grid = new SlotGrid(new GridConfig(), new BalancePathHolder());
            _settings = new MushroomSettings();
            _board = new MushroomBoard(_grid, _settings, new Origin());
            _grid.Place(new MushroomBumperModel(0, 0), null, First);
            _grid.Place(new MushroomBumperModel(1, 1), null, Second);

            _beat = new Beat { Count = 40 };
            _ballsInFlight = new List<Vector3>();
            _balls = Substitute.For<IProjectileRegistry>();
            _balls.InFlightCount.Returns(_ => _ballsInFlight.Count);
            _balls.InFlightPositionAt(Arg.Any<int>()).Returns(call => _ballsInFlight[call.Arg<int>()]);
            _regrown = new List<Vector2Int>();
            var publisher = Substitute.For<IPublisher<MushroomRegrownMessage>>();
            publisher.Publish(Arg.Do<MushroomRegrownMessage>(message => _regrown.Add(message.Slot)));
            _controller = new MushroomRegrowController(_board, _beat, _balls, publisher);
        }

        [Test]
        public void ACutCap_GrowsBackOnTheFifthBeatAfterItsCut_AndIsAnnouncedOnce()
        {
            Assert.AreEqual(5, _settings.RegrowBeats);
            _board.Cut(0);
            Frames(3);

            for (var beat = 1; beat <= 4; beat++)
            {
                BeatFalls();
                Assert.AreEqual(1, _board.Bumpers.Count, $"still cut after beat {beat}");
            }

            Frames(10);
            Assert.AreEqual(0, _regrown.Count, "and between beats");

            BeatFalls();

            Assert.AreEqual(2, _board.Bumpers.Count, "back on the fifth");
            Assert.AreEqual(0, _board.Bumpers[0].Id);
            CollectionAssert.AreEqual(new[] { First }, _regrown);

            BeatFalls();
            BeatFalls();
            Assert.AreEqual(1, _regrown.Count, "once");
        }

        // The first beat is the first one seen to fall after the frame of the cut: a beat that falls in the very
        // next frame counts, so the cap is gone a little over four beats at the least.
        [Test]
        public void TheFirstBeat_IsTheFirstToFallAfterTheFrameOfTheCut()
        {
            _board.Cut(0);
            _controller.Tick();

            for (var beat = 1; beat <= 5; beat++)
            {
                Assert.AreEqual(0, _regrown.Count, $"before beat {beat}");
                BeatFalls();
            }

            Assert.AreEqual(1, _regrown.Count);
        }

        // A beat that falls in the frame a cap is cut in is not the cap's, whichever came first: the cap is first
        // seen by the tick that reads that beat. The same for the first cap cut on a whole board and for one cut
        // while another is already waiting.
        [Test]
        public void ABeatInTheFrameOfTheCut_IsNotTheCaps_ForTheFirstCutAndForALaterOneAlike()
        {
            _board.Cut(0);
            BeatFalls();
            for (var beat = 1; beat <= 4; beat++)
            {
                BeatFalls();
            }

            Assert.AreEqual(0, _regrown.Count, "four beats since the frame of its cut");

            // The second cap is cut in the frame of the first cap's fifth beat.
            _board.Cut(0);
            BeatFalls();
            CollectionAssert.AreEqual(new[] { First }, _regrown, "the first cap's fifth");

            for (var beat = 1; beat <= 4; beat++)
            {
                BeatFalls();
                Assert.AreEqual(1, _regrown.Count, $"beat {beat} since the frame of the second cut");
            }

            BeatFalls();
            CollectionAssert.AreEqual(new[] { First, Second }, _regrown, "its own fifth, and not a beat sooner");
        }

        // A cap cut while another waits, in a frame with no beat: its count starts with the next one.
        [Test]
        public void ACapCutWhileAnotherWaits_CountsFromTheFirstBeatAfterItsOwnFrame()
        {
            _board.Cut(0);
            Frames(1);
            BeatFalls();
            BeatFalls();
            _board.Cut(0);
            Frames(1);

            BeatFalls();
            BeatFalls();
            BeatFalls();
            CollectionAssert.AreEqual(new[] { First }, _regrown);
            BeatFalls();
            Assert.AreEqual(1, _regrown.Count, "the second has seen four");
            BeatFalls();
            CollectionAssert.AreEqual(new[] { First, Second }, _regrown);
        }

        // A cap the lance cut is regrown round a ball's flight, by the game's own step: while the ball is inside
        // the circle the cap is to have, the cap waits; once the ball has flown on the cap comes back on the
        // next beat, and when the ball returns to it, it bounces off it.
        [Test]
        public void ACapDueWhileABallIsInsideIt_ComesBackOnceTheBallHasFlownOn_AndBouncesItWhenItReturns()
        {
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(MushroomTestBoard.WallsClockwise);
            var resolver = new ProjectileMotionResolver(
                config, new ProjectileTapResolver(config), _board, new TrueReflectRule(), _settings);
            var cap = _board.Bumpers[0];

            // A lance up through the cap from below cuts it.
            var lance = new ProjectileModel { Direction = Vector2.up, Speed = 1f, IsFree = true };
            lance.ShieldsRemaining.Value = 2;
            lance.IsPiercing.Value = true;
            resolver.Step(lance, cap.Center + (Vector2.down * 1f), 1f);
            Assert.AreEqual(1, _board.CutCount);
            Frames(1);

            // An ordinary ball stops for a moment over the stem, inside the circle, while the beats fall.
            var ball = new ProjectileModel { Direction = Vector2.up, Speed = 1f, IsFree = true };
            ball.ShieldsRemaining.Value = 2;
            var first = resolver.Step(ball, cap.Center + (Vector2.down * 0.2f), 0.1f);
            Assert.AreEqual(ProjectileStepOutcome.Moved, first.Outcome, "through where the cap stood");
            _ballsInFlight.Add(first.Position);
            for (var beat = 1; beat <= 6; beat++)
            {
                BeatFalls();
            }

            Assert.AreEqual(0, _regrown.Count, "due since the fifth beat, and held back by the ball");
            Assert.AreEqual(0, resolver.Step(ball, first.Position, 0.05f).BumperBounces, "nothing closed round it");

            // The ball flies on, out of the circle, and the cap is back on the next beat.
            var out1 = resolver.Step(ball, first.Position, 1f);
            Assert.AreEqual(ProjectileStepOutcome.Moved, out1.Outcome);
            Assert.Greater(((Vector2)out1.Position - cap.Center).magnitude, cap.Radius);
            _ballsInFlight[0] = out1.Position;
            BeatFalls();
            CollectionAssert.AreEqual(new[] { First }, _regrown);

            // Sent back at the cap, it bounces off it at the circle's edge.
            ball.Direction = Vector2.down;
            var back = resolver.Step(ball, out1.Position, 1f);
            Assert.AreEqual(1, back.BumperBounces);
            Assert.AreEqual(cap.Id, resolver.StepBumperBounces[0].BumperId);
            Assert.AreEqual(cap.Center.y + cap.Radius, resolver.StepBumperBounces[0].Point.y, 1e-4f);
        }

        // The beats the board counted before anything was cut are not the cap's.
        [Test]
        public void BeatsThatFellWhileNothingWasCut_AreNotCounted()
        {
            for (var i = 0; i < 9; i++)
            {
                BeatFalls();
            }

            _board.Cut(0);
            Frames(2);
            for (var beat = 1; beat <= 4; beat++)
            {
                BeatFalls();
            }

            Assert.AreEqual(0, _regrown.Count);
            BeatFalls();
            Assert.AreEqual(1, _regrown.Count);
        }

        [Test]
        public void ACapUnderABall_WaitsForTheNextBeat_AndTheOneAfter_UntilItsPlaceIsClear()
        {
            _board.Cut(0);
            Frames(1);
            var cap = _board.CutCircleAt(0);
            for (var beat = 1; beat <= 4; beat++)
            {
                BeatFalls();
            }

            // A ball's centre just inside the circle the cap will have: a ball cannot be put inside a bumper.
            _ballsInFlight.Add(cap.Center + new Vector2(cap.Radius - 0.01f, 0f));
            BeatFalls();
            Assert.AreEqual(0, _regrown.Count, "the fifth beat finds a ball in its way");
            BeatFalls();
            Assert.AreEqual(0, _regrown.Count, "and so does the sixth");

            _ballsInFlight.Clear();
            Frames(20);
            Assert.AreEqual(0, _regrown.Count, "it does not pop back off the beat once the ball has gone");

            BeatFalls();
            CollectionAssert.AreEqual(new[] { First }, _regrown);
            Assert.AreEqual(2, _board.Bumpers.Count);
        }

        [Test]
        public void ABallOutsideTheCapsCircle_IsNotInItsWay()
        {
            _board.Cut(0);
            Frames(1);
            var cap = _board.CutCircleAt(0);
            _ballsInFlight.Add(cap.Center + new Vector2(cap.Radius + 0.01f, 0f));
            _ballsInFlight.Add(cap.Center + new Vector2(0f, -3f));

            for (var beat = 1; beat <= 5; beat++)
            {
                BeatFalls();
            }

            Assert.AreEqual(1, _regrown.Count);
        }

        [Test]
        public void TwoCaps_CutBeatsApart_EachGrowBackOnTheirOwnFifth()
        {
            _board.Cut(0);
            Frames(1);
            BeatFalls();
            BeatFalls();
            _board.Cut(0);
            Frames(1);

            BeatFalls();
            BeatFalls();
            Assert.AreEqual(0, _regrown.Count);
            BeatFalls();
            CollectionAssert.AreEqual(new[] { First }, _regrown, "the first, five beats after its cut");

            BeatFalls();
            Assert.AreEqual(1, _regrown.Count);
            BeatFalls();
            CollectionAssert.AreEqual(new[] { First, Second }, _regrown, "the second, five after its own");
            Assert.AreEqual(2, _board.Bumpers.Count);
        }

        // A cap regrows whether or not the rest of its wall is whole, and every cap of a board cut bare comes back.
        [Test]
        public void ABoardCutBare_GrowsEveryCapBack()
        {
            _board.Cut(0);
            _board.Cut(0);
            Assert.AreEqual(0, _board.Bumpers.Count);
            Frames(1);

            for (var beat = 1; beat <= 5; beat++)
            {
                BeatFalls();
            }

            CollectionAssert.AreEquivalent(new[] { First, Second }, _regrown);
            CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { _board.Bumpers[0].Id, _board.Bumpers[1].Id });
        }

        // The beat's count falls back to nothing when a run starts over. That is no beat, and the count carries on
        // from there.
        [Test]
        public void ACountThatFalls_IsNoBeat()
        {
            _board.Cut(0);
            Frames(1);
            for (var beat = 1; beat <= 4; beat++)
            {
                BeatFalls();
            }

            _beat.Count = 0;
            _controller.Tick();
            Assert.AreEqual(0, _regrown.Count, "the fall is not the fifth beat");

            BeatFalls();
            Assert.AreEqual(1, _regrown.Count);
        }

        // A level's end, a retry and a reset clear the board and place new caps. Nothing is regrown onto the new
        // board and nothing is announced: the view of a pooled mushroom resets itself.
        [Test]
        public void CapsThatLeftTheBoardWhileCut_AreNotRegrown_AndNotAnnounced()
        {
            _board.Cut(0);
            _board.Cut(0);
            Frames(1);
            BeatFalls();

            _grid.Remove(First);
            _grid.Remove(Second);
            _grid.Place(new MushroomBumperModel(0, 0), null, Second);
            for (var beat = 0; beat < 8; beat++)
            {
                BeatFalls();
            }

            Assert.AreEqual(0, _regrown.Count);
            Assert.AreEqual(1, _board.Bumpers.Count, "the new level's cap, whole");

            // And a cap cut on the new board counts its own beats from its own cut.
            _board.Cut(0);
            Frames(1);
            for (var beat = 1; beat <= 4; beat++)
            {
                BeatFalls();
            }

            Assert.AreEqual(0, _regrown.Count);
            BeatFalls();
            CollectionAssert.AreEqual(new[] { Second }, _regrown);
        }

        [Test]
        public void WithNoCapCut_ATickReadsNoBeat_AsksAfterNoBall_AndAllocatesNothing()
        {
            _grid.Place(new BalloonModel(), null, new Vector2Int(0, 0));
            TickThroughTenBeats();
            var readsBefore = _beat.Reads;

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            TickThroughTenBeats();
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
            Assert.AreEqual(readsBefore, _beat.Reads, "the beat is not read while the board is whole");
            _ = _balls.DidNotReceive().InFlightCount;
            Assert.AreEqual(0, _regrown.Count);
        }

        [Test]
        public void WhileACapWaits_TicksAndBeatsAllocateNothing()
        {
            // A first cap is cut and waited out, to warm the runtime up; the second is the one measured.
            _board.Cut(0);
            Frames(2);
            BeatFalls();
            TickThroughThreeBeats();
            BeatFalls();
            Assert.AreEqual(1, _regrown.Count);
            _board.Cut(0);
            Frames(2);
            BeatFalls();

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            TickThroughThreeBeats();
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
            Assert.AreEqual(1, _regrown.Count, "still waiting");
            BeatFalls();
            Assert.AreEqual(2, _regrown.Count);
        }

        private void TickThroughTenBeats()
        {
            for (var frame = 0; frame < 1000; frame++)
            {
                _beat.Count += frame % 100 == 0 ? 1 : 0;
                _controller.Tick();
            }
        }

        private void TickThroughThreeBeats()
        {
            for (var beat = 0; beat < 3; beat++)
            {
                _beat.Count++;
                for (var frame = 0; frame < 100; frame++)
                {
                    _controller.Tick();
                }
            }
        }

        private void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _controller.Tick();
            }
        }

        // The board counts one more beat, and a frame reads it.
        private void BeatFalls()
        {
            _beat.Count++;
            _controller.Tick();
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
            public float ProjectileContactRadius => BallRadius;
        }

        private sealed class Beat : IBoardBeat
        {
            internal int Count;
            internal int Reads;

            public int OffBeats(float share)
            {
                Reads++;
                return Count;
            }
        }
    }
}

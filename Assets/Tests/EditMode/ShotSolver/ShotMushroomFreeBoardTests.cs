using System;
using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Solver;
using NUnit.Framework;
using UnityEngine;
using Board = BalloonParty.Tests.Slots.MushroomTestBoard;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     The simulator on boards with no mushrooms (phase 5 of <c>Plans/PLAN-MeadowMushrooms.md</c>): the meadow's
    ///     bumpers must cost such a board nothing and change nothing on it. Eight seeded boards of the shipped
    ///     grid (colours, toughs, unbreakables, rainbows) are flown from every angle of the thrower's aim grid,
    ///     and every flight's result, path and timeline is folded into one number a board.
    /// </summary>
    /// <remarks>
    ///     The number is held (<see cref="PreMushroomDigest" />). It was captured from the simulator as it stood
    ///     before the meadow's mushrooms: the solver of main <c>b85c863c2</c>, built in a worktree of its own and
    ///     flown over these same boards and angles, gave it, and so does today's. It is floating point folded to
    ///     the bit, and Mono and .NET round a flight's arithmetic differently, so there is one number a runtime,
    ///     each captured from the old simulator on that runtime. Also held, on any runtime: mushrooms no shot can
    ///     reach change no flight, to the bit, and a sweep over mushrooms allocates nothing.
    /// </remarks>
    [TestFixture]
    public class ShotMushroomFreeBoardTests
    {
        private const int Boards = 8;
        private const float ProjectileRadius = Board.BallRadius;
        private const float ProjectileSpeed = 9.869022f;
        private const int Shields = 3;
        private const string Rainbow = "Rainbow";

        // Every flight's result, path and timeline over the eight boards, folded into one number by the
        // simulator of main b85c863c2, the commit before the mushroom work: its Solver/ sources and everything
        // they call, compiled apart and flown from a copy of this fixture's own code. Today's simulator must
        // give the same number: a board without mushrooms flies as it did before the game had any.
        //
        // Two numbers, because the two runtimes these tests run on do not round alike. On Mono: the old
        // simulator flown by the Editor's own Mono (6.13, its mono.exe, outside the Editor), which is the
        // number the Editor's test run gives today's. On .NET: the old simulator flown by .NET 10, where the
        // suite is run outside the Editor. Board for board, old and new agreed on each.
        private const ulong PreMushroomDigestOnMono = 0x665E2DB291313350UL;
        private const ulong PreMushroomDigestOnDotNet = 0x40BD7F3BA0F6AE8CUL;

        private static readonly string[] Colors = { "Red", "Blue", "Green" };

        // Cruise, the sweep and the earned pierce all switched on, so the flights go through every branch the
        // bumpers were threaded past.
        private static readonly ShotCruiseConfig Cruise = new(
            wallBounceThreshold: 2, speedGainPerTap: 0.15f, maxSpeedMultiplier: 9.4f, piercingTapThreshold: 4,
            sweepEnabled: true, sweepTapThreshold: 2);

        private static ulong PreMushroomDigest =>
            Type.GetType("Mono.Runtime") != null ? PreMushroomDigestOnMono : PreMushroomDigestOnDotNet;

        [Test]
        public void EveryFlightOverBoardsWithoutMushrooms_IsTheFlightItWasBeforeTheMushrooms()
        {
            var total = 0UL;
            var flights = 0;
            for (var seed = 1; seed <= Boards; seed++)
            {
                var board = BuildBoard(seed);
                var digest = DigestOf(board, out var count);
                flights += count;
                total = (total * 1099511628211UL) ^ digest;
                Report($"Mushroom-free board {seed}: {board.Length} targets, {count} flights, digest {digest:X16}");
            }

            Report($"Mushroom-free boards: {flights} flights, digest of all {total:X16}");
            Assert.AreEqual(4088, flights);
            Assert.AreEqual(
                PreMushroomDigest, total,
                $"The flights fold to {total:X16}, not the {PreMushroomDigest:X16} the pre-mushroom simulator gave.");
        }

        // The layer's presence alone must change nothing: the same boards with four mushrooms standing outside
        // the walls, where no ball's line can enter their circles, fold to the same numbers as with none.
        [Test]
        public void MushroomsNoShotCanReach_ChangeNoFlight()
        {
            var settings = new MushroomSettings();
            var outside = new[]
            {
                new BumperCircle(0, new Vector2(-6f, 0f), Board.ContactRadius),
                new BumperCircle(1, new Vector2(6f, 1f), Board.ContactRadius),
                new BumperCircle(2, new Vector2(0f, 8f), Board.ContactRadius),
                new BumperCircle(3, new Vector2(1f, -9f), Board.ContactRadius),
            };
            var bumpers = new ShotBumperLayer(outside, new ForgivingKickRule(settings), settings.Rules, 1.6f);
            for (var seed = 1; seed <= Boards; seed++)
            {
                var board = BuildBoard(seed);

                Assert.AreEqual(DigestOf(board, out _), DigestOf(board, out _, bumpers), $"Board {seed}");
            }
        }

        // The bot runs thousands of flights a frame, so the mushrooms may add no allocation to one. A sweep over
        // a layout of four walls with nothing to pop (a pop's scoring boxes its damage flags, on a runtime that does
        // not optimise that away, and always did): bounces, kicks, a lance armed and the caps it cuts taken out of
        // the layer and put back for the next flight, and after a first sweep to warm the runtime up, not a byte.
        [Test]
        public void ASweepOverMushrooms_AllocatesNothing()
        {
            var settings = new MushroomSettings();
            var bumpers = new ShotBumperLayer(
                Board.Bumpers(Board.Layout(4, 1)), new ForgivingKickRule(settings), settings.Rules, 1.6f);
            var board = new[] { ShotBalloonSnapshot.ForToughTarget(new Vector2(1000f, 1000f), 0.1f, 0, int.MaxValue) };
            var workingSet = new ShotBalloonState[board.Length];
            var angles = AimAngleGrid.ResolveSweepSampleCount(5f, 175f, Board.AimStepDegrees, 1);
            var tally = default(ShotMushroomTally);
            for (var i = 0; i < angles; i++)
            {
                var flown = Fly(board, workingSet, AngleAt(i), null, null, bumpers).Mushrooms;
                tally.Bounces += flown.Bounces;
                tally.LancesArmed += flown.LancesArmed;
                tally.CapsCut += flown.CapsCut;
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < angles; i++)
            {
                Fly(board, workingSet, AngleAt(i), null, null, bumpers);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, $"{angles} flights, {tally.Bounces} bounces");
            Assert.Greater(tally.Bounces, 500, "the sweep bounced off the mushrooms");
            Assert.Greater(tally.LancesArmed, 0, "armed a lance");
            Assert.Greater(tally.CapsCut, 0, "and cut caps with it");
            Assert.AreEqual(7, bumpers.Circles.Count + CapsCutBy(board, workingSet, AngleAt(angles - 1), bumpers),
                "every cap is back for the next flight: the layer holds what the last one left, and no more is cut");
        }

        // The simulator's loop is handed a cap to cut only while the layer holds it: a cut takes the cap out, a
        // cut the layer refuses (an index that is no cap) changes nothing and says so, and no flight cuts more
        // caps than the board has, however long it flies.
        [Test]
        public void ASimulatedCut_TakesItsCapOutOfTheLayer_SoNoFlightCutsMoreCapsThanTheBoardHas()
        {
            var settings = new MushroomSettings();
            var caps = Board.Bumpers(Board.Layout(4, 1));
            var cuts = new List<int>();
            var bumpers = new ShotBumperLayer(caps, new ForgivingKickRule(settings), settings.Rules, 1.6f) { CutLog = cuts };

            Assert.IsFalse(bumpers.Cut(-1));
            Assert.IsFalse(bumpers.Cut(caps.Length));
            Assert.AreEqual(caps.Length, bumpers.Circles.Count, "a cut that is refused changes nothing");
            Assert.AreEqual(0, cuts.Count);
            Assert.IsTrue(bumpers.Cut(0));
            Assert.AreEqual(caps.Length - 1, bumpers.Circles.Count);

            // Lances with shields to spare, so each flies wall to wall several times over the same stems.
            var board = new[] { ShotBalloonSnapshot.ForToughTarget(new Vector2(1000f, 1000f), 0.1f, 0, int.MaxValue) };
            var workingSet = new ShotBalloonState[board.Length];
            var angles = AimAngleGrid.ResolveSweepSampleCount(5f, 175f, Board.AimStepDegrees, 1);
            var most = 0;
            for (var i = 0; i < angles; i += 3)
            {
                var direction = ShotBoardGather.DirectionFromDegrees(AngleAt(i));
                var result = ShotSimulator.Simulate(
                    board, Board.WallsClockwise, MuzzleFor(direction), direction, 6, ProjectileRadius, workingSet,
                    rippleDamage: 1, seed: ShotFlightSeed.Armed(1.6f), streakGrantsShields: false, bumpers: bumpers);

                Assert.AreEqual(cuts.Count, result.Mushrooms.CapsCut);
                Assert.AreEqual(cuts.Count, new HashSet<int>(cuts).Count, "no cap twice in one flight");
                Assert.AreEqual(caps.Length - cuts.Count, bumpers.Circles.Count);
                Assert.IsFalse(result.Capped, "and the flight ended at a wall, not at the cap on events");
                most = Mathf.Max(most, cuts.Count);
            }

            Assert.Greater(most, 2, "the lances did cut caps");
        }

        // The last flight flown again: what it cut, with the layer left as that flight left it.
        private static int CapsCutBy(
            ShotBalloonSnapshot[] board, ShotBalloonState[] workingSet, float angle, ShotBumperLayer bumpers)
        {
            return Fly(board, workingSet, angle, null, null, bumpers).Mushrooms.CapsCut;
        }

        internal static ulong DigestOf(ShotBalloonSnapshot[] board, out int flights, ShotBumperLayer bumpers = null)
        {
            var workingSet = new ShotBalloonState[board.Length];
            var path = new List<Vector2>(64);
            var times = new List<float>(64);
            var digest = 14695981039346656037UL;
            flights = AimAngleGrid.ResolveSweepSampleCount(5f, 175f, Board.AimStepDegrees, 1);
            for (var i = 0; i < flights; i++)
            {
                var result = Fly(board, workingSet, AngleAt(i), path, times, bumpers);
                Fold(ref digest, result.RawScore);
                Fold(ref digest, result.Pops);
                Fold(ref digest, result.ToughsCleared);
                Fold(ref digest, result.Events);
                Fold(ref digest, result.FinalStreak);
                Fold(ref digest, (result.Died ? 1 : 0) | (result.Capped ? 2 : 0) | (result.BoardCleared ? 4 : 0));
                Fold(ref digest, result.FinalColor == null ? -1 : Array.IndexOf(Colors, result.FinalColor));
                Fold(ref digest, path.Count);
                for (var p = 0; p < path.Count; p++)
                {
                    Fold(ref digest, BitConverter.SingleToInt32Bits(path[p].x));
                    Fold(ref digest, BitConverter.SingleToInt32Bits(path[p].y));
                    Fold(ref digest, BitConverter.SingleToInt32Bits(times[p]));
                }
            }

            return digest;
        }

        internal static float AngleAt(int index)
        {
            return AimAngleGrid.ResolveSweepAngle(index, 5f, 175f, Board.AimStepDegrees, 1);
        }

        internal static ShotSimulationResult Fly(
            ShotBalloonSnapshot[] board, ShotBalloonState[] workingSet, float degrees, List<Vector2> path,
            List<float> times, ShotBumperLayer bumpers = null)
        {
            var direction = ShotBoardGather.DirectionFromDegrees(degrees);
            return ShotSimulator.Simulate(
                board, Board.WallsClockwise, MuzzleFor(direction), direction, Shields, ProjectileRadius, workingSet,
                rippleDamage: 1, pathOut: path, projectileSpeed: ProjectileSpeed, cruiseConfig: Cruise,
                timestampsOut: times, allowedColors: Colors, rainbowColorId: Rainbow, streakGrantsShields: false,
                bumpers: bumpers);
        }

        // The muzzle sits half a unit ahead of the pivot along the aim, and the ball's collider 0.03 ahead of that.
        internal static Vector2 MuzzleFor(Vector2 direction)
        {
            return Board.Cannon + (direction * 0.53f);
        }

        // The shipped grid, each slot empty or holding a colour, a tough, an unbreakable or a rainbow, with the
        // contact radii the game's prefabs have.
        internal static ShotBalloonSnapshot[] BuildBoard(int seed)
        {
            var random = new System.Random(seed * 7919);
            var board = new List<ShotBalloonSnapshot>();
            foreach (var slot in Board.AllSlots())
            {
                var roll = random.NextDouble();
                var at = Board.SlotCenter(slot);
                if (roll < 0.45)
                {
                    continue;
                }

                if (roll < 0.85)
                {
                    board.Add(ShotBalloonSnapshot.ForColorTarget(at, 0.271f, Colors[random.Next(Colors.Length)], 1, 1));
                }
                else if (roll < 0.93)
                {
                    board.Add(ShotBalloonSnapshot.ForToughTarget(at, 0.325f, 2, 2 + random.Next(2)));
                }
                else if (roll < 0.98)
                {
                    board.Add(ShotBalloonSnapshot.ForToughTarget(at, 0.357f, 5, int.MaxValue));
                }
                else
                {
                    board.Add(ShotBalloonSnapshot.ForRainbowTarget(at, 0.271f, Rainbow, 1, 1));
                }
            }

            return board.ToArray();
        }

        internal static void Report(string line)
        {
            Debug.Log(line);
            Console.WriteLine(line);
        }

        private static void Fold(ref ulong digest, int value)
        {
            digest = (digest ^ (uint)value) * 1099511628211UL;
        }
    }
}

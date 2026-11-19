using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Preview;
using BalloonParty.Prediction;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Board = BalloonParty.Tests.Slots.MushroomTestBoard;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     The aim line against the flight (phase 5 of <c>Plans/PLAN-MeadowMushrooms.md</c>): on boards with
    ///     mushrooms, an unbreakable and walls, <see cref="PredictionTraceCalculator" /> bends where the shot
    ///     simulator's flight of the same shot turns (which <see cref="MushroomMirrorTests" /> holds to the game),
    ///     for an ordinary ball and for one loaded as a lance, and marks its pierce at the corner where the chain
    ///     arms the ball. A lance's line is not bent by a mushroom (it cuts the cap and flies on), and neither is
    ///     the Snipe's pierce corridor (<see cref="SnipeRangePreview" />).
    /// </summary>
    [TestFixture]
    public class MushroomAimLineTests
    {
        private const float UnbreakableRadius = 0.357f;

        // The thrower's pivot at rest, which every shot's line passes through.
        private static readonly Vector2 Cannon = new(0f, -4.6f);

        private IPredictionTraceConfig _config;
        private IProjectileFlightConfig _flightConfig;
        private MushroomSettings _settings;
        private Field _field;
        private Deflectors _deflectors;
        private PredictionTraceCalculator _calculator;
        private List<Vector3> _line;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IPredictionTraceConfig>();
            _config.SegmentLength.Returns(0.35f);
            _config.MaxReflections.Returns(8);
            _config.MaxSegments.Returns(600);
            _flightConfig = Substitute.For<IProjectileFlightConfig>();
            _flightConfig.LimitsClockwise.Returns(Board.WallsClockwise);
            _settings = new MushroomSettings();
            _field = new Field();
            _deflectors = new Deflectors();
            _calculator = new PredictionTraceCalculator(
                _config, _flightConfig, _deflectors, new NoPierceItems(), _field, new ForgivingKickRule(_settings),
                _settings);
            _line = new List<Vector3>();
        }

        // Layouts of the catalogue with one unbreakable, shots round the middle of the widest window and across
        // the arc: every corner of the line is where the flight turns, drawn on the skin of what it turned off.
        [Test]
        public void TheLine_BendsWhereTheFlightTurns([Values(false, true)] bool startsAsLance)
        {
            var corners = 0;
            var offMushrooms = 0;
            var armedOnTheLine = 0;
            var lines = 0;
            for (var seed = 1; seed <= 40; seed++)
            {
                var layout = Board.Layout(2 + (seed % 3), seed * 5);
                var bumpers = Board.Bumpers(layout);
                _field.Set(bumpers);
                var unbreakable = PlaceUnbreakable(seed, layout.Caps.Select(cap => cap.Slot).ToList());
                _deflectors.Set(new DeflectorCircle(unbreakable.Center, unbreakable.Radius));
                var sim = new MushroomSimGame(Board.WallsClockwise, bumpers, _settings, new ForgivingKickRule(_settings));
                var idealIndex = Board.MiddleOfTheWidestWindow(layout, _settings);
                var angles = AimAngleGrid.ResolveSweepSampleCount(5f, 175f, Board.AimStepDegrees, 1);
                for (var i = 0; i < angles; i++)
                {
                    if (Mathf.Abs(i - idealIndex) > 4 && i % 16 != seed % 16)
                    {
                        continue;
                    }

                    var degrees = AimAngleGrid.ResolveSweepAngle(i, 5f, 175f, Board.AimStepDegrees, 1);
                    var direction = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                    var origin = Cannon + (direction * (0.15f / direction.y));
                    _calculator.Calculate(origin, direction, Board.BallRadius, _line, out var end, startsAsLance);
                    var flight = sim.Fly(origin, direction, 8, startsAsLance, new[] { unbreakable });
                    var label = $"board {seed}, {degrees:F2} degrees, lance {startsAsLance}";

                    var held = AssertCornersAreTheFlightsTurns(flight, bumpers, unbreakable, end, label);
                    lines++;
                    corners += held;
                    for (var turn = 0; turn < held; turn++)
                    {
                        offMushrooms += flight.Turns[turn].Kind == MirrorTurnKind.Bumper ? 1 : 0;
                        if (flight.Turns[turn].Reward == MushroomReward.Arm)
                        {
                            armedOnTheLine++;
                            Assert.AreEqual(turn + 1, end.PierceStartIndex, $"{label}: the pierce starts where the chain arms");
                        }
                    }
                }
            }

            ShotMushroomFreeBoardTests.Report(
                $"Aim line against the flight, lance {startsAsLance}: {lines} lines, {corners} corners held, " +
                $"{offMushrooms} of them off mushrooms, {armedOnTheLine} lines armed by a chain on the way.");
            if (startsAsLance)
            {
                Assert.AreEqual(0, offMushrooms, "no corner of a lance's line is on a mushroom: it flies through them");
            }
            else
            {
                Assert.Greater(offMushrooms, 300, "the lines met mushrooms");
                Assert.Greater(armedOnTheLine, 20, "and some drew a chain to the bounce that arms");
            }
        }

        [Test]
        public void ALineLoadedAsALance_RunsStraightThroughTheMushroomAndTheUnbreakable()
        {
            // Straight up from under a mushroom, with an unbreakable in between: the lance's line ignores the
            // balloon, as it always did, and the mushroom too, whose cap the lance will cut.
            _flightConfig.LimitsClockwise.Returns(new Vector4(5f, 5f, -5f, -5f));
            _field.Set(new[] { new BumperCircle(0, new Vector2(0.2f, 2f), 0.425f) });
            _deflectors.Set(new DeflectorCircle(new Vector2(0f, 0f), UnbreakableRadius));

            _calculator.Calculate(new Vector3(0f, -3f, 0f), Vector3.up, Board.BallRadius, _line, out var end, true);

            Assert.AreEqual(0, end.PierceStartIndex, "a lance from its first point");
            Assert.AreEqual(5f, _line[1].y, 1e-4f, "the first corner is the wall behind them both");
            Assert.AreEqual(0f, _line[1].x, 1e-4f, "straight up");
            Assert.AreEqual(-5f, _line[2].y, 1e-4f, "and back down through both to the floor");
        }

        // The line of an ordinary ball that its chain arms: bent at each mushroom up to the corner that arms it,
        // and from that corner a lance's, straight through the mushrooms it would have gone on bouncing between.
        [Test]
        public void ALineItsChainArms_IsALancesFromThatCorner_AndRunsThroughTheMushroomsAfterIt()
        {
            _flightConfig.LimitsClockwise.Returns(new Vector4(5f, 5f, -5f, -5f));
            _field.Set(new[]
            {
                new BumperCircle(0, new Vector2(0f, 2f), 0.5f), new BumperCircle(1, new Vector2(0f, -2f), 0.5f),
            });
            _calculator = new PredictionTraceCalculator(
                _config, _flightConfig, _deflectors, new NoPierceItems(), _field, new TrueReflectRule(), _settings);

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _line, out var end);

            Assert.AreEqual(1.5f, _line[1].y, 1e-4f, "the upper mushroom");
            Assert.AreEqual(-1.5f, _line[2].y, 1e-4f, "the lower");
            Assert.AreEqual(1.5f, _line[3].y, 1e-4f, "the upper again: the third hit, which arms");
            Assert.AreEqual(3, end.PierceStartIndex, "a lance's line from that corner");
            Assert.AreEqual(-5f, _line[4].y, 1e-4f, "through the lower mushroom to the floor");
            Assert.AreEqual(5f, _line[5].y, 1e-4f, "and through the upper one to the top");
        }

        // A cap a lance has cut is not in the field, and bends no line: the line over a board with a cut cap is
        // the line over a board that cap never stood on, to the bit.
        [Test]
        public void ACutCap_BendsNoLine()
        {
            _flightConfig.LimitsClockwise.Returns(new Vector4(5f, 5f, -5f, -5f));
            var standing = new BumperCircle(1, new Vector2(-2f, 1f), 0.425f);
            var inTheWay = new BumperCircle(0, new Vector2(0.2f, 2f), 0.425f);
            _field.Set(new[] { standing });
            _calculator.Calculate(new Vector3(0f, -3f, 0f), Vector3.up, Board.BallRadius, _line, out var without);
            var expected = new List<Vector3>(_line);

            _field.Set(new[] { inTheWay, standing });
            _calculator.Calculate(new Vector3(0f, -3f, 0f), Vector3.up, Board.BallRadius, _line, out _);
            Assert.AreNotEqual(expected[1], _line[1], "whole, the cap bends the line");

            _field.Cut(0);
            _calculator.Calculate(new Vector3(0f, -3f, 0f), Vector3.up, Board.BallRadius, _line, out var afterTheCut);

            Assert.AreEqual(expected.Count, _line.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.IsTrue(expected[i].Equals(_line[i]), $"point {i}");
            }

            Assert.AreEqual(without.Kind, afterTheCut.Kind);
        }

        [Test]
        public void TheReflectionBudget_CountsAMushroom_AndALineThatEndsOnOneSaysSo()
        {
            // One reflection: the line turns at the first mushroom and ends where that leg meets the second.
            _config.MaxReflections.Returns(1);
            _flightConfig.LimitsClockwise.Returns(new Vector4(5f, 5f, -5f, -5f));
            _field.Set(new[]
            {
                new BumperCircle(0, new Vector2(0f, 2f), 0.5f), new BumperCircle(1, new Vector2(0f, -2f), 0.5f),
            });
            _calculator = new PredictionTraceCalculator(
                _config, _flightConfig, _deflectors, new NoPierceItems(), _field, new TrueReflectRule(), _settings);

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _line, out var end);

            Assert.AreEqual(3, _line.Count, "the start, the first mushroom, the second");
            Assert.AreEqual(1.5f, _line[1].y, 1e-4f);
            Assert.AreEqual(-1.5f, _line[2].y, 1e-4f);
            Assert.AreEqual(PredictionTraceEndKind.Bumper, end.Kind);
            Assert.AreEqual(1f, end.Normal.y, 1e-4f, "the surface it ended on faces back up the line");
            Assert.AreEqual(-1, end.PierceStartIndex, "two bounces arm nothing");
        }

        [Test]
        public void TheChainsCount_ComesFromTheBall_SoALineSeededAtTwoArmsOnItsFirstMushroom()
        {
            _flightConfig.LimitsClockwise.Returns(new Vector4(5f, 5f, -5f, -5f));
            _field.Set(new[] { new BumperCircle(0, new Vector2(0f, 2f), 0.5f) });
            var chain = default(MushroomChainState);
            chain.Count = _settings.Rules.ChainTarget - 1;
            chain.RememberLast(7);

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _line, out var seeded, false, chain);
            var seededPierce = seeded.PierceStartIndex;
            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _line, out var fresh);

            Assert.AreEqual(1, seededPierce, "the third mushroom of the ball's chain is the line's first corner");
            Assert.AreEqual(-1, fresh.PierceStartIndex, "a ball that has not flown has two more to go");
        }

        // With no mushroom on the board the line is the one it was: the calculator wired to an empty field and
        // the calculator built without one draw the same points, to the bit, off the same walls and deflector.
        [Test]
        public void WithNoMushrooms_TheLineIsTheOneItWas([Values(false, true)] bool startsAsLance)
        {
            _field.Set(new BumperCircle[0]);
            var plain = new PredictionTraceCalculator(_config, _flightConfig, _deflectors, new NoPierceItems());
            var plainLine = new List<Vector3>();
            var angles = AimAngleGrid.ResolveSweepSampleCount(5f, 175f, Board.AimStepDegrees, 1);
            for (var seed = 1; seed <= 6; seed++)
            {
                var unbreakable = PlaceUnbreakable(seed, new List<Vector2Int>());
                _deflectors.Set(new DeflectorCircle(unbreakable.Center, unbreakable.Radius));
                for (var i = 0; i < angles; i += 5)
                {
                    var degrees = AimAngleGrid.ResolveSweepAngle(i, 5f, 175f, Board.AimStepDegrees, 1);
                    var direction = new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad), 0f);
                    var origin = (Vector3)Cannon + (direction * (0.15f / direction.y));

                    _calculator.Calculate(origin, direction, Board.BallRadius, _line, out var end, startsAsLance);
                    plain.Calculate(origin, direction, Board.BallRadius, plainLine, out var plainEnd, startsAsLance);

                    Assert.AreEqual(plainLine.Count, _line.Count, $"board {seed}, {degrees:F2} degrees");
                    for (var p = 0; p < _line.Count; p++)
                    {
                        Assert.IsTrue(plainLine[p].Equals(_line[p]), $"board {seed}, {degrees:F2} degrees, point {p}");
                    }

                    Assert.AreEqual(plainEnd.Kind, end.Kind);
                    Assert.AreEqual(plainEnd.PierceStartIndex, end.PierceStartIndex);
                    Assert.IsTrue(plainEnd.Normal.Equals(end.Normal));
                }
            }
        }

        // The Snipe's corridor is a lance's run from the host to its first wall: on layouts of the catalogue it
        // is the simulated lance's own straight path from that point, through every cap in its way.
        [Test]
        public void TheSnipesCorridor_RunsStraightThroughTheMushroomsALanceCuts()
        {
            var preview = new SnipeRangePreview(_flightConfig);
            var shape = new ItemPreviewShape();
            var through = 0;
            for (var seed = 1; seed <= 40; seed++)
            {
                var layout = Board.Layout(2 + (seed % 3), seed * 5);
                var bumpers = Board.Bumpers(layout);
                _field.Set(bumpers);
                var sim = new MushroomSimGame(Board.WallsClockwise, bumpers, _settings, new ForgivingKickRule(_settings));
                var middle = Board.Degrees(Board.MiddleOfTheWidestWindow(layout, _settings));
                for (var offset = -6; offset <= 6; offset++)
                {
                    var degrees = middle + (offset * Board.AimStepDegrees);
                    var direction = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                    var host = Cannon + (direction * (0.3f / direction.y));
                    shape.Clear();
                    preview.BuildShape(
                        new ItemPreviewContext(host, Vector2Int.zero, direction, null, null, 0f), shape);
                    var flight = sim.Fly(host, direction, 0, true, new MirrorTarget[0]);
                    var label = $"board {seed}, {degrees:F2} degrees";

                    Assert.AreEqual(1, flight.Turns.Count, $"{label}: the lance turns nowhere before its wall");
                    Assert.AreEqual(MirrorTurnKind.Wall, flight.Turns[0].Kind);
                    Assert.AreEqual(1, shape.Strokes.Count, $"{label}: one stroke");
                    Assert.AreEqual(2, shape.Points.Count, $"{label}: a straight segment");
                    Assert.Less(Vector2.Distance(host, shape.Points[0]), 1e-5f, "it starts at the host");
                    Assert.Less(
                        Vector2.Distance(flight.Turns[0].Point, shape.Points[1]), 1e-3f, $"{label}: and ends at the wall");
                    through += flight.CapsCut.Count > 0 ? 1 : 0;
                }
            }

            Assert.Greater(through, 100, "the corridors ran through mushrooms");
        }

        // Each corner of the line, after its start, against the flight's turn of the same number: the same kind
        // of thing at the same place, the corner drawn on the skin of what the ball's centre turned off. Held
        // from a tenth of a millimetre, each corner at most 2000 times as far out as the widest before it, until
        // the two are half a millimetre apart (see MirrorTolerance), or until the flight meets a cap at its very
        // edge, where the line, a rounding error away, passes it by (see MushroomMirror.IsAGraze, whose bound it
        // is). Returns how many corners were held.
        private int AssertCornersAreTheFlightsTurns(
            MirrorFlight flight, BumperCircle[] bumpers, MirrorTarget unbreakable, PredictionTraceEnd end, string label)
        {
            var corners = end.Kind == PredictionTraceEndKind.OpenAir ? _line.Count - 2 : _line.Count - 1;
            var allowed = 1e-4f;
            var widest = 0f;
            for (var i = 0; i < corners && i < flight.Turns.Count; i++)
            {
                var turn = flight.Turns[i];

                // A step of the line that crosses two walls reflects off both at the first, where the flight
                // meets them one after the other: the line's own step, older than the mushrooms. The line is
                // held up to there.
                if (turn.Kind == MirrorTurnKind.Wall && IsNearACorner(turn.Point))
                {
                    return i;
                }

                var expected = turn.Point;
                if (turn.Kind == MirrorTurnKind.Bumper)
                {
                    var bumper = bumpers[turn.BumperId];
                    expected = bumper.Center
                        + ((turn.Point - bumper.Center).normalized * (bumper.Radius - Board.BallRadius));
                }
                else if (turn.Kind == MirrorTurnKind.Deflect)
                {
                    expected = unbreakable.Center + ((turn.Point - unbreakable.Center).normalized * unbreakable.Radius);
                }

                var apart = Vector2.Distance(expected, _line[i + 1]);
                if (apart > allowed && MushroomMirror.IsAGraze(flight, i, widest, bumpers, MushroomChainState.NoBumper))
                {
                    return i;
                }

                Assert.LessOrEqual(apart, allowed, $"{label}: corner {i + 1} against the flight's {turn}");
                widest = Mathf.Max(widest, apart);
                if (apart > 5e-4f)
                {
                    return i;
                }

                allowed = Mathf.Max(allowed, apart * 2000f);
            }

            return Mathf.Min(corners, flight.Turns.Count);
        }

        // Within one step of the line of two walls at once.
        private bool IsNearACorner(Vector2 point)
        {
            var walls = new WallLimits(Board.WallsClockwise);
            var reach = _config.SegmentLength;
            var nearVertical = point.x - walls.Left < reach || walls.Right - point.x < reach;
            var nearHorizontal = point.y - walls.Bottom < reach || walls.Top - point.y < reach;
            return nearVertical && nearHorizontal;
        }

        // One unbreakable on a slot no mushroom stands on, off the bottom rows.
        private static MirrorTarget PlaceUnbreakable(int seed, IReadOnlyList<Vector2Int> mushrooms)
        {
            var random = new System.Random(seed * 7907);
            while (true)
            {
                var slot = new Vector2Int(random.Next(Board.GridSize.x), 2 + random.Next(Board.GridSize.y - 2));
                var taken = false;
                for (var i = 0; i < mushrooms.Count; i++)
                {
                    taken |= mushrooms[i] == slot;
                }

                if (!taken)
                {
                    return new MirrorTarget(Board.SlotCenter(slot), UnbreakableRadius, int.MaxValue);
                }
            }
        }

        private sealed class Field : IBumperField
        {
            private readonly List<BumperCircle> _bumpers = new();

            public IReadOnlyList<BumperCircle> Bumpers => _bumpers;

            public Vector2Int SlotAt(int index)
            {
                return new Vector2Int(index, 0);
            }

            public void Cut(int index)
            {
                _bumpers.RemoveAt(index);
            }

            internal void Set(IReadOnlyList<BumperCircle> bumpers)
            {
                _bumpers.Clear();
                _bumpers.AddRange(bumpers);
            }
        }

        private sealed class Deflectors : IDeflectorField
        {
            private readonly List<DeflectorCircle> _circles = new();

            public void CollectDeflectors(List<DeflectorCircle> results)
            {
                results.Clear();
                results.AddRange(_circles);
            }

            internal void Set(DeflectorCircle circle)
            {
                _circles.Clear();
                _circles.Add(circle);
            }
        }

        private sealed class NoPierceItems : IPierceItemField
        {
            public void CollectPierceItems(List<PierceItemCircle> results)
            {
                results.Clear();
            }
        }
    }
}

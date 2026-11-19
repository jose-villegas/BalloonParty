using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     The flight of a ball on a board without mushrooms, pinned step by step to what
    ///     <see cref="ProjectileMotionResolver" /> did before it knew about mushrooms.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The numbers in <see cref="Golden" /> were not produced by the code under test. They were captured
    ///         from the resolver as it stood before the meadow's mushrooms (the parent of commit
    ///         <c>7b5c0da7c</c>): that file and its <c>ProjectileStep</c> were compiled beside the tests under
    ///         another namespace, the scripted flights below were flown through it, and every step was printed.
    ///         At capture the old and the new resolver agreed on every number to the bit.
    ///     </para>
    ///     <para>
    ///         The capture ran outside the Editor, so floats are held to <see cref="Tolerance" /> and not to the
    ///         bit: two runtimes may round a compound expression differently in its last place. Step kinds and
    ///         counters are held exactly. Whether the bumper field's mere presence changes anything is a
    ///         separate question with the same runtime on both sides, held to the bit by
    ///         <c>ProjectileMotionResolverBumperTests</c>.
    ///     </para>
    ///     <para>
    ///         Each flight is flown twice: by a resolver built without a bumper field, and by one wired to an
    ///         empty board with the forgiving kick, as the game builds it on every level but a meadow's.
    ///     </para>
    /// </remarks>
    [TestFixture]
    public class ProjectileMotionResolverGoldenTests
    {
        internal const string PlainWalls = "plain walls";
        internal const string Corner = "corner";
        internal const string DoomedGlide = "doomed glide";
        internal const string Cruise = "cruise";
        internal const string PierceDischarge = "pierce discharge";

        private const float Tolerance = 2e-5f;
        private const float FixedStep = 0.02f;

        // ProjectileFlightConfig.asset, as shipped.
        private const float BaseSpeed = 9.869022f;

        private static readonly Vector4 Walls = new(3.625f, 2.25f, -4.5f, -2.25f);

        internal static readonly string[] Flights = { PlainWalls, Corner, DoomedGlide, Cruise, PierceDischarge };

        [Test]
        public void AFlightWithNoBumperField_IsStepForStepWhatItWasBeforeTheMushrooms(
            [ValueSource(nameof(Flights))] string flight)
        {
            AssertGolden(flight, Fly(flight, config => Stepper(Plain(config))));
        }

        [Test]
        public void AFlightOverAnEmptyBoard_IsStepForStepWhatItWasBeforeTheMushrooms(
            [ValueSource(nameof(Flights))] string flight)
        {
            AssertGolden(flight, Fly(flight, config => Stepper(WiredToAnEmptyBoard(config))));
        }

        [Test]
        public void TheFlights_CoverWhatTheyAreNamedFor()
        {
            // A script that stopped reaching its wall, its corner or its discharge would pin nothing.
            Assert.IsTrue(Golden(PlainWalls).Exists(r => r.Kind == (int)ProjectileStepOutcome.Bounced));
            Assert.AreEqual((int)ProjectileStepOutcome.Bounced, Golden(Corner)[0].Kind, "both walls on the first step");
            Assert.AreEqual(2, Golden(Corner)[0].Shields, "a corner is one bounce and one shield, of three");
            var glide = Golden(DoomedGlide);
            Assert.AreEqual((int)ProjectileStepOutcome.Destroyed, glide[glide.Count - 1].Kind);
            Assert.IsTrue(Golden(Cruise).Exists(r => r.Cruising && r.Taps > 0), "taps minted while cruising");
            Assert.IsTrue(Golden(Cruise).Exists(r => r.Piercing), "and enough of them to arm the pierce");
            var pierce = Golden(PierceDischarge);
            Assert.IsTrue(pierce.Exists(r => r.Piercing) && !pierce[pierce.Count - 1].Piercing, "armed, then spent");
        }

        /// <summary>Flies a scripted flight through whatever <paramref name="build" /> makes of its config.</summary>
        internal static List<Row> Fly(string flight, Func<IProjectileFlightConfig, StepFunction> build)
        {
            var step = build(Config());
            return flight switch
            {
                PlainWalls => FlyPlainWalls(step),
                Corner => FlyCorner(step),
                DoomedGlide => FlyDoomedGlide(step),
                Cruise => FlyCruise(step),
                PierceDischarge => FlyPierceDischarge(step),
                _ => throw new ArgumentException(flight),
            };
        }

        internal static IProjectileFlightConfig Config()
        {
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(Walls);
            config.SpeedGainPerTap.Returns(0.3926874f);
            config.MaxSpeedMultiplier.Returns(9.4245f);
            config.CruiseTapCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            config.CruiseTapEaseDuration.Returns(0.25f);
            config.CruisePiercingTapThreshold.Returns(5);
            config.PierceArmRampCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            config.PierceArmRampDuration.Returns(0.5f);
            config.LastShieldApproachCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            config.LastShieldApproachDuration.Returns(0.8f);
            return config;
        }

        // Up and to the right off the bottom of the board: the right wall, the top, the left, in turn.
        private static List<Row> FlyPlainWalls(StepFunction step)
        {
            var model = NewModel(Heading(63f), shields: 6);
            return Steps(step, model, new Vector3(0.2f, -4f, 0f), 70, null);
        }

        // From just inside the top right corner, straight at it: the first step crosses both walls.
        private static List<Row> FlyCorner(StepFunction step)
        {
            var model = NewModel(Heading(45f), shields: 3);
            return Steps(step, model, new Vector3(2.2f, 3.575f, 0f), 8, null);
        }

        // No shield left and nothing in the way: the last breath, eased over a fixed time to the wall it dies on.
        private static List<Row> FlyDoomedGlide(StepFunction step)
        {
            var start = new Vector3(-1f, -2f, 0f);
            var model = NewModel(Heading(70f), shields: 0);
            model.IsLastShieldApproach.Value = true;
            model.Flight.SegmentStartPosition = start;
            model.Flight.SegmentElapsed = 0f;
            return Steps(step, model, start, 60, null);
        }

        // An empty board: after its first wall the view finds the corridor clear and starts the cruise, as
        // TryEnterCruise does, and every wall after that mints a tap, until the fifth arms the pierce.
        private static List<Row> FlyCruise(StepFunction step)
        {
            var model = NewModel(Heading(58f), shields: 12);
            return Steps(step, model, new Vector3(-0.4f, -4f, 0f), 150, (flown, last) =>
            {
                if (last.Kind == (int)ProjectileStepOutcome.Bounced && !flown.IsCruising.Value
                    && !flown.IsPiercing.Value && flown.Flight.ConsecutiveWallBounces >= 1)
                {
                    flown.BeginTapBeat();
                    flown.IsCruising.Value = true;
                }
            });
        }

        // A lance with a charge banked plows a tough before each of two walls: the first discharge spends the
        // pierce and the charge keeps it armed, the second ends it. The view clears what was plowed at each.
        private static List<Row> FlyPierceDischarge(StepFunction step)
        {
            var model = NewModel(Heading(110f), shields: 5);
            model.IsPiercing.Value = true;
            model.Flight.BankedPierceCharges = 1;
            var plowed = 0;
            return Steps(step, model, new Vector3(1f, -3.5f, 0f), 75, (flown, last) =>
            {
                if (last.Kind == (int)ProjectileStepOutcome.Bounced)
                {
                    // What SnipeItemHandler and ProjectileView do after a discharge.
                    if (flown.Flight.PendingPierceHits.Count > 0)
                    {
                        flown.Flight.PendingPierceHits.Clear();
                        flown.Flight.BankedPierceCharges = 0;
                    }
                }
                else if (plowed < 2 && flown.Flight.PendingPierceHits.Count == 0 && flown.IsPiercing.Value)
                {
                    plowed++;
                    flown.Flight.PendingPierceHits.Add(
                        new PendingPierceHit(Substitute.For<IBalloonModel>(), Vector3.zero));
                }
            });
        }

        // Flies up to a number of fixed steps, stopping when the ball dies. Before each step but the first the
        // script may do to the ball what the rest of the game would have done since the last one.
        private static List<Row> Steps(
            StepFunction step, ProjectileModel model, Vector3 start, int count,
            Action<ProjectileModel, Row> betweenSteps)
        {
            var rows = new List<Row>(count);
            var position = start;
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    betweenSteps?.Invoke(model, rows[i - 1]);
                }

                var result = step(model, position, FixedStep);
                rows.Add(new Row(result, model));
                if (result.Kind == (int)ProjectileStepOutcome.Destroyed)
                {
                    break;
                }

                position = result.Position;
            }

            return rows;
        }

        private static void AssertGolden(string flight, List<Row> flown)
        {
            var golden = Golden(flight);
            Assert.AreEqual(golden.Count, flown.Count, $"{flight}: steps flown");
            for (var i = 0; i < golden.Count; i++)
            {
                var expected = golden[i];
                var actual = flown[i];
                var at = $"{flight}, step {i}";
                Assert.AreEqual(expected.Kind, actual.Kind, at + ": step kind");
                Assert.AreEqual(expected.Shields, actual.Shields, at + ": shields");
                Assert.AreEqual(expected.EmptyWallBounces, actual.EmptyWallBounces, at + ": consecutive wall bounces");
                Assert.AreEqual(expected.Taps, actual.Taps, at + ": taps");
                Assert.AreEqual(expected.WallHits, actual.WallHits, at + ": wall hit sequence");
                Assert.AreEqual(expected.Cruising, actual.Cruising, at + ": cruising");
                Assert.AreEqual(expected.Piercing, actual.Piercing, at + ": piercing");
                AssertNear(expected.Position, actual.Position, at + ": position");
                AssertNear(expected.Direction, actual.Direction, at + ": direction");
                AssertNear(expected.Contact, actual.Contact, at + ": wall contact");
                Assert.AreEqual(expected.Speed, actual.Speed, Tolerance * BaseSpeed, at + ": speed");
            }
        }

        private static void AssertNear(Vector2 expected, Vector2 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, message + " (x)");
            Assert.AreEqual(expected.y, actual.y, Tolerance, message + " (y)");
        }

        private static ProjectileMotionResolver Plain(IProjectileFlightConfig config)
        {
            return new ProjectileMotionResolver(config, new ProjectileTapResolver(config));
        }

        private static ProjectileMotionResolver WiredToAnEmptyBoard(IProjectileFlightConfig config)
        {
            var settings = new MushroomSettings();
            var field = Substitute.For<IBumperField>();
            field.Bumpers.Returns(Array.Empty<BumperCircle>());
            return new ProjectileMotionResolver(
                config, new ProjectileTapResolver(config), field, new ForgivingKickRule(settings), settings);
        }

        private static StepFunction Stepper(ProjectileMotionResolver resolver)
        {
            return (model, position, deltaTime) =>
            {
                var step = resolver.Step(model, position, deltaTime);
                Assert.AreEqual(0, step.BumperBounces, "no mushrooms on this board");
                return new StepResult((int)step.Outcome, step.Position, step.WallContact, step.Direction, step.Speed);
            };
        }

        private static ProjectileModel NewModel(Vector2 direction, int shields)
        {
            var model = new ProjectileModel
            {
                Direction = direction,
                Speed = BaseSpeed,
                IsFree = true
            };
            model.ShieldsRemaining.Value = shields;
            return model;
        }

        private static Vector2 Heading(float degrees)
        {
            return new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        }

        private static Row R(
            int kind, float px, float py, float dx, float dy, float cx, float cy, float speed, int shields,
            int emptyWallBounces, int taps, int wallHits, bool cruising, bool piercing)
        {
            return new Row(
                kind, new Vector2(px, py), new Vector2(dx, dy), new Vector2(cx, cy), speed, shields,
                emptyWallBounces, taps, wallHits, cruising, piercing);
        }

        /// <summary>One fixed step by any resolver, old or new, in terms neither owns.</summary>
        internal delegate StepResult StepFunction(IWriteableProjectileModel model, Vector3 position, float deltaTime);

        internal readonly struct StepResult
        {
            public readonly int Kind;
            public readonly Vector3 Position;
            public readonly Vector3 Contact;
            public readonly Vector2 Direction;
            public readonly float Speed;

            public StepResult(int kind, Vector3 position, Vector3 contact, Vector2 direction, float speed)
            {
                Kind = kind;
                Position = position;
                Contact = contact;
                Direction = direction;
                Speed = speed;
            }
        }

        /// <summary>What one step left behind: the step itself, and the ball's counters after it.</summary>
        internal readonly struct Row
        {
            public readonly int Kind;
            public readonly Vector2 Position;
            public readonly Vector2 Direction;
            public readonly Vector2 Contact;
            public readonly float Speed;
            public readonly int Shields;
            public readonly int EmptyWallBounces;
            public readonly int Taps;
            public readonly int WallHits;
            public readonly bool Cruising;
            public readonly bool Piercing;

            public Row(in StepResult step, ProjectileModel model)
                : this(
                    step.Kind, step.Position, step.Direction, step.Contact, step.Speed, model.ShieldsRemaining.Value,
                    model.Flight.ConsecutiveWallBounces, model.Flight.TotalCruiseTaps, model.Flight.WallHitSequence,
                    model.IsCruising.Value, model.IsPiercing.Value)
            {
            }

            public Row(
                int kind, Vector2 position, Vector2 direction, Vector2 contact, float speed, int shields,
                int emptyWallBounces, int taps, int wallHits, bool cruising, bool piercing)
            {
                Kind = kind;
                Position = position;
                Direction = direction;
                Contact = contact;
                Speed = speed;
                Shields = shields;
                EmptyWallBounces = emptyWallBounces;
                Taps = taps;
                WallHits = wallHits;
                Cruising = cruising;
                Piercing = piercing;
            }
        }

        // Captured, not written: see the remarks on the class. One row a fixed step: the step's kind, the position
        // it ended at, the direction, the wall contact and the speed, then the ball's shields, its consecutive wall
        // bounces, its taps, its wall hit sequence, and whether it was cruising and piercing after the step.
        private static List<Row> Golden(string flight)
        {
            return flight switch
            {
                PlainWalls => GoldenPlainWalls(),
                Corner => GoldenCorner(),
                DoomedGlide => GoldenDoomedGlide(),
                Cruise => GoldenCruise(),
                PierceDischarge => GoldenPierceDischarge(),
                _ => throw new ArgumentException(flight),
            };
        }

        private static List<Row> GoldenPlainWalls()
        {
            return new List<Row>
            {
                R(0, 0.289608836f, -3.82413268f, 0.453990519f, 0.891006529f, 0.289608836f, -3.82413268f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 0.379217684f, -3.64826536f, 0.453990519f, 0.891006529f, 0.379217684f, -3.64826536f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 0.468826532f, -3.47239804f, 0.453990519f, 0.891006529f, 0.468826532f, -3.47239804f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 0.55843538f, -3.29653072f, 0.453990519f, 0.891006529f, 0.55843538f, -3.29653072f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 0.648044229f, -3.1206634f, 0.453990519f, 0.891006529f, 0.648044229f, -3.1206634f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 0.737653077f, -2.94479609f, 0.453990519f, 0.891006529f, 0.737653077f, -2.94479609f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 0.827261925f, -2.76892877f, 0.453990519f, 0.891006529f, 0.827261925f, -2.76892877f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 0.916870773f, -2.59306145f, 0.453990519f, 0.891006529f, 0.916870773f, -2.59306145f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.00647962f, -2.41719413f, 0.453990519f, 0.891006529f, 1.00647962f, -2.41719413f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.09608841f, -2.24132681f, 0.453990519f, 0.891006529f, 1.09608841f, -2.24132681f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.18569732f, -2.06545949f, 0.453990519f, 0.891006529f, 1.18569732f, -2.06545949f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.27530622f, -1.88959217f, 0.453990519f, 0.891006529f, 1.27530622f, -1.88959217f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.36491513f, -1.71372485f, 0.453990519f, 0.891006529f, 1.36491513f, -1.71372485f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.45452404f, -1.53785753f, 0.453990519f, 0.891006529f, 1.45452404f, -1.53785753f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.54413295f, -1.36199021f, 0.453990519f, 0.891006529f, 1.54413295f, -1.36199021f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.63374186f, -1.18612289f, 0.453990519f, 0.891006529f, 1.63374186f, -1.18612289f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.72335076f, -1.01025558f, 0.453990519f, 0.891006529f, 1.72335076f, -1.01025558f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.81295967f, -0.834388316f, 0.453990519f, 0.891006529f, 1.81295967f, -0.834388316f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.90256858f, -0.658521056f, 0.453990519f, 0.891006529f, 1.90256858f, -0.658521056f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 1.99217749f, -0.482653797f, 0.453990519f, 0.891006529f, 1.99217749f, -0.482653797f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 2.08178639f, -0.306786537f, 0.453990519f, 0.891006529f, 2.08178639f, -0.306786537f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(0, 2.1713953f, -0.130919278f, 0.453990519f, 0.891006529f, 2.1713953f, -0.130919278f, 9.86902237f, 6, 0, 0, 0, false, false),
                R(1, 2.23899579f, 0.0449479818f, -0.453990519f, 0.891006529f, 2.25f, 0.0449479818f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 2.14938688f, 0.220815241f, -0.453990519f, 0.891006529f, 2.14938688f, 0.220815241f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 2.05977798f, 0.396682501f, -0.453990519f, 0.891006529f, 2.05977798f, 0.396682501f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.97016907f, 0.57254976f, -0.453990519f, 0.891006529f, 1.97016907f, 0.57254976f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.88056016f, 0.74841702f, -0.453990519f, 0.891006529f, 1.88056016f, 0.74841702f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.79095125f, 0.924284279f, -0.453990519f, 0.891006529f, 1.79095125f, 0.924284279f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.70134234f, 1.10015154f, -0.453990519f, 0.891006529f, 1.70134234f, 1.10015154f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.61173344f, 1.27601886f, -0.453990519f, 0.891006529f, 1.61173344f, 1.27601886f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.52212453f, 1.45188618f, -0.453990519f, 0.891006529f, 1.52212453f, 1.45188618f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.43251562f, 1.6277535f, -0.453990519f, 0.891006529f, 1.43251562f, 1.6277535f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.34290671f, 1.80362082f, -0.453990519f, 0.891006529f, 1.34290671f, 1.80362082f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.25329781f, 1.97948813f, -0.453990519f, 0.891006529f, 1.25329781f, 1.97948813f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.1636889f, 2.15535545f, -0.453990519f, 0.891006529f, 1.1636889f, 2.15535545f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 1.07407999f, 2.33122277f, -0.453990519f, 0.891006529f, 1.07407999f, 2.33122277f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 0.984471142f, 2.50709009f, -0.453990519f, 0.891006529f, 0.984471142f, 2.50709009f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 0.894862294f, 2.68295741f, -0.453990519f, 0.891006529f, 0.894862294f, 2.68295741f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 0.805253446f, 2.85882473f, -0.453990519f, 0.891006529f, 0.805253446f, 2.85882473f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 0.715644598f, 3.03469205f, -0.453990519f, 0.891006529f, 0.715644598f, 3.03469205f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 0.62603575f, 3.21055937f, -0.453990519f, 0.891006529f, 0.62603575f, 3.21055937f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 0.536426902f, 3.38642669f, -0.453990519f, 0.891006529f, 0.536426902f, 3.38642669f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(0, 0.446818054f, 3.56229401f, -0.453990519f, 0.891006529f, 0.446818054f, 3.56229401f, 9.86902237f, 5, 1, 0, 1, false, false),
                R(1, 0.357209206f, 3.51183867f, -0.453990519f, -0.891006529f, 0.357209206f, 3.625f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, 0.267600358f, 3.33597136f, -0.453990519f, -0.891006529f, 0.267600358f, 3.33597136f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, 0.177991509f, 3.16010404f, -0.453990519f, -0.891006529f, 0.177991509f, 3.16010404f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, 0.0883826613f, 2.98423672f, -0.453990519f, -0.891006529f, 0.0883826613f, 2.98423672f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.00122618675f, 2.8083694f, -0.453990519f, -0.891006529f, -0.00122618675f, 2.8083694f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.0908350348f, 2.63250208f, -0.453990519f, -0.891006529f, -0.0908350348f, 2.63250208f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.180443883f, 2.45663476f, -0.453990519f, -0.891006529f, -0.180443883f, 2.45663476f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.270052731f, 2.28076744f, -0.453990519f, -0.891006529f, -0.270052731f, 2.28076744f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.359661579f, 2.10490012f, -0.453990519f, -0.891006529f, -0.359661579f, 2.10490012f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.449270427f, 1.9290328f, -0.453990519f, -0.891006529f, -0.449270427f, 1.9290328f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.538879275f, 1.75316548f, -0.453990519f, -0.891006529f, -0.538879275f, 1.75316548f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.628488123f, 1.57729816f, -0.453990519f, -0.891006529f, -0.628488123f, 1.57729816f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.718096972f, 1.40143085f, -0.453990519f, -0.891006529f, -0.718096972f, 1.40143085f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.80770582f, 1.22556353f, -0.453990519f, -0.891006529f, -0.80770582f, 1.22556353f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.897314668f, 1.04969621f, -0.453990519f, -0.891006529f, -0.897314668f, 1.04969621f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -0.986923516f, 0.873828948f, -0.453990519f, -0.891006529f, -0.986923516f, 0.873828948f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.07653236f, 0.697961688f, -0.453990519f, -0.891006529f, -1.07653236f, 0.697961688f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.16614127f, 0.522094429f, -0.453990519f, -0.891006529f, -1.16614127f, 0.522094429f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.25575018f, 0.346227169f, -0.453990519f, -0.891006529f, -1.25575018f, 0.346227169f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.34535909f, 0.17035991f, -0.453990519f, -0.891006529f, -1.34535909f, 0.17035991f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.43496799f, -0.00550734997f, -0.453990519f, -0.891006529f, -1.43496799f, -0.00550734997f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.5245769f, -0.181374609f, -0.453990519f, -0.891006529f, -1.5245769f, -0.181374609f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.61418581f, -0.357241869f, -0.453990519f, -0.891006529f, -1.61418581f, -0.357241869f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.70379472f, -0.533109128f, -0.453990519f, -0.891006529f, -1.70379472f, -0.533109128f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.79340363f, -0.708976388f, -0.453990519f, -0.891006529f, -1.79340363f, -0.708976388f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.88301253f, -0.884843647f, -0.453990519f, -0.891006529f, -1.88301253f, -0.884843647f, 9.86902237f, 4, 2, 0, 2, false, false),
                R(0, -1.97262144f, -1.06071091f, -0.453990519f, -0.891006529f, -1.97262144f, -1.06071091f, 9.86902237f, 4, 2, 0, 2, false, false),
            };
        }

        private static List<Row> GoldenCorner()
        {
            return new List<Row>
            {
                R(1, 2.16043091f, 3.53543091f, -0.70710665f, -0.70710665f, 2.25f, 3.625f, 9.86902237f, 2, 1, 0, 1, false, false),
                R(0, 2.02086186f, 3.39586186f, -0.70710665f, -0.70710665f, 2.02086186f, 3.39586186f, 9.86902237f, 2, 1, 0, 1, false, false),
                R(0, 1.88129282f, 3.25629282f, -0.70710665f, -0.70710665f, 1.88129282f, 3.25629282f, 9.86902237f, 2, 1, 0, 1, false, false),
                R(0, 1.74172378f, 3.11672378f, -0.70710665f, -0.70710665f, 1.74172378f, 3.11672378f, 9.86902237f, 2, 1, 0, 1, false, false),
                R(0, 1.60215473f, 2.97715473f, -0.70710665f, -0.70710665f, 1.60215473f, 2.97715473f, 9.86902237f, 2, 1, 0, 1, false, false),
                R(0, 1.46258569f, 2.83758569f, -0.70710665f, -0.70710665f, 1.46258569f, 2.83758569f, 9.86902237f, 2, 1, 0, 1, false, false),
                R(0, 1.32301664f, 2.69801664f, -0.70710665f, -0.70710665f, 1.32301664f, 2.69801664f, 9.86902237f, 2, 1, 0, 1, false, false),
                R(0, 1.1834476f, 2.5584476f, -0.70710665f, -0.70710665f, 1.1834476f, 2.5584476f, 9.86902237f, 2, 1, 0, 1, false, false),
            };
        }

        private static List<Row> GoldenDoomedGlide()
        {
            return new List<Row>
            {
                R(0, -1f, -2f, 0.342020154f, 0.939692616f, -1f, -2f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.948816657f, -1.859375f, 0.342020154f, 0.939692616f, -0.948816657f, -1.859375f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.897633374f, -1.71875f, 0.342020154f, 0.939692616f, -0.897633374f, -1.71875f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.84645009f, -1.578125f, 0.342020154f, 0.939692616f, -0.84645009f, -1.578125f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.795266747f, -1.4375f, 0.342020154f, 0.939692616f, -0.795266747f, -1.4375f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.744083405f, -1.296875f, 0.342020154f, 0.939692616f, -0.744083405f, -1.296875f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.692900121f, -1.15625f, 0.342020154f, 0.939692616f, -0.692900121f, -1.15625f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.641716838f, -1.015625f, 0.342020154f, 0.939692616f, -0.641716838f, -1.015625f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.590533495f, -0.875000119f, 0.342020154f, 0.939692616f, -0.590533495f, -0.875000119f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.539350212f, -0.734375238f, 0.342020154f, 0.939692616f, -0.539350212f, -0.734375238f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.488166869f, -0.593750119f, 0.342020154f, 0.939692616f, -0.488166869f, -0.593750119f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.436983585f, -0.453125238f, 0.342020154f, 0.939692616f, -0.436983585f, -0.453125238f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.385800302f, -0.312500238f, 0.342020154f, 0.939692616f, -0.385800302f, -0.312500238f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.334616959f, -0.171875119f, 0.342020154f, 0.939692616f, -0.334616959f, -0.171875119f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.283433616f, -0.0312501192f, 0.342020154f, 0.939692616f, -0.283433616f, -0.0312501192f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.232250333f, 0.109374762f, 0.342020154f, 0.939692616f, -0.232250333f, 0.109374762f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.18106699f, 0.25f, 0.342020154f, 0.939692616f, -0.18106699f, 0.25f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.129883647f, 0.390625f, 0.342020154f, 0.939692616f, -0.129883647f, 0.390625f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.078700304f, 0.53125f, 0.342020154f, 0.939692616f, -0.078700304f, 0.53125f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, -0.0275169611f, 0.671875238f, 0.342020154f, 0.939692616f, -0.0275169611f, 0.671875238f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.0236663818f, 0.812500238f, 0.342020154f, 0.939692616f, 0.0236663818f, 0.812500238f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.0748497248f, 0.953125238f, 0.342020154f, 0.939692616f, 0.0748497248f, 0.953125238f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.126033068f, 1.09375048f, 0.342020154f, 0.939692616f, 0.126033068f, 1.09375048f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.177216411f, 1.23437524f, 0.342020154f, 0.939692616f, 0.177216411f, 1.23437524f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.228399754f, 1.37500048f, 0.342020154f, 0.939692616f, 0.228399754f, 1.37500048f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.279582977f, 1.51562524f, 0.342020154f, 0.939692616f, 0.279582977f, 1.51562524f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.33076632f, 1.65625024f, 0.342020154f, 0.939692616f, 0.33076632f, 1.65625024f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.381949663f, 1.79687524f, 0.342020154f, 0.939692616f, 0.381949663f, 1.79687524f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.433132768f, 1.93749976f, 0.342020154f, 0.939692616f, 0.433132768f, 1.93749976f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.484316111f, 2.078125f, 0.342020154f, 0.939692616f, 0.484316111f, 2.078125f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.535499334f, 2.21874952f, 0.342020154f, 0.939692616f, 0.535499334f, 2.21874952f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.586682677f, 2.35937452f, 0.342020154f, 0.939692616f, 0.586682677f, 2.35937452f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.637865901f, 2.49999952f, 0.342020154f, 0.939692616f, 0.637865901f, 2.49999952f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.689049244f, 2.64062452f, 0.342020154f, 0.939692616f, 0.689049244f, 2.64062452f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.740232348f, 2.78124905f, 0.342020154f, 0.939692616f, 0.740232348f, 2.78124905f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.791415811f, 2.92187405f, 0.342020154f, 0.939692616f, 0.791415811f, 2.92187405f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.842598915f, 3.06249857f, 0.342020154f, 0.939692616f, 0.842598915f, 3.06249857f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.893782258f, 3.20312405f, 0.342020154f, 0.939692616f, 0.893782258f, 3.20312405f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.944965482f, 3.34374857f, 0.342020154f, 0.939692616f, 0.944965482f, 3.34374857f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 0.996148825f, 3.48437357f, 0.342020154f, 0.939692616f, 0.996148825f, 3.48437357f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(0, 1.04733205f, 3.62499809f, 0.342020154f, 0.939692616f, 1.04733205f, 3.62499809f, 9.86902237f, 0, 0, 0, 0, false, false),
                R(2, 1.11484075f, 3.625f, 0.342020154f, 0.939692616f, 1.11484075f, 3.625f, 9.86902237f, 0, 0, 0, 0, false, false),
            };
        }

        private static List<Row> GoldenCruise()
        {
            return new List<Row>
            {
                R(0, -0.295404315f, -3.8326118f, 0.529919267f, 0.848048091f, -0.295404315f, -3.8326118f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, -0.190808624f, -3.6652236f, 0.529919267f, 0.848048091f, -0.190808624f, -3.6652236f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, -0.0862129256f, -3.4978354f, 0.529919267f, 0.848048091f, -0.0862129256f, -3.4978354f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.0183827728f, -3.3304472f, 0.529919267f, 0.848048091f, 0.0183827728f, -3.3304472f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.122978471f, -3.163059f, 0.529919267f, 0.848048091f, 0.122978471f, -3.163059f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.22757417f, -2.9956708f, 0.529919267f, 0.848048091f, 0.22757417f, -2.9956708f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.332169861f, -2.82828259f, 0.529919267f, 0.848048091f, 0.332169861f, -2.82828259f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.436765552f, -2.66089439f, 0.529919267f, 0.848048091f, 0.436765552f, -2.66089439f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.541361272f, -2.49350619f, 0.529919267f, 0.848048091f, 0.541361272f, -2.49350619f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.645956993f, -2.32611799f, 0.529919267f, 0.848048091f, 0.645956993f, -2.32611799f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.750552714f, -2.15872979f, 0.529919267f, 0.848048091f, 0.750552714f, -2.15872979f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.855148435f, -1.99134171f, 0.529919267f, 0.848048091f, 0.855148435f, -1.99134171f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 0.959744155f, -1.82395363f, 0.529919267f, 0.848048091f, 0.959744155f, -1.82395363f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.06433988f, -1.65656555f, 0.529919267f, 0.848048091f, 1.06433988f, -1.65656555f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.16893554f, -1.48917747f, 0.529919267f, 0.848048091f, 1.16893554f, -1.48917747f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.2735312f, -1.32178938f, 0.529919267f, 0.848048091f, 1.2735312f, -1.32178938f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.37812686f, -1.1544013f, 0.529919267f, 0.848048091f, 1.37812686f, -1.1544013f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.48272252f, -0.987013221f, 0.529919267f, 0.848048091f, 1.48272252f, -0.987013221f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.58731818f, -0.819625139f, 0.529919267f, 0.848048091f, 1.58731818f, -0.819625139f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.69191384f, -0.652237058f, 0.529919267f, 0.848048091f, 1.69191384f, -0.652237058f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.7965095f, -0.484848946f, 0.529919267f, 0.848048091f, 1.7965095f, -0.484848946f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 1.90110517f, -0.317460835f, 0.529919267f, 0.848048091f, 1.90110517f, -0.317460835f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 2.00570083f, -0.150072724f, 0.529919267f, 0.848048091f, 2.00570083f, -0.150072724f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 2.11029649f, 0.0173153877f, 0.529919267f, 0.848048091f, 2.11029649f, 0.0173153877f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(0, 2.21489215f, 0.184703499f, 0.529919267f, 0.848048091f, 2.21489215f, 0.184703499f, 9.86902237f, 12, 0, 0, 0, false, false),
                R(1, 2.18051219f, 0.35209161f, -0.529919267f, 0.848048091f, 2.25f, 0.35209161f, 9.86902237f, 11, 1, 0, 1, false, false),
                R(0, 2.07591653f, 0.519479752f, -0.529919267f, 0.848048091f, 2.07591653f, 0.519479752f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.97132087f, 0.686867833f, -0.529919267f, 0.848048091f, 1.97132087f, 0.686867833f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.86672521f, 0.854255915f, -0.529919267f, 0.848048091f, 1.86672521f, 0.854255915f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.76212955f, 1.021644f, -0.529919267f, 0.848048091f, 1.76212955f, 1.021644f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.65753388f, 1.18903208f, -0.529919267f, 0.848048091f, 1.65753388f, 1.18903208f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.55293822f, 1.35642016f, -0.529919267f, 0.848048091f, 1.55293822f, 1.35642016f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.44834256f, 1.52380824f, -0.529919267f, 0.848048091f, 1.44834256f, 1.52380824f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.3437469f, 1.69119632f, -0.529919267f, 0.848048091f, 1.3437469f, 1.69119632f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.23915124f, 1.8585844f, -0.529919267f, 0.848048091f, 1.23915124f, 1.8585844f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.13455558f, 2.0259726f, -0.529919267f, 0.848048091f, 1.13455558f, 2.0259726f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 1.02995992f, 2.19336081f, -0.529919267f, 0.848048091f, 1.02995992f, 2.19336081f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 0.925364196f, 2.36074901f, -0.529919267f, 0.848048091f, 0.925364196f, 2.36074901f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 0.820768476f, 2.52813721f, -0.529919267f, 0.848048091f, 0.820768476f, 2.52813721f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 0.716172755f, 2.69552541f, -0.529919267f, 0.848048091f, 0.716172755f, 2.69552541f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 0.611577034f, 2.86291361f, -0.529919267f, 0.848048091f, 0.611577034f, 2.86291361f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 0.506981313f, 3.03030181f, -0.529919267f, 0.848048091f, 0.506981313f, 3.03030181f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 0.402385622f, 3.19769001f, -0.529919267f, 0.848048091f, 0.402385622f, 3.19769001f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 0.297789931f, 3.36507821f, -0.529919267f, 0.848048091f, 0.297789931f, 3.36507821f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(0, 0.19319424f, 3.53246641f, -0.529919267f, 0.848048091f, 0.19319424f, 3.53246641f, 9.86902237f, 11, 1, 0, 1, true, false),
                R(1, 0.0885985419f, 3.55014539f, -0.529919267f, -0.848048091f, 0.0885985419f, 3.625f, 9.86902237f, 10, 2, 1, 2, true, false),
                R(0, 0.0885985419f, 3.55014539f, -0.529919267f, -0.848048091f, 0.0885985419f, 3.55014539f, 0f, 10, 2, 1, 2, true, false),
                R(0, 0.0769450143f, 3.53149581f, -0.529919267f, -0.848048091f, 0.0769450143f, 3.53149581f, 1.09955704f, 10, 2, 1, 2, true, false),
                R(0, 0.0536379553f, 3.49419665f, -0.529919267f, -0.848048091f, 0.0536379553f, 3.49419665f, 2.19911408f, 10, 2, 1, 2, true, false),
                R(0, 0.0186773688f, 3.43824792f, -0.529919267f, -0.848048091f, 0.0186773688f, 3.43824792f, 3.29867125f, 10, 2, 1, 2, true, false),
                R(0, -0.0279367492f, 3.36364985f, -0.529919267f, -0.848048091f, -0.0279367492f, 3.36364985f, 4.39822817f, 10, 2, 1, 2, true, false),
                R(0, -0.0862043947f, 3.27040219f, -0.529919267f, -0.848048091f, -0.0862043947f, 3.27040219f, 5.49778509f, 10, 2, 1, 2, true, false),
                R(0, -0.15612556f, 3.15850496f, -0.529919267f, -0.848048091f, -0.15612556f, 3.15850496f, 6.59734201f, 10, 2, 1, 2, true, false),
                R(0, -0.237700254f, 3.02795815f, -0.529919267f, -0.848048091f, -0.237700254f, 3.02795815f, 7.69689894f, 10, 2, 1, 2, true, false),
                R(0, -0.330928504f, 2.87876177f, -0.529919267f, -0.848048091f, -0.330928504f, 2.87876177f, 8.79645634f, 10, 2, 1, 2, true, false),
                R(0, -0.435810268f, 2.7109158f, -0.529919267f, -0.848048091f, -0.435810268f, 2.7109158f, 9.89601231f, 10, 2, 1, 2, true, false),
                R(0, -0.552345514f, 2.52442026f, -0.529919267f, -0.848048091f, -0.552345514f, 2.52442026f, 10.9955692f, 10, 2, 1, 2, true, false),
                R(0, -0.680534303f, 2.31927538f, -0.529919267f, -0.848048091f, -0.680534303f, 2.31927538f, 12.0951262f, 10, 2, 1, 2, true, false),
                R(0, -0.820376635f, 2.09548092f, -0.529919267f, -0.848048091f, -0.820376635f, 2.09548092f, 13.1946831f, 10, 2, 1, 2, true, false),
                R(0, -0.966045737f, 1.86236167f, -0.529919267f, -0.848048091f, -0.966045737f, 1.86236167f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(0, -1.11171484f, 1.62924242f, -0.529919267f, -0.848048091f, -1.11171484f, 1.62924242f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(0, -1.25738394f, 1.39612317f, -0.529919267f, -0.848048091f, -1.25738394f, 1.39612317f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(0, -1.40305305f, 1.16300392f, -0.529919267f, -0.848048091f, -1.40305305f, 1.16300392f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(0, -1.54872215f, 0.929884613f, -0.529919267f, -0.848048091f, -1.54872215f, 0.929884613f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(0, -1.69439125f, 0.696765304f, -0.529919267f, -0.848048091f, -1.69439125f, 0.696765304f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(0, -1.84006035f, 0.463645995f, -0.529919267f, -0.848048091f, -1.84006035f, 0.463645995f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(0, -1.98572946f, 0.230526686f, -0.529919267f, -0.848048091f, -1.98572946f, 0.230526686f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(0, -2.13139868f, -0.00259262323f, -0.529919267f, -0.848048091f, -2.13139868f, -0.00259262323f, 13.7444639f, 10, 2, 1, 2, true, false),
                R(1, -2.2229321f, -0.235711932f, 0.529919267f, -0.848048091f, -2.25f, -0.235711932f, 13.7444639f, 9, 3, 2, 3, true, false),
                R(0, -2.2229321f, -0.235711932f, 0.529919267f, -0.848048091f, -2.2229321f, -0.235711932f, 0f, 9, 3, 2, 3, true, false),
                R(0, -2.20799279f, -0.259619981f, 0.529919267f, -0.848048091f, -2.20799279f, -0.259619981f, 1.40959239f, 9, 3, 2, 3, true, false),
                R(0, -2.17811394f, -0.307436079f, 0.529919267f, -0.848048091f, -2.17811394f, -0.307436079f, 2.81918478f, 9, 3, 2, 3, true, false),
                R(0, -2.13329577f, -0.379160225f, 0.529919267f, -0.848048091f, -2.13329577f, -0.379160225f, 4.22877741f, 9, 3, 2, 3, true, false),
                R(0, -2.07353806f, -0.474792391f, 0.529919267f, -0.848048091f, -2.07353806f, -0.474792391f, 5.63836956f, 9, 3, 2, 3, true, false),
                R(0, -1.99884105f, -0.594332576f, 0.529919267f, -0.848048091f, -1.99884105f, -0.594332576f, 7.04796171f, 9, 3, 2, 3, true, false),
                R(0, -1.9092046f, -0.737780809f, 0.529919267f, -0.848048091f, -1.9092046f, -0.737780809f, 8.45755386f, 9, 3, 2, 3, true, false),
                R(0, -1.80462873f, -0.905137122f, 0.529919267f, -0.848048091f, -1.80462873f, -0.905137122f, 9.86714649f, 9, 3, 2, 3, true, false),
                R(0, -1.68511355f, -1.09640145f, 0.529919267f, -0.848048091f, -1.68511355f, -1.09640145f, 11.2767382f, 9, 3, 2, 3, true, false),
                R(0, -1.55065894f, -1.31157386f, 0.529919267f, -0.848048091f, -1.55065894f, -1.31157386f, 12.6863308f, 9, 3, 2, 3, true, false),
                R(0, -1.40126491f, -1.55065429f, 0.529919267f, -0.848048091f, -1.40126491f, -1.55065429f, 14.0959225f, 9, 3, 2, 3, true, false),
                R(0, -1.23693144f, -1.81364274f, 0.529919267f, -0.848048091f, -1.23693144f, -1.81364274f, 15.5055151f, 9, 3, 2, 3, true, false),
                R(0, -1.05765867f, -2.10053921f, 0.529919267f, -0.848048091f, -1.05765867f, -2.10053921f, 16.9151077f, 9, 3, 2, 3, true, false),
                R(0, -0.870916128f, -2.39938974f, 0.529919267f, -0.848048091f, -0.870916128f, -2.39938974f, 17.6199055f, 9, 3, 2, 3, true, false),
                R(0, -0.684173584f, -2.69824028f, 0.529919267f, -0.848048091f, -0.684173584f, -2.69824028f, 17.6199055f, 9, 3, 2, 3, true, false),
                R(0, -0.49743104f, -2.99709082f, 0.529919267f, -0.848048091f, -0.49743104f, -2.99709082f, 17.6199055f, 9, 3, 2, 3, true, false),
                R(0, -0.310688496f, -3.29594135f, 0.529919267f, -0.848048091f, -0.310688496f, -3.29594135f, 17.6199055f, 9, 3, 2, 3, true, false),
                R(0, -0.123945951f, -3.59479189f, 0.529919267f, -0.848048091f, -0.123945951f, -3.59479189f, 17.6199055f, 9, 3, 2, 3, true, false),
                R(0, 0.0627965927f, -3.89364243f, 0.529919267f, -0.848048091f, 0.0627965927f, -3.89364243f, 17.6199055f, 9, 3, 2, 3, true, false),
                R(0, 0.249539137f, -4.19249296f, 0.529919267f, -0.848048091f, 0.249539137f, -4.19249296f, 17.6199055f, 9, 3, 2, 3, true, false),
                R(0, 0.436281681f, -4.4913435f, 0.529919267f, -0.848048091f, 0.436281681f, -4.4913435f, 17.6199055f, 9, 3, 2, 3, true, false),
                R(1, 0.623024225f, -4.20980597f, 0.529919267f, 0.848048091f, 0.623024225f, -4.5f, 17.6199055f, 8, 4, 3, 4, true, false),
                R(0, 0.623024225f, -4.20980597f, 0.529919267f, 0.848048091f, 0.623024225f, -4.20980597f, 0f, 8, 4, 3, 4, true, false),
                R(0, 0.641249478f, -4.18063927f, 0.529919267f, 0.848048091f, 0.641249478f, -4.18063927f, 1.7196275f, 8, 4, 3, 4, true, false),
                R(0, 0.677700043f, -4.12230635f, 0.529919267f, 0.848048091f, 0.677700043f, -4.12230635f, 3.439255f, 8, 4, 3, 4, true, false),
                R(0, 0.73237586f, -4.03480673f, 0.529919267f, 0.848048091f, 0.73237586f, -4.03480673f, 5.15888262f, 8, 4, 3, 4, true, false),
                R(0, 0.80527699f, -3.91814065f, 0.529919267f, 0.848048091f, 0.80527699f, -3.91814065f, 6.87851f, 8, 4, 3, 4, true, false),
                R(0, 0.896403372f, -3.77230787f, 0.529919267f, 0.848048091f, 0.896403372f, -3.77230787f, 8.5981369f, 8, 4, 3, 4, true, false),
                R(0, 1.00575495f, -3.59730864f, 0.529919267f, 0.848048091f, 1.00575495f, -3.59730864f, 10.3177643f, 8, 4, 3, 4, true, false),
                R(0, 1.13333189f, -3.39314294f, 0.529919267f, 0.848048091f, 1.13333189f, -3.39314294f, 12.0373917f, 8, 4, 3, 4, true, false),
                R(0, 1.27913404f, -3.15981054f, 0.529919267f, 0.848048091f, 1.27913404f, -3.15981054f, 13.757019f, 8, 4, 3, 4, true, false),
                R(0, 1.44316149f, -2.89731169f, 0.529919267f, 0.848048091f, 1.44316149f, -2.89731169f, 15.4766455f, 8, 4, 3, 4, true, false),
                R(0, 1.62541425f, -2.60564637f, 0.529919267f, 0.848048091f, 1.62541425f, -2.60564637f, 17.1962738f, 8, 4, 3, 4, true, false),
                R(0, 1.82589221f, -2.2848146f, 0.529919267f, 0.848048091f, 1.82589221f, -2.2848146f, 18.9158993f, 8, 4, 3, 4, true, false),
                R(0, 2.04459548f, -1.93481624f, 0.529919267f, 0.848048091f, 2.04459548f, -1.93481624f, 20.6355267f, 8, 4, 3, 4, true, false),
                R(1, 2.22758865f, -1.57023454f, -0.529919267f, 0.848048091f, 2.25f, -1.57023454f, 21.4953442f, 7, 5, 4, 5, true, false),
                R(0, 2.22758865f, -1.57023454f, -0.529919267f, 0.848048091f, 2.22758865f, -1.57023454f, 0f, 7, 5, 4, 5, true, false),
                R(0, 2.20607758f, -1.53580952f, -0.529919267f, 0.848048091f, 2.20607758f, -1.53580952f, 2.02966285f, 7, 5, 4, 5, true, false),
                R(0, 2.16305518f, -1.46695948f, -0.529919267f, 0.848048091f, 2.16305518f, -1.46695948f, 4.0593257f, 7, 5, 4, 5, true, false),
                R(0, 2.09852171f, -1.36368442f, -0.529919267f, 0.848048091f, 2.09852171f, -1.36368442f, 6.08898878f, 7, 5, 4, 5, true, false),
                R(0, 2.01247716f, -1.22598433f, -0.529919267f, 0.848048091f, 2.01247716f, -1.22598433f, 8.11865139f, 7, 5, 4, 5, true, false),
                R(0, 1.90492141f, -1.05385911f, -0.529919267f, 0.848048091f, 1.90492141f, -1.05385911f, 10.1483145f, 7, 5, 4, 5, true, false),
                R(0, 1.77585459f, -0.847308934f, -0.529919267f, 0.848048091f, 1.77585459f, -0.847308934f, 12.1779766f, 7, 5, 4, 5, true, false),
                R(0, 1.62527657f, -0.606333733f, -0.529919267f, 0.848048091f, 1.62527657f, -0.606333733f, 14.2076387f, 7, 5, 4, 5, true, false),
                R(0, 1.45318735f, -0.330933511f, -0.529919267f, 0.848048091f, 1.45318735f, -0.330933511f, 16.2373009f, 7, 5, 4, 5, true, false),
                R(0, 1.25958705f, -0.0211082399f, -0.529919267f, 0.848048091f, 1.25958705f, -0.0211082399f, 18.266964f, 7, 5, 4, 5, true, false),
                R(0, 1.04447556f, 0.323142081f, -0.529919267f, 0.848048091f, 1.04447556f, 0.323142081f, 20.296627f, 7, 5, 4, 5, true, false),
                R(0, 0.807852983f, 0.701817393f, -0.529919267f, 0.848048091f, 0.807852983f, 0.701817393f, 22.3262882f, 7, 5, 4, 5, true, false),
                R(0, 0.549719214f, 1.11491776f, -0.529919267f, 0.848048091f, 0.549719214f, 1.11491776f, 24.3559513f, 7, 5, 4, 5, true, false),
                R(0, 0.280829847f, 1.54523063f, -0.529919267f, 0.848048091f, 0.280829847f, 1.54523063f, 25.3707867f, 7, 5, 4, 5, true, false),
                R(0, 0.0119404793f, 1.9755435f, -0.529919267f, 0.848048091f, 0.0119404793f, 1.9755435f, 25.3707867f, 7, 5, 4, 5, true, false),
                R(0, -0.256948888f, 2.40585637f, -0.529919267f, 0.848048091f, -0.256948888f, 2.40585637f, 25.3707867f, 7, 5, 4, 5, true, false),
                R(0, -0.525838256f, 2.83616924f, -0.529919267f, 0.848048091f, -0.525838256f, 2.83616924f, 25.3707867f, 7, 5, 4, 5, true, false),
                R(0, -0.794727623f, 3.26648211f, -0.529919267f, 0.848048091f, -0.794727623f, 3.26648211f, 25.3707867f, 7, 5, 4, 5, true, false),
                R(1, -1.06361699f, 3.55320501f, -0.529919267f, -0.848048091f, -1.06361699f, 3.625f, 25.3707867f, 6, 6, 5, 6, true, true),
                R(0, -1.33250642f, 3.12289214f, -0.529919267f, -0.848048091f, -1.33250642f, 3.12289214f, 25.3707867f, 6, 6, 5, 6, true, true),
                R(0, -1.60303879f, 2.68994999f, -0.529919267f, -0.848048091f, -1.60303879f, 2.68994999f, 25.5258045f, 6, 6, 5, 6, true, true),
                R(0, -1.8752141f, 2.25437856f, -0.529919267f, -0.848048091f, -1.8752141f, 2.25437856f, 25.6808224f, 6, 6, 5, 6, true, true),
                R(0, -2.14903235f, 1.81617785f, -0.529919267f, -0.848048091f, -2.14903235f, 1.81617785f, 25.8358402f, 6, 6, 5, 6, true, true),
                R(1, -2.07550645f, 1.37534797f, 0.529919267f, -0.848048091f, -2.25f, 1.37534797f, 25.9908562f, 5, 7, 6, 7, true, true),
                R(0, -1.80004537f, 0.934518099f, 0.529919267f, -0.848048091f, -1.80004537f, 0.934518099f, 25.9908562f, 5, 7, 6, 7, true, true),
                R(0, -1.52156126f, 0.488850385f, 0.529919267f, -0.848048091f, -1.52156126f, 0.488850385f, 26.2760887f, 5, 7, 6, 7, true, true),
                R(0, -1.24005413f, 0.0383448601f, 0.529919267f, -0.848048091f, -1.24005413f, 0.0383448601f, 26.5613213f, 5, 7, 6, 7, true, true),
                R(0, -0.955524027f, -0.416998476f, 0.529919267f, -0.848048091f, -0.955524027f, -0.416998476f, 26.8465538f, 5, 7, 6, 7, true, true),
                R(0, -0.667970896f, -0.877179682f, 0.529919267f, -0.848048091f, -0.667970896f, -0.877179682f, 27.1317863f, 5, 7, 6, 7, true, true),
                R(0, -0.377394766f, -1.34219873f, 0.529919267f, -0.848048091f, -0.377394766f, -1.34219873f, 27.4170189f, 5, 7, 6, 7, true, true),
                R(0, -0.0837956667f, -1.81205547f, 0.529919267f, -0.848048091f, -0.0837956667f, -1.81205547f, 27.7022495f, 5, 7, 6, 7, true, true),
                R(0, 0.212826431f, -2.28675008f, 0.529919267f, -0.848048091f, 0.212826431f, -2.28675008f, 27.9874821f, 5, 7, 6, 7, true, true),
                R(0, 0.512471557f, -2.76628256f, 0.529919267f, -0.848048091f, 0.512471557f, -2.76628256f, 28.2727146f, 5, 7, 6, 7, true, true),
                R(0, 0.815139651f, -3.25065279f, 0.529919267f, -0.848048091f, 0.815139651f, -3.25065279f, 28.5579472f, 5, 7, 6, 7, true, true),
                R(0, 1.12083077f, -3.73986077f, 0.529919267f, -0.848048091f, 1.12083077f, -3.73986077f, 28.8431797f, 5, 7, 6, 7, true, true),
                R(0, 1.42954493f, -4.23390675f, 0.529919267f, -0.848048091f, 1.42954493f, -4.23390675f, 29.1284122f, 5, 7, 6, 7, true, true),
                R(1, 1.74128199f, -4.26720953f, 0.529919267f, 0.848048091f, 1.74128199f, -4.5f, 29.4136448f, 4, 8, 7, 8, true, true),
                R(0, 2.05301905f, -3.76832581f, 0.529919267f, 0.848048091f, 2.05301905f, -3.76832581f, 29.4136448f, 4, 8, 7, 8, true, true),
                R(1, 2.13202882f, -3.26429725f, -0.529919267f, 0.848048091f, 2.25f, -3.26429725f, 29.7169838f, 3, 9, 8, 9, true, true),
                R(0, 1.8170768f, -2.76026869f, -0.529919267f, 0.848048091f, 1.8170768f, -2.76026869f, 29.7169838f, 3, 9, 8, 9, true, true),
                R(0, 1.49739552f, -2.24867177f, -0.529919267f, 0.848048091f, 1.49739552f, -2.24867177f, 30.1632061f, 3, 9, 8, 9, true, true),
                R(0, 1.17298496f, -1.72950649f, -0.529919267f, 0.848048091f, 1.17298496f, -1.72950649f, 30.6094284f, 3, 9, 8, 9, true, true),
                R(0, 0.843845189f, -1.20277274f, -0.529919267f, 0.848048091f, 0.843845189f, -1.20277274f, 31.0556526f, 3, 9, 8, 9, true, true),
                R(0, 0.509976149f, -0.668470621f, -0.529919267f, 0.848048091f, 0.509976149f, -0.668470621f, 31.5018749f, 3, 9, 8, 9, true, true),
                R(0, 0.171377927f, -0.126600206f, -0.529919267f, 0.848048091f, 0.171377927f, -0.126600206f, 31.9480972f, 3, 9, 8, 9, true, true),
            };
        }

        private static List<Row> GoldenPierceDischarge()
        {
            return new List<Row>
            {
                R(0, 0.932491899f, -3.31452298f, -0.342020124f, 0.939692616f, 0.932491899f, -3.31452298f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.864983797f, -3.12904596f, -0.342020124f, 0.939692616f, 0.864983797f, -3.12904596f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.797475696f, -2.94356894f, -0.342020124f, 0.939692616f, 0.797475696f, -2.94356894f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.729967594f, -2.75809193f, -0.342020124f, 0.939692616f, 0.729967594f, -2.75809193f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.662459493f, -2.57261491f, -0.342020124f, 0.939692616f, 0.662459493f, -2.57261491f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.594951391f, -2.38713789f, -0.342020124f, 0.939692616f, 0.594951391f, -2.38713789f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.52744329f, -2.20166087f, -0.342020124f, 0.939692616f, 0.52744329f, -2.20166087f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.459935218f, -2.01618385f, -0.342020124f, 0.939692616f, 0.459935218f, -2.01618385f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.392427146f, -1.83070695f, -0.342020124f, 0.939692616f, 0.392427146f, -1.83070695f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.324919075f, -1.64523005f, -0.342020124f, 0.939692616f, 0.324919075f, -1.64523005f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.257411003f, -1.45975316f, -0.342020124f, 0.939692616f, 0.257411003f, -1.45975316f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.189902931f, -1.27427626f, -0.342020124f, 0.939692616f, 0.189902931f, -1.27427626f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.122394852f, -1.08879936f, -0.342020124f, 0.939692616f, 0.122394852f, -1.08879936f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, 0.0548867732f, -0.903322399f, -0.342020124f, 0.939692616f, 0.0548867732f, -0.903322399f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.0126213059f, -0.71784544f, -0.342020124f, 0.939692616f, -0.0126213059f, -0.71784544f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.080129385f, -0.532368481f, -0.342020124f, 0.939692616f, -0.080129385f, -0.532368481f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.147637457f, -0.346891522f, -0.342020124f, 0.939692616f, -0.147637457f, -0.346891522f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.215145528f, -0.161414579f, -0.342020124f, 0.939692616f, -0.215145528f, -0.161414579f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.2826536f, 0.0240623653f, -0.342020124f, 0.939692616f, -0.2826536f, 0.0240623653f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.350161672f, 0.209539309f, -0.342020124f, 0.939692616f, -0.350161672f, 0.209539309f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.417669743f, 0.395016253f, -0.342020124f, 0.939692616f, -0.417669743f, 0.395016253f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.485177815f, 0.580493212f, -0.342020124f, 0.939692616f, -0.485177815f, 0.580493212f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.552685916f, 0.76597017f, -0.342020124f, 0.939692616f, -0.552685916f, 0.76597017f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.620194018f, 0.951447129f, -0.342020124f, 0.939692616f, -0.620194018f, 0.951447129f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.687702119f, 1.13692403f, -0.342020124f, 0.939692616f, -0.687702119f, 1.13692403f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.755210221f, 1.32240093f, -0.342020124f, 0.939692616f, -0.755210221f, 1.32240093f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.822718322f, 1.50787783f, -0.342020124f, 0.939692616f, -0.822718322f, 1.50787783f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.890226424f, 1.69335473f, -0.342020124f, 0.939692616f, -0.890226424f, 1.69335473f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -0.957734525f, 1.87883162f, -0.342020124f, 0.939692616f, -0.957734525f, 1.87883162f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.02524257f, 2.06430864f, -0.342020124f, 0.939692616f, -1.02524257f, 2.06430864f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.09275067f, 2.24978566f, -0.342020124f, 0.939692616f, -1.09275067f, 2.24978566f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.16025877f, 2.43526268f, -0.342020124f, 0.939692616f, -1.16025877f, 2.43526268f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.22776687f, 2.6207397f, -0.342020124f, 0.939692616f, -1.22776687f, 2.6207397f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.29527497f, 2.80621672f, -0.342020124f, 0.939692616f, -1.29527497f, 2.80621672f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.36278307f, 2.99169374f, -0.342020124f, 0.939692616f, -1.36278307f, 2.99169374f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.43029118f, 3.17717075f, -0.342020124f, 0.939692616f, -1.43029118f, 3.17717075f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.49779928f, 3.36264777f, -0.342020124f, 0.939692616f, -1.49779928f, 3.36264777f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(0, -1.56530738f, 3.54812479f, -0.342020124f, 0.939692616f, -1.56530738f, 3.54812479f, 9.86902237f, 5, 0, 0, 0, false, true),
                R(1, -1.63281548f, 3.51639819f, -0.342020124f, -0.939692616f, -1.63281548f, 3.625f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -1.70032358f, 3.33092117f, -0.342020124f, -0.939692616f, -1.70032358f, 3.33092117f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -1.76783168f, 3.14544415f, -0.342020124f, -0.939692616f, -1.76783168f, 3.14544415f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -1.83533978f, 2.95996714f, -0.342020124f, -0.939692616f, -1.83533978f, 2.95996714f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -1.90284789f, 2.77449012f, -0.342020124f, -0.939692616f, -1.90284789f, 2.77449012f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -1.97035599f, 2.5890131f, -0.342020124f, -0.939692616f, -1.97035599f, 2.5890131f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -2.03786397f, 2.40353608f, -0.342020124f, -0.939692616f, -2.03786397f, 2.40353608f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -2.10537195f, 2.21805906f, -0.342020124f, -0.939692616f, -2.10537195f, 2.21805906f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -2.17287993f, 2.03258204f, -0.342020124f, -0.939692616f, -2.17287993f, 2.03258204f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(0, -2.24038792f, 1.84710515f, -0.342020124f, -0.939692616f, -2.24038792f, 1.84710515f, 9.86902237f, 4, 0, 0, 1, false, true),
                R(1, -2.1921041f, 1.66162825f, 0.342020124f, -0.939692616f, -2.25f, 1.66162825f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -2.12459612f, 1.47615135f, 0.342020124f, -0.939692616f, -2.12459612f, 1.47615135f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -2.05708814f, 1.29067445f, 0.342020124f, -0.939692616f, -2.05708814f, 1.29067445f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.98958004f, 1.10519755f, 0.342020124f, -0.939692616f, -1.98958004f, 1.10519755f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.92207193f, 0.91972059f, 0.342020124f, -0.939692616f, -1.92207193f, 0.91972059f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.85456383f, 0.734243631f, 0.342020124f, -0.939692616f, -1.85456383f, 0.734243631f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.78705573f, 0.548766673f, 0.342020124f, -0.939692616f, -1.78705573f, 0.548766673f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.71954763f, 0.363289714f, 0.342020124f, -0.939692616f, -1.71954763f, 0.363289714f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.65203953f, 0.17781277f, 0.342020124f, -0.939692616f, -1.65203953f, 0.17781277f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.58453143f, -0.00766417384f, 0.342020124f, -0.939692616f, -1.58453143f, -0.00766417384f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.51702332f, -0.193141118f, 0.342020124f, -0.939692616f, -1.51702332f, -0.193141118f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.44951522f, -0.378618062f, 0.342020124f, -0.939692616f, -1.44951522f, -0.378618062f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.38200712f, -0.56409502f, 0.342020124f, -0.939692616f, -1.38200712f, -0.56409502f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.31449902f, -0.749571979f, 0.342020124f, -0.939692616f, -1.31449902f, -0.749571979f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.24699092f, -0.935048938f, 0.342020124f, -0.939692616f, -1.24699092f, -0.935048938f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.17948282f, -1.12052584f, 0.342020124f, -0.939692616f, -1.17948282f, -1.12052584f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.11197472f, -1.30600274f, 0.342020124f, -0.939692616f, -1.11197472f, -1.30600274f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -1.04446661f, -1.49147964f, 0.342020124f, -0.939692616f, -1.04446661f, -1.49147964f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.976958513f, -1.67695653f, 0.342020124f, -0.939692616f, -0.976958513f, -1.67695653f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.909450412f, -1.86243343f, 0.342020124f, -0.939692616f, -0.909450412f, -1.86243343f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.84194231f, -2.04791045f, 0.342020124f, -0.939692616f, -0.84194231f, -2.04791045f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.774434209f, -2.23338747f, 0.342020124f, -0.939692616f, -0.774434209f, -2.23338747f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.706926107f, -2.41886449f, 0.342020124f, -0.939692616f, -0.706926107f, -2.41886449f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.639418006f, -2.60434151f, 0.342020124f, -0.939692616f, -0.639418006f, -2.60434151f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.571909904f, -2.78981853f, 0.342020124f, -0.939692616f, -0.571909904f, -2.78981853f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.504401803f, -2.97529554f, 0.342020124f, -0.939692616f, -0.504401803f, -2.97529554f, 9.86902237f, 3, 0, 0, 2, false, false),
                R(0, -0.436893731f, -3.16077256f, 0.342020124f, -0.939692616f, -0.436893731f, -3.16077256f, 9.86902237f, 3, 0, 0, 2, false, false),
            };
        }
    }
}

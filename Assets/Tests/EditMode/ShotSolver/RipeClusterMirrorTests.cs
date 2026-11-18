using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Flight;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Score;
using BalloonParty.Item;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Effects;
using BalloonParty.Nudge;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     The solver against the game for ripe clusters, their ripple and foam coats. Each test lays one board of real
    ///     models on a <see cref="SlotGrid" />, snapshots it through the gather's own target builder, flies the
    ///     simulator over it, then plays the same contacts through the game: <see cref="ProjectileHitResolver" /> for
    ///     the shot, the bomb's selection over a <see cref="GridEffectBoard" /> for an item, <see cref="ScoreController" />
    ///     for score and streak, <see cref="RipeBubbleService" /> for the ripple (chains with no delay, so one tick runs
    ///     them all). Score, the shot's colour and streak, and what is left on the board must agree.
    ///     The grid is (1, 1) apart with no offset: same-row neighbours sit 2 apart, the rest at (±1, ±1), and a slot two
    ///     rows away in the same column 2 straight above or below; nothing else is nearer than 2.8. The ripple's
    ///     radius is 2.05 unless a test sets another, so a blast takes the six neighbours and those two slots, on
    ///     both sides by the same rule (<see cref="RipeBubbleRule.RippleReaches" />). Shots fly along (1, 2), which passes every
    ///     neighbour of a slot on its line at least 0.44 off, so only the slots placed on the line are struck. Along
    ///     that line from (2, 8) the slots are (3, 4) and (4, 0). An item goes off the way <c>ItemActivator</c> decides
    ///     (only a pop releases one), and a bomb's blast takes its damage and flags from the same item settings the
    ///     game's <c>BombItemHandler</c> reads, which also build the solver's item layer.
    /// </summary>
    [TestFixture]
    public class RipeClusterMirrorTests
    {
        private const string Red = "Red";
        private const string Blue = "Blue";
        private const string Green = "Green";
        private const int MergeMax = 5;
        private const float ContactRadius = 0.1f;
        private const float BlastRadius = 2.05f;
        private const float RippleRadius = 2.05f;

        private static readonly Vector2 Separation = new(1f, 1f);
        private static readonly Vector2 Diagonal = new Vector2(1f, 2f).normalized;
        private static readonly string[] Colors = { Red, Blue, Green };

        private SlotGrid _grid;
        private IGamePalette _palette;
        private IBalloonsConfiguration _balloons;
        private ProjectileRegistry _registry;
        private ColorStreakTracker _streaks;
        private ScoreController _score;
        private RipeBubbleService _ripe;
        private ProjectileHitResolver _resolver;
        private GameDispatcher _dispatcher;
        private ProjectileModel _ball;
        private IMessageHandler<ActorHitMessage> _rippleHears;
        private IItemConfiguration _items;
        private ItemActivator _activator;
        private List<IBalloonModel> _released;
        private int _gameScore;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gridConfig.SlotSeparation.Returns(Separation);
            gridConfig.SlotsOffset.Returns(Vector2.zero);
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _palette = Substitute.For<IGamePalette>();
            _palette.ColorNames.Returns(Colors);
            _palette.ProgressColorNames.Returns(Colors);
            _palette.Colors.Returns(new List<PaletteEntry>());
            _palette.IsRainbow(GamePalette.RainbowColorId).Returns(true);

            var settings = Substitute.For<IRipeBubbleSettings>();
            settings.FuseSeconds.Returns(6f);
            settings.RippleDamage.Returns(1);
            settings.RippleRadius.Returns(RippleRadius);
            settings.BurstSeconds.Returns(0f);
            settings.CoatLayers.Returns(1);
            _balloons = Substitute.For<IBalloonsConfiguration>();
            _balloons.BubbleMergeMax.Returns(MergeMax);
            _balloons.RipeBubble.Returns(settings);

            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(new ReactiveProperty<int>(1));
            levelProgress.GetRequiredPoints().Returns(1000);
            levelProgress.ClaimProgress(Arg.Any<string>(), Arg.Any<int>()).Returns(ci => (0, ci.Arg<int>()));

            _registry = new ProjectileRegistry();
            _streaks = new ColorStreakTracker(
                Substitute.For<IPublisher<StreakChangedMessage>>(), Quiet<ScoreLevelUpMessage>(),
                Quiet<ProjectileLoadedMessage>(), _registry);
            _gameScore = 0;
            var scored = Substitute.For<IPublisher<ScorePointsGroupMessage>>();
            scored.When(p => p.Publish(Arg.Any<ScorePointsGroupMessage>()))
                .Do(call => _gameScore += call.Arg<ScorePointsGroupMessage>().Points);
            _score = new ScoreController(
                Quiet<ScoreTrailArrivedMessage>(), Quiet<ScoreLevelUpMessage>(), scored,
                Substitute.For<IPublisher<StreakGrewMessage>>(), levelProgress, _palette,
                Substitute.For<IRetryState>(), _streaks, Substitute.For<ITimeOfDayNight>());
            _score.Start();

            _items = BombConfiguration(BlastRadius);
            var bomb = Substitute.For<IBalloonItem>();
            bomb.Type.Returns(ItemType.Bomb);
            _activator = new ItemActivator(
                new[] { bomb }, Quiet<ActorHitMessage>(), Substitute.For<IPublisher<ItemActivatedMessage>>());
            _activator.Start();
            _released = new List<IBalloonModel>();

            _dispatcher = new GameDispatcher(_grid, _score, message =>
            {
                _rippleHears.Handle(message);
                if (_activator.HandlerReleasedBy(message) != null)
                {
                    _released.Add((IBalloonModel)message.Actor);
                }
            });
            var disturbanceField = new DisturbanceFieldService(
                Substitute.For<IDisturbanceFieldSettings>(), Substitute.For<IGameDisplayConfiguration>(),
                new ImpactEventBus());
            var clock = Substitute.For<ISpawnClock>();
            clock.SecondsOnLevel.Returns(0f);
            var room = new RipeRoom(_grid, Substitute.For<IBoardBeat>(), levelProgress, _balloons);
            _ripe = new RipeBubbleService(
                _grid, clock, _balloons, levelProgress, new RipeBubbleBurst(
                    _grid, _dispatcher, Substitute.For<IBalloonPopper>(), _balloons, room, disturbanceField,
                    new SceneLightFieldService(
                        Substitute.For<IGameDisplayConfiguration>(), _palette,
                        Substitute.For<ISceneLightFieldSettings>()),
                    Substitute.For<IPublisher<NudgeMessage>>()),
                room, Capture<ActorHitMessage>(h => _rippleHears = h), Quiet<BubbleMergedMessage>(),
                Quiet<GameOverMessage>(),
                Substitute.For<IPublisher<BubbleRipenedMessage>>(), Substitute.For<IPublisher<RipeBubbleBurstMessage>>(),
                Substitute.For<IPublisher<RipeFuseBeatMessage>>());
            _ripe.Start();

            var runConfig = Substitute.For<IRunConfig>();
            runConfig.StreakGrantsShields.Returns(true);
            _resolver = new ProjectileHitResolver(
                _dispatcher, Substitute.For<IPublisher<ShieldGainedMessage>>(),
                Substitute.For<IPublisher<PierceDischargedMessage>>(), Substitute.For<IFlightStatsWriter>(), _streaks,
                runConfig, _grid, new ShotPaintLedger(_registry));

            _ball = new ProjectileModel { IsFree = true };
            _registry.Load(_ball, null);
            _registry.Launch(_ball);
        }

        [TearDown]
        public void TearDown()
        {
            _ripe.Dispose();
            _score.Dispose();
            _activator.Dispose();
        }

        // A ripe cluster in a ring of every kind of neighbour, one of them ripe too. The burst scores the 5 it held and
        // breaks the streak; the ripple pops the plain balloon, takes the coat, takes a bubble off the small cluster,
        // bursts the ripe neighbour, and passes the heart and the unbreakable by. The chained burst hits the small
        // cluster again and pops the balloon whose coat the first took.
        [Test]
        public void ARipeClusterInAMixedRing_GameAndSolverAgree()
        {
            var centre = new Vector2Int(3, 4);
            var red = Plain(new Vector2Int(2, 8), Red);
            var ripe = Cluster(centre, MergeMax);
            Plain(new Vector2Int(2, 4), Red);
            var coated = Plain(new Vector2Int(4, 4), Blue);
            coated.TryCoat(1);
            Cluster(new Vector2Int(3, 3), MergeMax);
            var small = Cluster(new Vector2Int(2, 3), 3);
            Place(new HeartBalloonModel(new BalloonModelConfig(typeName: BalloonType.Heart)), new Vector2Int(3, 5));
            Place(new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)),
                new Vector2Int(2, 5));
            RipenAtTheCap();

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors);
            Shoot(red, ripe);
            _ripe.Tick();

            AssertAgree(sim, survivors);
            Assert.AreEqual(1 + 5 + 1 + 5 + 3 + 2 + 1, _gameScore);
            Assert.AreEqual(1, small.HitsRemaining.Value);
            Assert.AreEqual(0, _streaks.For(_ball).CurrentStreak);
            Assert.IsTrue(string.IsNullOrEmpty(_ball.ColorName.Value), "the burst washed the shot");
        }

        // Three ripe clusters up a column and a plain balloon past the last. The first blast bursts both clusters above
        // it (the far one is 2 away, inside the radius), and their own blasts reach the plain balloon.
        [Test]
        public void AChainOfRipeClusters_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(2, 8), Red);
            var first = Cluster(new Vector2Int(3, 4), MergeMax);
            Cluster(new Vector2Int(3, 3), MergeMax);
            Cluster(new Vector2Int(3, 2), MergeMax);
            Plain(new Vector2Int(3, 1), Red);
            RipenAtTheCap();

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors);
            Shoot(red, first);
            _ripe.Tick();

            AssertAgree(sim, survivors);
            Assert.AreEqual(1 + 5 + 5 + 5 + 1, _gameScore);
            Assert.IsTrue(sim.BoardCleared);
        }

        // Straight up a column: the coat takes the first contact and washes the shot; the wall sends it back down onto
        // the bare balloon, which pops and gives the shot its colour.
        [Test]
        public void ACoatedBalloonHitTwice_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(3, 6), Red);
            var coated = Plain(new Vector2Int(3, 4), Blue);
            coated.TryCoat(1);

            var top = _grid.IndexToWorldPosition(new Vector2Int(3, 2)).y;
            var bottom = _grid.IndexToWorldPosition(new Vector2Int(3, 7)).y;
            var walls = new Vector4(top, 1000f, bottom, -1000f);
            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Vector2.up), Vector2.up, shields: 1, out var survivors,
                walls);
            Shoot(red, coated);
            _ball.LastHitBalloon = null;
            Shoot(coated);

            AssertAgree(sim, survivors);
            Assert.AreEqual(2, _gameScore);
            Assert.AreEqual(Blue, _ball.ColorName.Value);
            Assert.AreEqual(1, sim.FinalStreak);
        }

        // A pierce pops a coated balloon on contact, coat and all, and steals its colour: nothing washes.
        [Test]
        public void ACoatedBalloonUnderAPierce_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(3, 6), Red);
            var coated = Plain(new Vector2Int(3, 4), Blue);
            coated.TryCoat(1);

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Vector2.up), Vector2.up, shields: 0, out var survivors,
                seed: ShotFlightSeed.Armed(0f));
            _ball.IsPiercing.Value = true;
            Shoot(red, coated);

            AssertAgree(sim, survivors);
            Assert.AreEqual(2, _gameScore);
            Assert.AreEqual(Blue, _ball.ColorName.Value);
            Assert.AreEqual(0, survivors.Count);
        }

        // The shot pops a bomb carrier. Its blast pierces every neighbour: the coated one pops, coat and all, the ripe
        // cluster bursts and the small cluster pops for the 3 it held. Past the neighbours the blast does plain damage:
        // a coat comes off, and a cluster loses a bubble and still scores the 3 it held. Then the ripple takes a
        // second bubble off that cluster.
        [Test]
        public void ABombOnCoatsAndClusters_GameAndSolverAgree()
        {
            var host = Plain(new Vector2Int(3, 3), Red);
            host.Item.Value = ItemType.Bomb;
            var coatedNeighbour = Plain(new Vector2Int(2, 3), Blue);
            coatedNeighbour.TryCoat(1);
            Cluster(new Vector2Int(3, 2), MergeMax);
            Cluster(new Vector2Int(4, 3), 3);
            var coatedFar = Plain(new Vector2Int(3, 5), Green);
            coatedFar.TryCoat(1);
            var farCluster = Cluster(new Vector2Int(3, 1), 3);
            RipenAtTheCap();

            var sim = Simulate(OriginBefore(host.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors,
                items: ItemLayer());

            Shoot(host);
            BlastReleased();
            _ripe.Tick();

            // The streak each side ends on depends on the order the blast hits land in: the game takes it from the physics
            // overlap, the solver from its working set. Every hit here scores at 1 either way.
            AssertAgree(sim, survivors, compareStreak: false);
            Assert.AreEqual(1 + 1 + 5 + 3 + 3 + 2, _gameScore);
            Assert.AreEqual(0, coatedFar.Coat.Value);
            Assert.AreSame(coatedFar, _grid.At(new Vector2Int(3, 5)));
            Assert.AreEqual(1, farCluster.HitsRemaining.Value);
        }

        // A rainbow-buffed shot climbs its streak on every pop, a cluster's burst too: two reds take it to 2 and the ripe
        // cluster at (4, 0) to 3, so its ripple scores at x3. The buff turns the two neighbours of each pop most across
        // the shot rainbow (ConvertSideNeighboursToRainbow), which here are (4, 1) and (5, 0); (3, 1) stays blue.
        [Test]
        public void ARippleUnderAStreakOfTwo_GameAndSolverAgree()
        {
            var first = Plain(new Vector2Int(2, 8), Red);
            var second = Plain(new Vector2Int(3, 4), Red);
            var ripe = Cluster(new Vector2Int(4, 0), MergeMax);
            var blue = Plain(new Vector2Int(3, 1), Blue);
            Plain(new Vector2Int(4, 1), Blue);
            Plain(new Vector2Int(5, 0), Green);
            RipenAtTheCap();

            var sim = Simulate(OriginBefore(first.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors,
                seed: ShotFlightSeed.WithRainbowBuff(untilWall: true));
            ApplyRainbowBuff();
            Shoot(first, second);
            Assert.AreEqual(2, _streaks.For(_ball).CurrentStreak, "two pops before the burst");
            Shoot(ripe);
            Assert.AreEqual(3, _streaks.For(_ball).CurrentStreak, "the buff climbs on the cluster too");
            _ripe.Tick();

            AssertAgree(sim, survivors);
            Assert.IsNull(_grid.At(blue.SlotIndex.Value), "the ripple popped the blue balloon");
            // Reds x1 and x2, the cluster's 5 at x3, then the ripple at x3: the blue 1, and each rainbow 1 per colour.
            Assert.AreEqual(1 + 2 + 5 * 3 + 1 * 3 + 3 * 3 + 3 * 3, _gameScore);
            Assert.AreEqual(3, _streaks.For(_ball).CurrentStreak, "the ripple leaves the streak where it was");
        }

        // A pierce bursts a ripe cluster on contact (it is never ploughed), and its ripple runs as from any burst: the
        // plain neighbour pops, the coated one loses its coat.
        [Test]
        public void ARipeClusterUnderAPierce_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(2, 8), Red);
            var ripe = Cluster(new Vector2Int(3, 4), MergeMax);
            Plain(new Vector2Int(2, 4), Blue);
            var coated = Plain(new Vector2Int(4, 4), Green);
            coated.TryCoat(1);
            RipenAtTheCap();

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors,
                seed: ShotFlightSeed.Armed(0f));
            _ball.IsPiercing.Value = true;
            Shoot(red, ripe);
            _ripe.Tick();

            AssertAgree(sim, survivors);
            Assert.IsNull(_grid.At(ripe.SlotIndex.Value), "the pierce burst the cluster");
            Assert.AreEqual(0, coated.Coat.Value);
            Assert.AreEqual(1 + 5 + 1, _gameScore);
        }

        // The shot's ordinary hit on a coated bomb carrier takes the coat and releases nothing: the balloon is still
        // there, and so is everything round it.
        [Test]
        public void ACoatedCarrierHitByTheShot_ReleasesNothing_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(2, 8), Red);
            var carrier = Plain(new Vector2Int(3, 4), Blue);
            carrier.Item.Value = ItemType.Bomb;
            carrier.TryCoat(1);
            Plain(new Vector2Int(2, 4), Green);

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors,
                items: ItemLayer());
            Shoot(red, carrier);
            BlastReleased();

            AssertAgree(sim, survivors);
            Assert.IsEmpty(_released, "a coat hit releases no item");
            Assert.AreSame(carrier, _grid.At(new Vector2Int(3, 4)));
            Assert.AreEqual(ItemType.Bomb, carrier.Item.Value);
            Assert.AreEqual(1, _gameScore);
        }

        // A ripple pops an uncoated bomb carrier next to the burst; the pop releases the bomb, whose blast pierces the
        // carrier's own neighbour.
        [Test]
        public void ARipplePopsACarrier_ItsItemGoesOff_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(2, 8), Red);
            var ripe = Cluster(new Vector2Int(3, 4), MergeMax);
            var carrier = Plain(new Vector2Int(2, 4), Red);
            carrier.Item.Value = ItemType.Bomb;
            var blasted = Plain(new Vector2Int(1, 4), Blue);
            RipenAtTheCap();

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors,
                items: ItemLayer());
            Shoot(red, ripe);
            _ripe.Tick();
            Assert.AreEqual(1, _released.Count, "the ripple's pop released the bomb");
            BlastReleased();

            AssertAgree(sim, survivors, compareStreak: false);
            Assert.IsNull(_grid.At(carrier.SlotIndex.Value));
            Assert.IsNull(_grid.At(blasted.SlotIndex.Value), "the bomb went off");
            Assert.AreEqual(1 + 5 + 1 + 1, _gameScore);
        }

        // A coated rainbow: the coat takes the first contact and leaves the red streak alone; back off the wall, the bare
        // rainbow pops as any rainbow does, climbing the streak and paying every colour.
        [Test]
        public void ACoatedRainbowHitTwice_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(3, 6), Red);
            var rainbow = Plain(new Vector2Int(3, 4), GamePalette.RainbowColorId);
            rainbow.TryCoat(1);

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Vector2.up), Vector2.up, shields: 1, out var survivors,
                ColumnWalls(2, 7));
            Shoot(red, rainbow);
            Assert.AreEqual(1, _streaks.For(_ball).CurrentStreak, "the coat hit leaves the streak");
            Assert.AreSame(rainbow, _grid.At(new Vector2Int(3, 4)));
            _ball.LastHitBalloon = null;
            Shoot(rainbow);

            AssertAgree(sim, survivors);
            Assert.AreEqual(2, sim.FinalStreak);
            Assert.AreEqual(1 + 3 * 2, _gameScore);
        }

        // Two reds give the shot a streak of 2; the coat hit washes its colour and leaves the count at 2. Back off the
        // wall, the bare blue pops: a colour the streak was not on, so it starts again at 1.
        [Test]
        public void ACoatHitUnderAStreakOfTwo_GameAndSolverAgree()
        {
            var first = Plain(new Vector2Int(3, 8), Red);
            var second = Plain(new Vector2Int(3, 6), Red);
            var coated = Plain(new Vector2Int(3, 4), Blue);
            coated.TryCoat(1);

            var sim = Simulate(OriginBefore(first.SlotIndex.Value, Vector2.up), Vector2.up, shields: 1,
                out var survivors, ColumnWalls(2, 9));
            Shoot(first, second, coated);
            Assert.IsTrue(string.IsNullOrEmpty(_ball.ColorName.Value), "the coat washed the shot");
            Assert.AreEqual(2, _streaks.For(_ball).CurrentStreak, "a coat hit neither climbs nor breaks the streak");
            _ball.LastHitBalloon = null;
            Shoot(coated);

            AssertAgree(sim, survivors);
            Assert.AreEqual(1 + 2 + 1, _gameScore);
            Assert.AreEqual(Blue, _ball.ColorName.Value);
        }

        // A blast, not a ring of neighbours: the plain balloons two rows straight up and down are 2 away and pop, the
        // coated neighbour loses its coat, and the balloons 4 along the row and 2.8 up the diagonal are out of it.
        [Test]
        public void ABlastPastTheNeighbours_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(2, 8), Red);
            var ripe = Cluster(new Vector2Int(3, 4), MergeMax);
            var above = Plain(new Vector2Int(3, 2), Blue);
            var below = Plain(new Vector2Int(3, 6), Green);
            var coated = Plain(new Vector2Int(4, 4), Blue);
            coated.TryCoat(1);
            var alongTheRow = Plain(new Vector2Int(1, 4), Red);
            var upTheDiagonal = Plain(new Vector2Int(4, 2), Red);
            RipenAtTheCap();

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors);
            Shoot(red, ripe);
            _ripe.Tick();

            AssertAgree(sim, survivors);
            Assert.IsNull(_grid.At(above.SlotIndex.Value), "2 away, not a neighbour: in the blast");
            Assert.IsNull(_grid.At(below.SlotIndex.Value));
            Assert.AreEqual(0, coated.Coat.Value);
            Assert.AreSame(alongTheRow, _grid.At(new Vector2Int(1, 4)));
            Assert.AreSame(upTheDiagonal, _grid.At(new Vector2Int(4, 2)));
            Assert.AreEqual(1 + 5 + 1 + 1, _gameScore);
            Assert.AreEqual(3, survivors.Count);
        }

        // A ripe cluster two rows up is inside the blast without being a neighbour. It bursts, and its own blast, from
        // its own slot, pops two balloons the first was too far from.
        [Test]
        public void AChainThroughARipeClusterInsideTheRadius_GameAndSolverAgree()
        {
            var red = Plain(new Vector2Int(2, 8), Red);
            var first = Cluster(new Vector2Int(3, 4), MergeMax);
            var second = Cluster(new Vector2Int(3, 2), MergeMax);
            var beyond = Plain(new Vector2Int(3, 0), Blue);
            var beside = Plain(new Vector2Int(2, 1), Green);
            RipenAtTheCap();
            Assert.IsTrue(second.Ripe.Value);

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors);
            Shoot(red, first);
            _ripe.Tick();

            AssertAgree(sim, survivors);
            Assert.IsNull(_grid.At(second.SlotIndex.Value), "the first blast burst it");
            Assert.IsNull(_grid.At(beyond.SlotIndex.Value), "4 from the first, 2 from the second");
            Assert.IsNull(_grid.At(beside.SlotIndex.Value), "3.2 from the first, 1.4 from the second");
            Assert.AreEqual(1 + 5 + 5 + 1 + 1, _gameScore);
            Assert.IsTrue(sim.BoardCleared);
        }

        // The radius decides on both sides, not adjacency: at 1.5 the neighbour on the row above (1.4 away) pops and
        // the neighbour on the cluster's own row (2 away) is left alone.
        [Test]
        public void ARadiusShortOfSomeNeighbours_GameAndSolverAgree()
        {
            _balloons.RipeBubble.RippleRadius.Returns(1.5f);
            var red = Plain(new Vector2Int(2, 8), Red);
            var ripe = Cluster(new Vector2Int(3, 4), MergeMax);
            var onTheRow = Plain(new Vector2Int(2, 4), Blue);
            var onTheRowAbove = Plain(new Vector2Int(3, 3), Green);
            RipenAtTheCap();

            var sim = Simulate(OriginBefore(red.SlotIndex.Value, Diagonal), Diagonal, shields: 0, out var survivors);
            Shoot(red, ripe);
            _ripe.Tick();

            AssertAgree(sim, survivors);
            Assert.AreSame(onTheRow, _grid.At(new Vector2Int(2, 4)));
            Assert.IsNull(_grid.At(onTheRowAbove.SlotIndex.Value));
            Assert.AreEqual(1 + 5 + 1, _gameScore);
        }

        // The ripple is measured between slot centres, as the game measures it, not between the balloons as drawn: a
        // balloon on its way to a slot inside the blast is hit though it is still drawn far off, and one drawn close by
        // whose slot is out of reach is not.
        [Test]
        public void TheSolversRipple_MeasuresBetweenSlotCentres_NotWhereTheBalloonsAreDrawn()
        {
            var board = new List<ShotBalloonSnapshot>
            {
                ShotBalloonSnapshot.ForBubbleClusterTarget(
                    Vector2.zero, 0.3f, MergeMax, SlotOnly(new Vector2Int(0, 0)), ripe: true),
                ShotBalloonSnapshot.ForColorTarget(new Vector2(10f, 0f), 0.3f, Red, 1, 1, SlotOnly(new Vector2Int(1, 0)))
                    .AtSlotCenter(new Vector2(1f, 0f)),
                ShotBalloonSnapshot.ForColorTarget(new Vector2(1f, 0.2f), 0.3f, Blue, 1, 1, SlotOnly(new Vector2Int(2, 0)))
                    .AtSlotCenter(new Vector2(5f, 5f))
            };
            var workingSet = new ShotBalloonState[board.Count];

            var result = ShotSimulator.Simulate(
                board, new Vector4(1000f, 1000f, -1000f, -1000f), new Vector2(0f, -2f), Vector2.up, 0, ContactRadius,
                workingSet, rippleDamage: 1, allowedColors: Colors, rippleRadius: 1.5f);

            Assert.AreEqual(2, result.Pops, "the cluster, and the balloon whose slot is 1 from it");
            Assert.AreEqual(new Vector2Int(2, 0), workingSet[0].SlotIndex, "the one whose slot is out of reach stays");
        }

        // A snapshot built without a slot centre sits on its slot: the ripple measures from where it is.
        [Test]
        public void ASnapshotWithNoSlotCentreOfItsOwn_IsMeasuredWhereItIs()
        {
            var drawn = new Vector2(3f, -2f);
            var snapshot = ShotBalloonSnapshot.ForColorTarget(drawn, 0.3f, Red, 1, 1);

            Assert.AreEqual(drawn, snapshot.SlotCenter);
            Assert.AreEqual(new Vector2(4f, 4f), snapshot.AtSlotCenter(new Vector2(4f, 4f)).SlotCenter);
            Assert.AreEqual(drawn, snapshot.AtSlotCenter(new Vector2(4f, 4f)).Position, "where it is drawn is kept");
        }

        // The gather reads the ripple's damage from the settings, with no fallback of its own.
        [Test]
        public void TheGather_ReadsTheConfiguredRippleDamage()
        {
            _balloons.RipeBubble.RippleDamage.Returns(2);

            Assert.AreEqual(2, ShotBoardGather.RippleDamageOf(_balloons));
        }

        // And its radius, the same way.
        [Test]
        public void TheGather_ReadsTheConfiguredRippleRadius()
        {
            _balloons.RipeBubble.RippleRadius.Returns(1.35f);

            Assert.AreEqual(1.35f, ShotBoardGather.RippleRadiusOf(_balloons));
        }

        // The context's ripple radius reaches the simulation: with it the burst pops the balloon beside it, and a
        // radius short of the next slot reaches nothing.
        [Test]
        public void SimulateAt_ForwardsTheContextsRippleRadius()
        {
            var red = Plain(new Vector2Int(2, 8), Red);
            Cluster(new Vector2Int(3, 4), MergeMax);
            Plain(new Vector2Int(2, 4), Blue);
            RipenAtTheCap();
            var board = BoardSnapshot();
            var angle = Mathf.Atan2(Diagonal.y, Diagonal.x) * Mathf.Rad2Deg;
            var origin = OriginBefore(red.SlotIndex.Value, Diagonal);

            var reaching = ShotBoardGather.SimulateAt(
                angle, ContextOver(board, origin, rippleDamage: 1), new ShotBalloonState[board.Count]);
            var reachingNothing = ShotBoardGather.SimulateAt(
                angle, ContextOver(board, origin, rippleDamage: 1, rippleRadius: 0.1f),
                new ShotBalloonState[board.Count]);

            Assert.AreEqual(3, reaching.Pops, "the red, the ripe cluster and the balloon beside it");
            Assert.AreEqual(2, reachingNothing.Pops);
        }

        // The context's ripple damage reaches the simulation: a ripple of 2 pops the two-bubble cluster next to the
        // burst, which a ripple of 1 would leave at one bubble.
        [Test]
        public void SimulateAt_ForwardsTheContextsRippleDamage()
        {
            var red = Plain(new Vector2Int(2, 8), Red);
            Cluster(new Vector2Int(3, 4), MergeMax);
            Cluster(new Vector2Int(2, 4), 2);
            RipenAtTheCap();
            var board = BoardSnapshot();
            var angle = Mathf.Atan2(Diagonal.y, Diagonal.x) * Mathf.Rad2Deg;

            var strong = ShotBoardGather.SimulateAt(
                angle, ContextOver(board, OriginBefore(red.SlotIndex.Value, Diagonal), rippleDamage: 2),
                new ShotBalloonState[board.Count]);
            var weak = ShotBoardGather.SimulateAt(
                angle, ContextOver(board, OriginBefore(red.SlotIndex.Value, Diagonal), rippleDamage: 1),
                new ShotBalloonState[board.Count]);

            Assert.AreEqual(3, strong.Pops, "the red, the ripe cluster and the two-bubble one");
            Assert.AreEqual(2, weak.Pops, "the two-bubble cluster keeps one bubble");
        }

        private ShotSimulationResult Simulate(
            Vector2 origin, Vector2 direction, int shields, out List<ShotBalloonState> survivors,
            Vector4? walls = null, ShotFlightSeed seed = default, ShotItemLayer items = null)
        {
            var board = BoardSnapshot();
            var workingSet = new ShotBalloonState[board.Count];
            var result = ShotSimulator.Simulate(
                board, walls ?? new Vector4(1000f, 1000f, -1000f, -1000f), origin, direction, shields, ContactRadius,
                workingSet, allowedColors: Colors, rainbowColorId: GamePalette.RainbowColorId, seed: seed, items: items,
                rippleDamage: _balloons.RipeBubble.RippleDamage, rippleRadius: _balloons.RipeBubble.RippleRadius);

            survivors = new List<ShotBalloonState>();
            var remaining = board.Count - result.Pops;
            for (var i = 0; i < remaining; i++)
            {
                survivors.Add(workingSet[i]);
            }

            return result;
        }

        private List<ShotBalloonSnapshot> BoardSnapshot()
        {
            var board = new List<ShotBalloonSnapshot>();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (!_grid.IsEmpty(col, row)
                        && ShotBoardGather.TryBuildTargetSnapshot(
                            _grid, _balloons, _palette, slot, _grid.At(slot), out var snapshot))
                    {
                        board.Add(snapshot);
                    }
                }
            }

            return board;
        }

        // Score, the shot's colour and streak, and every slot the solver keeps holding what the game's holds.
        private void AssertAgree(
            in ShotSimulationResult sim, List<ShotBalloonState> survivors, bool compareStreak = true)
        {
            Assert.AreEqual(_gameScore, sim.RawScore, "score");
            if (compareStreak)
            {
                Assert.AreEqual(_streaks.For(_ball).CurrentStreak, sim.FinalStreak, "streak");
            }

            Assert.AreEqual(
                string.IsNullOrEmpty(_ball.ColorName.Value) ? null : _ball.ColorName.Value,
                string.IsNullOrEmpty(sim.FinalColor) ? null : sim.FinalColor, "the shot's colour");

            var gameLeft = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row))
                    {
                        gameLeft++;
                    }
                }
            }

            Assert.AreEqual(gameLeft, survivors.Count, "how many are left");
            foreach (var survivor in survivors)
            {
                var actor = _grid.At(survivor.SlotIndex);
                Assert.IsNotNull(actor, $"the solver keeps {survivor.SlotIndex}, the game does not");
                if (actor is ICoatable coatable)
                {
                    Assert.AreEqual(coatable.Coat.Value, survivor.Coat, $"coat at {survivor.SlotIndex}");
                }

                if (actor is IHasDurability durable)
                {
                    Assert.AreEqual(durable.HitsRemaining.Value, survivor.HitsRemaining, $"hits at {survivor.SlotIndex}");
                }
            }
        }

        private void Shoot(params IBalloonModel[] balloons)
        {
            foreach (var balloon in balloons)
            {
                _resolver.Resolve(_ball, balloon, _grid.IndexToWorldPosition(balloon.SlotIndex.Value));
            }
        }

        // What BombItemHandler.Activate does for each bomb the game released (ItemActivator.HandlerReleasedBy): the
        // damage and flags from the item's settings, the carrier's colour as the source, its hex neighbours a piercing
        // hit and the rest the settings' own (BlastBalloons). The selection is the game's own core over the live grid:
        // the handler finds its targets through physics overlaps, and an edit-mode test has no colliders for them.
        private void BlastReleased()
        {
            var settings = _items[ItemType.Bomb];
            for (var r = 0; r < _released.Count; r++)
            {
                var host = _released[r];
                var hostSlot = host.SlotIndex.Value;
                var context = new DamageContext(settings.Damage, settings.Flags, host.GetColorId(), _ball);
                var piercing = new DamageContext(
                    context.Damage, DamageFlags.Piercing, context.SourceColorId, context.Projectile);

                var board = new GridEffectBoard(_grid);
                board.Rebuild(hostSlot);
                var hits = new List<EffectHit>();
                var origin = (Vector2)_grid.IndexToWorldPosition(hostSlot);
                BombBlast.Resolve(board, origin, hostSlot, false, null,
                    new BombBlastParams(settings.Bomb.Radius, settings.Bomb.RainbowConversionRange), hits);

                var models = new List<IBalloonModel>();
                var kinds = new List<EffectHitKind>();
                foreach (var hit in hits)
                {
                    models.Add(board.ModelAt(hit.Handle));
                    kinds.Add(hit.Kind);
                }

                for (var i = 0; i < models.Count; i++)
                {
                    var model = models[i];
                    _dispatcher.Dispatch(ActorHitMessage.From(
                        model, _grid.IndexToWorldPosition(model.SlotIndex.Value), Vector3.zero,
                        kinds[i] == EffectHitKind.PiercingDamage ? piercing : context));
                }
            }

            _released.Clear();
        }

        private ShotItemLayer ItemLayer()
        {
            return new ShotItemLayer(
                ItemEffectParams.FromConfiguration(_items),
                new ShotSlotLattice(Separation, Vector2.zero, _grid.Columns, _grid.Rows), GamePalette.RainbowColorId);
        }

        private void ApplyRainbowBuff()
        {
            _ball.Direction = Diagonal;
            _ball.AddBuff(new ProjectileBuff(
                ProjectileBuffId.RainbowShield, 0f, BuffModifierOp.Flat,
                new WallBounceEndCondition(Quiet<ShieldLostMessage>(), _ball)));
        }

        private static ShotSolveContext ContextOver(
            List<ShotBalloonSnapshot> board, Vector2 origin, int rippleDamage, float rippleRadius = RippleRadius)
        {
            return new ShotSolveContext(
                board, new Vector4(1000f, 1000f, -1000f, -1000f), origin, Vector3.zero, 0, true, ContactRadius, 1f,
                default, null, 0f, Colors, GamePalette.RainbowColorId, null, rippleDamage, rippleRadius);
        }

        // A target's slot and nothing else of its balance: the simulator finds a ripple's targets by slot.
        private static BalanceProfile SlotOnly(Vector2Int slot)
        {
            return new BalanceProfile(slot, 0, 0, 0f, false, null);
        }

        // Walls across the column between two rows: the top one bounces the shot back down.
        private Vector4 ColumnWalls(int topRow, int bottomRow)
        {
            var top = _grid.IndexToWorldPosition(new Vector2Int(3, topRow)).y;
            var bottom = _grid.IndexToWorldPosition(new Vector2Int(3, bottomRow)).y;
            return new Vector4(top, 1000f, bottom, -1000f);
        }

        private void RipenAtTheCap()
        {
            _ripe.Tick();
        }

        private Vector2 OriginBefore(Vector2Int slot, Vector2 direction)
        {
            return (Vector2)_grid.IndexToWorldPosition(slot) - (direction * 0.5f);
        }

        private BalloonModel Plain(Vector2Int slot, string color)
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1, scoreValue: 1), _palette, Colors);
            balloon.Color.Value = color;
            return Place(balloon, slot);
        }

        private BubbleClusterModel Cluster(Vector2Int slot, int bubbles)
        {
            var cluster = new BubbleClusterModel(
                new BalloonModelConfig(typeName: BalloonType.BubbleCluster, hitsToPop: 3, scoreValue: 1), _palette,
                Colors);
            cluster.SetBubbles(bubbles);
            return Place(cluster, slot);
        }

        private T Place<T>(T actor, Vector2Int slot)
            where T : IWriteableSlotActor
        {
            _grid.Place(actor, null, slot);
            return actor;
        }

        // The bomb's item settings as the game's configuration holds them: one hit of plain damage, and a blast radius
        // that reaches past the hex neighbours.
        private static IItemConfiguration BombConfiguration(float radius)
        {
            var bomb = new ItemSettings();
            SetField(bomb, "_type", ItemType.Bomb);
            SetField(bomb, "_damage", 1);
            SetField(bomb, "_damageFlags", DamageFlags.Normal);
            SetField(bomb.Bomb, "_bombRadius", radius);
            SetField(bomb.Bomb, "_bombRainbowConversionRange", 0f);

            var items = Substitute.For<IItemConfiguration>();
            items.Items.Returns(new List<ItemSettings> { bomb });
            items[ItemType.Bomb].Returns(bomb);
            return items;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
        }

        private static ISubscriber<T> Quiet<T>()
        {
            return Capture<T>(_ => { });
        }

        private static ISubscriber<T> Capture<T>(Action<IMessageHandler<T>> keep)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(Arg.Any<IMessageHandler<T>>(), Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(call =>
                {
                    keep(call.Arg<IMessageHandler<T>>());
                    return Substitute.For<IDisposable>();
                });
            return subscriber;
        }

        // What HitPipeline does that matters here, in its order: score and streak, then the balloon leaves its slot if
        // the hit popped it, then the hit is heard (the ripe service queues a burst).
        private sealed class GameDispatcher : IHitDispatcher
        {
            private readonly SlotGrid _grid;
            private readonly ScoreController _score;
            private readonly Action<ActorHitMessage> _publish;

            public GameDispatcher(SlotGrid grid, ScoreController score, Action<ActorHitMessage> publish)
            {
                _grid = grid;
                _score = score;
                _publish = publish;
            }

            public void Dispatch(ActorHitMessage msg)
            {
                _score.OnActorHit(msg);
                var slot = msg.Actor.SlotIndex;
                if ((msg.Outcome & HitOutcome.Pop) != 0 && ReferenceEquals(_grid.At(slot), msg.Actor))
                {
                    _grid.Remove(slot);
                }

                _publish(msg);
            }
        }
    }
}

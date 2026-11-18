using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Nudge;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The ripe cluster's fuse and ripple over a real grid, driven by a fake level clock and <c>Tick</c>: a cluster
    ///     at the merge cap ripens, its fuse follows the clock (and holds when it holds), runs out once as foam (its
    ///     bubbles handed to its neighbours, then it leaves), or ends in a ripple the burst delay after a hit pops it. The ripple's hits go through a dispatcher that does what the game's does for
    ///     these tests: a popped balloon leaves its slot, then the hit is heard. A level change, game over or run
    ///     reset forgets every fuse and every burst still waiting.
    ///     The grid is (1, 0.85) apart: from a slot, its two neighbours on the row sit 2 away, the four on the rows
    ///     above and below 1.31, the slot two rows straight up or down 1.7, and nothing else nearer than 2.6. The
    ///     ripple's radius is 2.05 unless a test sets another, so a blast takes the six neighbours and those two.
    /// </summary>
    [TestFixture]
    public class RipeBubbleServiceTests
    {
        private const int MergeMax = 6;
        private const float Fuse = 6f;
        private const float Burst = 0.35f;
        private const float FoamStep = 0.12f;
        private const float Radius = 2.05f;

        private static readonly Vector2Int Slot = new(1, 1);
        private static readonly Vector2Int OtherSlot = new(4, 6);
        private static readonly Vector2Int Centre = new(2, 4);

        private readonly List<BubbleRipenedMessage> _ripened = new();
        private readonly List<RipeBubbleBurstMessage> _bursts = new();
        // The fast beats, by the clock's time; every beat, slow ones too, is counted in _allBeats.
        private readonly List<float> _beats = new();

        // The clusters that left the board quietly, and how long each one's view was to linger.
        private readonly List<(IBalloonModel Model, float Linger)> _left = new();

        // Each nudge the burst published, and how many hits had been dispatched by then.
        private readonly List<(NudgeMessage Message, int HitsBefore)> _nudges = new();
        private readonly NudgeOverride[] _blastNudge = { new(NudgeType.Shockwave, 1.2f, 0.35f, 1.2f) };
        private int _allBeats;

        private SlotGrid _grid;
        private IGamePalette _palette;
        private ReactiveProperty<int> _level;
        private IMessageHandler<ActorHitMessage> _hitHandler;
        private IMessageHandler<BubbleMergedMessage> _mergedHandler;
        private IMessageHandler<GameOverMessage> _gameOverHandler;
        private GridDispatcher _dispatcher;
        private IProjectileModel _shot;
        private RipeBubbleService _service;
        private float _now;
        private float _fuseSeconds;
        private float _burstSeconds;
        private float _rippleRadius;

        // Runs as each burst message goes out, for a test that acts in the middle of a spray.
        private Action<RipeBubbleBurstMessage> _onBurst;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gridConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());

            _now = 0f;
            _fuseSeconds = Fuse;
            _burstSeconds = Burst;
            _rippleRadius = Radius;
            var clock = Substitute.For<ISpawnClock>();
            clock.SecondsOnLevel.Returns(_ => _now);

            var settings = Substitute.For<IRipeBubbleSettings>();
            settings.FuseSeconds.Returns(_ => _fuseSeconds);
            settings.RippleDamage.Returns(1);
            settings.RippleRadius.Returns(_ => _rippleRadius);
            settings.BlastNudgeOverrides.Returns(_blastNudge);
            settings.BurstSeconds.Returns(_ => _burstSeconds);
            settings.FoamStepSeconds.Returns(FoamStep);
            settings.CoatLayers.Returns(1);
            settings.ThrobFastSeconds.Returns(2f);
            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.BubbleMergeMax.Returns(MergeMax);
            balloons.RipeBubble.Returns(settings);

            _level = new ReactiveProperty<int>(1);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);

            _ripened.Clear();
            _bursts.Clear();
            _beats.Clear();
            _allBeats = 0;
            _onBurst = null;
            var ripenedPublisher = Substitute.For<IPublisher<BubbleRipenedMessage>>();
            ripenedPublisher.When(p => p.Publish(Arg.Any<BubbleRipenedMessage>()))
                .Do(call => _ripened.Add(call.Arg<BubbleRipenedMessage>()));
            var burstPublisher = Substitute.For<IPublisher<RipeBubbleBurstMessage>>();
            burstPublisher.When(p => p.Publish(Arg.Any<RipeBubbleBurstMessage>()))
                .Do(call =>
                {
                    var message = call.Arg<RipeBubbleBurstMessage>();
                    _bursts.Add(message);
                    _onBurst?.Invoke(message);
                });

            var beatPublisher = Substitute.For<IPublisher<RipeFuseBeatMessage>>();
            beatPublisher.When(p => p.Publish(Arg.Any<RipeFuseBeatMessage>()))
                .Do(call =>
                {
                    _allBeats++;
                    if (call.Arg<RipeFuseBeatMessage>().Fast)
                    {
                        _beats.Add(_now);
                    }
                });

            _shot = Substitute.For<IProjectileModel>();
            _dispatcher = new GridDispatcher(_grid, message => _hitHandler.Handle(message));
            var disturbanceField = new DisturbanceFieldService(
                Substitute.For<IDisturbanceFieldSettings>(), Substitute.For<IGameDisplayConfiguration>(),
                new ImpactEventBus());
            _left.Clear();
            var popper = Substitute.For<IBalloonPopper>();
            popper.TryLeave(Arg.Any<IBalloonModel>(), Arg.Any<float>()).Returns(call =>
            {
                var model = call.Arg<IBalloonModel>();
                _grid.Remove(model.SlotIndex.Value);
                _left.Add((model, call.Arg<float>()));
                return true;
            });
            // The flash is held for no time (the settings' 0), so the light service is never started.
            var lightField = new SceneLightFieldService(
                Substitute.For<IGameDisplayConfiguration>(), _palette, Substitute.For<ISceneLightFieldSettings>());
            _nudges.Clear();
            var nudgePublisher = Substitute.For<IPublisher<NudgeMessage>>();
            nudgePublisher.When(p => p.Publish(Arg.Any<NudgeMessage>()))
                .Do(call => _nudges.Add((call.Arg<NudgeMessage>(), _dispatcher.Dispatched.Count)));
            var burst = new RipeBubbleBurst(
                _grid, _dispatcher, popper, balloons, disturbanceField, lightField, nudgePublisher);

            _service = new RipeBubbleService(
                _grid, clock, balloons, levelProgress, burst, Capture<ActorHitMessage>(h => _hitHandler = h),
                Capture<BubbleMergedMessage>(h => _mergedHandler = h),
                Capture<GameOverMessage>(h => _gameOverHandler = h), ripenedPublisher, burstPublisher,
                beatPublisher);
            _service.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void AClusterAtTheCap_Ripens_AndAnnouncesIt()
        {
            var cluster = Place(Slot, MergeMax);

            _service.Tick();

            Assert.IsTrue(cluster.Ripe.Value);
            Assert.AreEqual(Fuse, cluster.FuseLeft.Value);
            Assert.AreEqual(1, _ripened.Count);
            Assert.AreEqual(Slot, _ripened[0].Slot);
            Assert.AreEqual(_grid.IndexToWorldPosition(Slot), _ripened[0].Position);
        }

        [Test]
        public void AClusterBelowTheCap_DoesNotRipen()
        {
            var cluster = Place(Slot, MergeMax - 1);

            _service.Tick();

            Assert.IsFalse(cluster.Ripe.Value);
            Assert.IsEmpty(_ripened);
        }

        [Test]
        public void ARipeCluster_RipensOnce()
        {
            Place(Slot, MergeMax);
            _service.Tick();

            Place(OtherSlot, 2);
            _service.Tick();

            Assert.AreEqual(1, _ripened.Count);
            Assert.AreEqual(1, _service.Tracked);
        }

        [Test]
        public void AMergeThatFillsACluster_IsLookedAtOnTheNextTick()
        {
            var cluster = Place(Slot, MergeMax - 1);
            _service.Tick();

            // The merge message goes out before the counts change, with no grid change after.
            _mergedHandler.Handle(default);
            cluster.SetBubbles(MergeMax);
            _service.Tick();

            Assert.IsTrue(cluster.Ripe.Value);
        }

        [Test]
        public void TheFuse_CountsDownWithTheClock_AndHoldsWhenItHolds()
        {
            var cluster = Place(Slot, MergeMax);
            _now = 10f;
            _service.Tick();

            _now = 12f;
            _service.Tick();
            Assert.AreEqual(4f, cluster.FuseLeft.Value);

            // A pause, the level-up or game over: the level clock stands still, and so does the fuse.
            _service.Tick();
            _service.Tick();
            Assert.AreEqual(4f, cluster.FuseLeft.Value);
            Assert.IsTrue(cluster.Ripe.Value);
        }

        // Fuse 6 with a fast window of 2: half-second beats at 2, 1.5, 1 and 0.5 seconds left, each heard once
        // however finely the clock steps, none while it holds and none as the fuse runs out.
        [Test]
        public void TheFastThrobs_AreAnnouncedOnceEach_InTheLastSeconds()
        {
            PlaceRipe();

            for (var step = 1; step <= 60; step++)
            {
                _now = step * 0.1f;
                _service.Tick();
                _service.Tick();
            }

            CollectionAssert.AreEqual(new[] { 4f, 4.5f, 5f, 5.5f }, RoundedBeats());

            // The slow throbs before them (5, 4 and 3 seconds left) are announced too, as beats that are not fast.
            Assert.AreEqual(7, _allBeats);
        }

        [Test]
        public void ABigStepAcrossSeveralBeats_IsAnnouncedOnce()
        {
            PlaceRipe();

            _now = 3.9f;
            _service.Tick();
            _now = 5.2f;
            _service.Tick();

            Assert.AreEqual(1, _beats.Count);
        }

        [Test]
        public void TheFuseRunningOut_HandsTheBubblesOutOnce_AndTheClusterLeaves()
        {
            var cluster = Place(Slot, MergeMax);
            _service.Tick();

            _now = Fuse;
            _service.Tick();
            _service.Tick();

            Assert.IsNull(_grid.At(Slot), "the cluster has left the board");
            Assert.IsTrue(cluster.Ripe.Value, "it leaves in its warning colour");
            Assert.AreEqual(1, _left.Count);
            Assert.AreSame(cluster, _left[0].Model);
            Assert.AreEqual(MergeMax * FoamStep, _left[0].Linger, 1e-4f, "its view lingers until the last bubble leaves");
            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(RipeBurstKind.Foam, _bursts[0].Kind);
            Assert.AreEqual(Slot, _bursts[0].Slot);
            Assert.AreEqual(0, _bursts[0].Reached);
            Assert.AreEqual(MergeMax, _bursts[0].Bubbles, "with no takers every bubble is a leftover");
            for (var bubble = 0; bubble < MergeMax; bubble++)
            {
                Assert.AreEqual(FoamGift.Leftover, _bursts[0].GiftOf(bubble, out _));
            }

            Assert.IsEmpty(_dispatcher.Dispatched, "leaving dispatches no hit");
            Assert.AreEqual(0, _service.Tracked);
        }

        [Test]
        public void ARipeClusterPoppedByAHit_BurstsAsARipple_BurstSecondsLater()
        {
            var cluster = Place(Slot, MergeMax);
            _service.Tick();

            var context = new DamageContext(1, DamageFlags.DirectHit);
            var outcome = cluster.EvaluateHit(context);
            _grid.Remove(Slot);
            _hitHandler.Handle(new ActorHitMessage(cluster, new Vector3(1f, 2f, 0f), Vector3.up, outcome, context));

            Assert.IsEmpty(_bursts, "nothing is set off inside the hit's own dispatch");

            _service.Tick();
            _now = Burst - 0.01f;
            _service.Tick();
            Assert.IsEmpty(_bursts, "the cluster is still folding");

            _now = Burst;
            _service.Tick();
            _service.Tick();

            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(RipeBurstKind.Ripple, _bursts[0].Kind);
            Assert.AreEqual(Slot, _bursts[0].Slot);
            Assert.AreEqual(_grid.IndexToWorldPosition(Slot), _bursts[0].Position);
            Assert.AreEqual(0, _bursts[0].Reached);
            Assert.AreEqual(0, _service.Tracked);
        }

        [Test]
        public void AHitThatDoesNotPop_LeavesTheFuseRunning()
        {
            var cluster = Place(Slot, MergeMax);
            _service.Tick();

            _hitHandler.Handle(new ActorHitMessage(cluster, Vector3.zero, Vector3.up, HitOutcome.PassThrough));
            _service.Tick();

            Assert.IsEmpty(_bursts);
            Assert.AreEqual(1, _service.Tracked);
        }

        [Test]
        public void AClusterThatMovesSlot_KeepsItsFuse()
        {
            var cluster = Place(Slot, MergeMax);
            _now = 1f;
            _service.Tick();

            _grid.Remove(Slot);
            _grid.Place(cluster, null, OtherSlot);
            _now = 3f;
            _service.Tick();

            Assert.IsTrue(cluster.Ripe.Value);
            Assert.AreEqual(4f, cluster.FuseLeft.Value);
            Assert.AreEqual(1, _ripened.Count, "a move does not start a second fuse");

            _now = 7f;
            _service.Tick();

            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(OtherSlot, _bursts[0].Slot);
        }

        [Test]
        public void AClusterThatLeavesTheBoardUnpopped_IsForgottenAndCleared()
        {
            var cluster = Place(Slot, MergeMax);
            _service.Tick();

            _grid.Remove(Slot);
            _service.Tick();

            Assert.AreEqual(0, _service.Tracked);
            Assert.IsFalse(cluster.Ripe.Value);
            Assert.IsEmpty(_bursts);
        }

        [Test]
        public void ALevelChange_ForgetsEveryFuse()
        {
            var cluster = PlaceRipe();

            _level.Value = 2;

            AssertForgotten(cluster);
        }

        [Test]
        public void GameOver_ForgetsEveryFuse()
        {
            var cluster = PlaceRipe();

            _gameOverHandler.Handle(default);

            AssertForgotten(cluster);
        }

        [Test]
        public void ARunReset_ForgetsEveryFuse()
        {
            var cluster = PlaceRipe();

            _service.ResetRun(1);

            AssertForgotten(cluster);
        }

        [Test]
        public void AStop_LooksAgainOnTheNextTick_EvenOnABoardThatDoesNotChange()
        {
            var cluster = PlaceRipe();
            _service.ResetRun(1);
            Assert.IsFalse(cluster.Ripe.Value);

            _service.Tick();

            Assert.IsTrue(cluster.Ripe.Value);
            Assert.AreEqual(1, _service.Tracked);
        }

        [Test]
        public void APopHeardBeforeAReset_IsNotBurst()
        {
            var cluster = PlaceRipe();
            var outcome = cluster.EvaluateHit(new DamageContext(1));
            _hitHandler.Handle(new ActorHitMessage(cluster, Vector3.zero, Vector3.up, outcome));

            _service.ResetRun(1);
            _service.Tick();

            Assert.IsEmpty(_bursts);
        }

        [Test]
        public void APopHeardBeforeALevelChange_RipplesNothing()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var plain = PlacePlain(neighbors[0]);
            _service.Tick();
            HitDirect(cluster);

            _level.Value = 2;
            _service.Tick();
            AfterBurst();

            Assert.AreEqual(0, _service.Pending);
            Assert.AreEqual(0, HitsOn(plain));
            Assert.IsEmpty(_bursts);
        }

        [Test]
        public void GameOverWhileTheClusterFolds_DropsItsRipple()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var plain = PlacePlain(neighbors[0]);
            _service.Tick();
            HitDirect(cluster);
            _now = Burst * 0.5f;
            _service.Tick();
            Assert.AreEqual(1, _service.Pending);

            _gameOverHandler.Handle(default);
            AfterBurst();
            AfterBurst();

            Assert.AreEqual(0, _service.Pending);
            Assert.AreEqual(0, HitsOn(plain));
            Assert.IsEmpty(_bursts, "no ripple, so telemetry counts none");
        }

        [Test]
        public void AFuseOfZeroSeconds_NeverRipens()
        {
            _fuseSeconds = 0f;
            var cluster = Place(Slot, MergeMax);

            _service.Tick();

            Assert.IsFalse(cluster.Ripe.Value);
            Assert.IsEmpty(_ripened);
        }

        [Test]
        public void TheRipple_HitsEachNeighbourThatTakesItOnce_WithTheShot()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var plainA = PlacePlain(neighbors[0]);
            var heart = PlaceActor(new HeartBalloonModel(new BalloonModelConfig(typeName: BalloonType.Heart)),
                neighbors[1]);
            var unbreakable = PlaceActor(
                new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)), neighbors[2]);
            var smallCluster = Place(neighbors[3], 3);
            var plainB = PlacePlain(neighbors[5]);
            var faraway = PlacePlain(OtherSlot);
            _service.Tick();

            HitDirect(cluster);
            AfterBurst();

            Assert.AreEqual(1, HitsOn(plainA));
            Assert.AreEqual(1, HitsOn(smallCluster));
            Assert.AreEqual(1, HitsOn(plainB));
            Assert.AreEqual(0, HitsOn(heart), "a reward balloon is skipped");
            Assert.AreEqual(0, HitsOn(unbreakable), "a balloon that bounces an ordinary hit is skipped");
            Assert.AreEqual(0, HitsOn(faraway));

            var ripples = Ripples();
            Assert.AreEqual(3, ripples.Count);
            foreach (var hit in ripples)
            {
                Assert.AreEqual(1, hit.Context.Damage);
                Assert.AreEqual(DamageFlags.Ripple, hit.Context.Flags, "no DirectHit, nothing else");
                Assert.AreSame(_shot, hit.Context.Projectile);
                var slot = ((ISlotActor)hit.Actor).SlotIndex;
                Assert.AreEqual(_grid.IndexToWorldPosition(slot), hit.WorldPosition);
                var expected = (_grid.IndexToWorldPosition(slot) - _grid.IndexToWorldPosition(Centre)).normalized;
                Assert.AreEqual(expected, hit.ProjectileDirection);
            }

            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(RipeBurstKind.Ripple, _bursts[0].Kind);
            Assert.AreEqual(Centre, _bursts[0].Slot);
            Assert.AreEqual(_grid.IndexToWorldPosition(Centre), _bursts[0].Position);
            Assert.AreEqual(3, _bursts[0].Reached);
            Assert.AreEqual(Radius, _bursts[0].Radius);
        }

        // A blast, not a ring of neighbours: the slots two rows straight up and down are 1.7 away, inside the radius,
        // and are hit; the next slot along the row and the one up the diagonal past it are 4 and 2.6 away, and are not.
        [Test]
        public void TheRipple_ReachesPastTheNeighbours_ToEverySlotWithinItsRadius()
        {
            var cluster = Place(Centre, MergeMax);
            var above = PlacePlain(new Vector2Int(2, 2));
            var below = PlacePlain(new Vector2Int(2, 6));
            var alongTheRow = PlacePlain(new Vector2Int(0, 4));
            var upTheDiagonal = PlacePlain(new Vector2Int(3, 2));
            _service.Tick();

            HitDirect(cluster);
            AfterBurst();

            Assert.AreEqual(1, HitsOn(above), "1.7 away");
            Assert.AreEqual(1, HitsOn(below), "1.7 away");
            Assert.AreEqual(0, HitsOn(alongTheRow), "4 away");
            Assert.AreEqual(0, HitsOn(upTheDiagonal), "2.6 away");
            Assert.AreEqual(2, _bursts[0].Reached);
            Assert.AreEqual(Radius, _bursts[0].Radius);
        }

        // The radius decides, not adjacency: at 1.5 the neighbours on the rows above and below (1.31 away) are in,
        // and the two neighbours on the cluster's own row (2 away) are out.
        [Test]
        public void ASmallerRadius_LeavesOutTheNeighboursItDoesNotReach()
        {
            _rippleRadius = 1.5f;
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var onTheRow = PlacePlain(neighbors[0]);
            var onTheRowAbove = PlacePlain(neighbors[2]);
            _service.Tick();

            HitDirect(cluster);
            AfterBurst();

            Assert.AreEqual(0, HitsOn(onTheRow));
            Assert.AreEqual(1, HitsOn(onTheRowAbove));
            Assert.AreEqual(1.5f, _bursts[0].Radius);
        }

        // The shot solver repeats the blast in this order (ShotSimulator.CollectRippleSlots): by column, then by row.
        [Test]
        public void TheRipple_HitsColumnByColumn_ThenRowByRow()
        {
            var cluster = Place(Centre, MergeMax);
            PlacePlain(new Vector2Int(3, 4));
            PlacePlain(new Vector2Int(2, 5));
            PlacePlain(new Vector2Int(2, 2));
            PlacePlain(new Vector2Int(2, 3));
            PlacePlain(new Vector2Int(1, 4));
            _service.Tick();

            HitDirect(cluster);
            AfterBurst();

            var hit = new List<Vector2Int>();
            foreach (var ripple in Ripples())
            {
                hit.Add(((ISlotActor)ripple.Actor).SlotIndex);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector2Int(1, 4), new Vector2Int(2, 2), new Vector2Int(2, 3), new Vector2Int(2, 5),
                    new Vector2Int(3, 4)
                },
                hit);
        }

        // The cluster left its slot when it popped. A balloon the board moves there before the ripple lands stands
        // in the middle of the blast.
        [Test]
        public void ABalloonMovedIntoTheBurstsOwnSlot_IsInTheBlast()
        {
            var cluster = Place(Centre, MergeMax);
            _service.Tick();

            HitDirect(cluster);
            var movedIn = PlacePlain(Centre);
            AfterBurst();

            Assert.AreEqual(1, HitsOn(movedIn));
            Assert.AreEqual(1, PopsOf(cluster), "the cluster itself is hit once, by the shot");
        }

        // As a bomb's blast does: one shockwave from the burst's centre with the burst's own overrides, sent before
        // the ripple's hits.
        [Test]
        public void TheBurst_ShovesTheBoardOutward_BeforeItsHits()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            PlacePlain(neighbors[0]);
            _service.Tick();

            HitDirect(cluster);
            Assert.IsEmpty(_nudges, "nothing is shoved while the cluster folds");
            AfterBurst();

            Assert.AreEqual(1, _nudges.Count);
            var (message, hitsBefore) = _nudges[0];
            Assert.AreEqual(NudgeType.Shockwave, message.Source);
            Assert.IsNull(message.Actor, "the whole board, as a bomb's shockwave");
            Assert.AreEqual(_grid.IndexToWorldPosition(Centre), message.Origin);
            Assert.AreSame(_blastNudge, message.Overrides);
            Assert.AreEqual(1, hitsBefore, "only the shot's own hit had landed");
            Assert.AreEqual(1, Ripples().Count);
        }

        // A ripe cluster two rows up is inside the blast without being a neighbour: it bursts, and its own ripple
        // goes out from its own slot, reaching a balloon the first blast was too far from.
        [Test]
        public void ARipeClusterInsideTheRadiusButNotNextDoor_BurstsInTurn_FromItsOwnCentre()
        {
            var secondSlot = new Vector2Int(2, 2);
            Assert.IsTrue(Array.IndexOf(NeighborSlots(Centre), secondSlot) < 0, "not a neighbour");
            var first = Place(Centre, MergeMax);
            var second = Place(secondSlot, MergeMax);
            var beyond = PlacePlain(new Vector2Int(2, 0));
            _service.Tick();

            HitDirect(first);
            AfterBurst();

            Assert.AreEqual(1, PopsOf(second), "the first blast bursts it");
            Assert.AreEqual(0, HitsOn(beyond), "3.4 from the first: out of its blast");
            Assert.AreEqual(1, _bursts.Count, "the second is still folding");

            AfterBurst();

            Assert.AreEqual(2, _bursts.Count);
            Assert.AreEqual(secondSlot, _bursts[1].Slot);
            Assert.AreEqual(_grid.IndexToWorldPosition(secondSlot), _bursts[1].Position);
            Assert.AreEqual(1, HitsOn(beyond), "1.7 from the second: in its blast");
            Assert.AreEqual(1, _bursts[1].Reached);
            Assert.AreEqual(1, _dispatcher.MaxDepth, "no ripple is dispatched inside another hit");
        }

        [Test]
        public void TheRipple_IsNotDispatchedInsideTheHitThatPoppedTheCluster()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var plain = PlacePlain(neighbors[0]);
            _service.Tick();

            HitDirect(cluster);

            Assert.AreEqual(0, HitsOn(plain), "no neighbour is hit inside the hit");
            Assert.IsEmpty(_bursts);

            AfterBurst();

            Assert.AreEqual(1, HitsOn(plain));
        }

        [Test]
        public void AnUnripeNeighbouringCluster_LosesOneBubbleAndStays()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var small = Place(neighbors[0], 3);
            _service.Tick();

            HitDirect(cluster);
            AfterBurst();

            Assert.AreEqual(2, small.HitsRemaining.Value);
            Assert.AreSame(small, _grid.At(neighbors[0]));
            Assert.AreEqual(1, _bursts.Count, "it does not burst");
        }

        [Test]
        public void RipeNeighbours_EachBurstBurstSecondsAfterItsOwnHit_EachOnce()
        {
            var (first, second, third) = PlaceChainOfThree();

            HitDirect(first);
            _service.Tick();
            Assert.IsEmpty(_bursts, "the first is still folding");

            AfterBurst();

            Assert.IsNull(_grid.At(second.SlotIndex.Value), "the ripple bursts a ripe neighbour");
            Assert.AreSame(third, _grid.At(third.SlotIndex.Value));
            Assert.AreEqual(1, _bursts.Count);

            _now += Burst - 0.01f;
            _service.Tick();
            Assert.AreEqual(1, _bursts.Count, "the second folds as long as the first did");

            _now += 0.01f;
            _service.Tick();
            Assert.AreEqual(2, _bursts.Count);
            Assert.IsNull(_grid.At(third.SlotIndex.Value), "and the chain goes on to the third");

            AfterBurst();
            AfterBurst();

            Assert.AreEqual(3, _bursts.Count);
            Assert.AreEqual(first.SlotIndex.Value, _bursts[0].Slot);
            Assert.AreEqual(second.SlotIndex.Value, _bursts[1].Slot);
            Assert.AreEqual(third.SlotIndex.Value, _bursts[2].Slot);
            Assert.AreEqual(1, PopsOf(first));
            Assert.AreEqual(1, PopsOf(second));
            Assert.AreEqual(1, PopsOf(third));
            Assert.AreEqual(1, _dispatcher.MaxDepth, "no ripple is dispatched inside another hit");
            foreach (var hit in Ripples())
            {
                Assert.AreSame(_shot, hit.Context.Projectile, "a chain still carries the shot");
            }
        }

        [Test]
        public void AChainWithNoDelay_RunsInOneTick_WithoutRecursing()
        {
            _burstSeconds = 0f;
            var (first, second, third) = PlaceChainOfThree();

            HitDirect(first);
            _service.Tick();

            Assert.AreEqual(3, _bursts.Count);
            Assert.AreEqual(1, PopsOf(second));
            Assert.AreEqual(1, PopsOf(third));
            Assert.AreEqual(1, _dispatcher.MaxDepth);
            Assert.AreEqual(0, _service.Pending);
        }

        [Test]
        public void AFuseRunningOutWhileAnotherBurstIsPending_EachEndsOnce()
        {
            var popped = Place(Centre, MergeMax);
            var expiring = Place(OtherSlot, MergeMax);
            _service.Tick();

            _now = Fuse - 0.1f;
            HitDirect(popped);
            _now = Fuse;
            _service.Tick();
            AfterBurst();

            Assert.AreEqual(2, _bursts.Count);
            Assert.AreEqual(1, CountBursts(RipeBurstKind.Ripple, Centre));
            Assert.AreEqual(1, CountBursts(RipeBurstKind.Foam, OtherSlot));
            Assert.IsNull(_grid.At(OtherSlot), "the expired cluster handed its bubbles out and left");
            Assert.IsTrue(expiring.Ripe.Value);
        }

        [Test]
        public void ARippleReachingAClusterWhoseFuseRunsOutThatTick_BurstsIt_AndSpraysNoFoam()
        {
            var neighbors = NeighborSlots(Centre);
            var popped = Place(Centre, MergeMax);
            var expiring = Place(neighbors[0], MergeMax);
            _service.Tick();

            _now = Fuse - Burst;
            HitDirect(popped);
            _now = Fuse;
            _service.Tick();
            AfterBurst();

            Assert.AreEqual(2, _bursts.Count);
            Assert.AreEqual(1, CountBursts(RipeBurstKind.Ripple, Centre));
            Assert.AreEqual(1, CountBursts(RipeBurstKind.Ripple, neighbors[0]));
            Assert.AreEqual(1, PopsOf(expiring));
            Assert.IsEmpty(_left, "nothing left the board quietly");
        }

        [Test]
        public void AHitBeforeTheTickTheFuseRunsOut_BurstsItAsARipple_AndNoFoam()
        {
            var cluster = Place(Centre, MergeMax);
            _service.Tick();

            _now = Fuse;
            HitDirect(cluster);
            _service.Tick();
            AfterBurst();

            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(RipeBurstKind.Ripple, _bursts[0].Kind);
        }

        [Test]
        public void AHitAfterTheTickTheFuseRanOut_SetsNothingOff()
        {
            var cluster = Place(Centre, MergeMax);
            _service.Tick();

            _now = Fuse;
            _service.Tick();
            HitDirect(cluster);
            AfterBurst();

            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(RipeBurstKind.Foam, _bursts[0].Kind);
            Assert.AreEqual(0, _service.Pending, "the cluster has left: a hit on it queues no burst");
        }

        [Test]
        public void BurstsHeardAtDifferentTimes_EachWaitsItsOwnDelay()
        {
            var early = Place(Slot, MergeMax);
            var late = Place(OtherSlot, MergeMax);
            _service.Tick();

            HitWith(early, DamageFlags.Ripple);
            _now = 0.2f;
            HitDirect(late);
            Assert.AreEqual(2, _service.Pending);

            _now = Burst;
            _service.Tick();

            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(Slot, _bursts[0].Slot);
            Assert.AreEqual(1, _service.Pending);

            _now = 0.2f + Burst;
            _service.Tick();

            Assert.AreEqual(2, _bursts.Count);
            Assert.AreEqual(OtherSlot, _bursts[1].Slot);
            Assert.AreEqual(0, _service.Pending);
        }

        [Test]
        public void TwoBurstsDueAtOnce_EachRunsOnce()
        {
            var first = Place(Slot, MergeMax);
            var second = Place(OtherSlot, MergeMax);
            _service.Tick();

            HitDirect(first);
            HitDirect(second);
            AfterBurst();
            _service.Tick();

            Assert.AreEqual(2, _bursts.Count);
            Assert.AreEqual(1, CountBursts(RipeBurstKind.Ripple, Slot));
            Assert.AreEqual(1, CountBursts(RipeBurstKind.Ripple, OtherSlot));
            Assert.AreEqual(0, _service.Pending);
        }

        [Test]
        public void ARippleWhoseHitsThrow_DoesNotRunAgain()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var plain = PlacePlain(neighbors[0]);
            _service.Tick();
            HitDirect(cluster);

            _now += Burst;
            _dispatcher.ThrowOnce = true;
            Assert.Throws<InvalidOperationException>(() => _service.Tick());
            _service.Tick();
            _service.Tick();

            Assert.AreEqual(1, HitsOn(plain), "the burst left the queue before its hits went out");
            Assert.AreEqual(0, _service.Pending);
        }

        [Test]
        public void GameOverInTheMiddleOfARipple_StopsEverything()
        {
            var (first, second, third) = PlaceChainOfThree();
            var elsewhere = Place(new Vector2Int(5, 0), MergeMax);
            _service.Tick();
            Assert.IsTrue(elsewhere.Ripe.Value);
            HitDirect(first);

            // As in the game, the score stage runs before the hit is heard: game over lands before the service hears
            // that the ripple popped the ripe neighbour.
            _dispatcher.BeforePublish = hit =>
            {
                if ((hit.Context.Flags & DamageFlags.Ripple) != 0)
                {
                    _dispatcher.BeforePublish = null;
                    _gameOverHandler.Handle(default);
                }
            };
            AfterBurst();

            Assert.IsEmpty(_bursts, "the stopped ripple announces nothing");
            Assert.AreEqual(0, _service.Pending);
            Assert.AreEqual(0, _service.Tracked);
            Assert.IsFalse(second.Ripe.Value);
            Assert.IsFalse(third.Ripe.Value, "nothing is left ripe");
            Assert.IsFalse(elsewhere.Ripe.Value);

            // Later ticks look again, as after any stop, but nothing that was queued runs.
            var ripplesAtStop = Ripples().Count;
            _now += Burst * 10f;
            _service.Tick();
            _service.Tick();

            Assert.IsEmpty(_bursts);
            Assert.AreEqual(ripplesAtStop, Ripples().Count, "no further burst runs");
            Assert.AreEqual(0, PopsOf(third));
        }

        [Test]
        public void GameOverWhileFoamIsAnnounced_LeavesTheClustersStillWaitingUnripeAndInPlace()
        {
            var first = Place(Slot, MergeMax);
            var second = Place(OtherSlot, MergeMax);
            _service.Tick();

            _onBurst = _ =>
            {
                _onBurst = null;
                _gameOverHandler.Handle(default);
            };
            _now = Fuse;
            _service.Tick();

            Assert.AreEqual(1, _bursts.Count, "the spray stops with the game");
            var sprayedSlot = _bursts[0].Slot;
            var waiting = sprayedSlot == Slot ? second : first;
            Assert.IsNull(_grid.At(sprayedSlot), "the announced hand-out still runs, and its cluster leaves");
            Assert.IsFalse(waiting.Ripe.Value, "none is left ripe with no fuse");
            Assert.AreEqual(0f, waiting.FuseLeft.Value);
            Assert.AreEqual(MergeMax, waiting.HitsRemaining.Value, "and it handed nothing out");
            Assert.AreSame(waiting, _grid.At(waiting.SlotIndex.Value));
        }

        [Test]
        public void AnExpiredClusterThatLeftItsSlotBeforeItsFoam_IsClearedNotSpent()
        {
            var first = Place(Slot, MergeMax);
            var second = Place(OtherSlot, MergeMax);
            _service.Tick();

            _onBurst = message =>
            {
                _onBurst = null;
                _grid.Remove(message.Slot == Slot ? OtherSlot : Slot);
            };
            _now = Fuse;
            _service.Tick();

            Assert.AreEqual(1, _bursts.Count);
            var removed = _bursts[0].Slot == Slot ? second : first;
            Assert.IsFalse(removed.Ripe.Value);
            Assert.AreEqual(MergeMax, removed.HitsRemaining.Value);
        }

        [Test]
        public void TheFoam_CoatsExactlyTheNeighboursThatTakeIt_Once_WithoutAHit()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var plain = PlacePlain(neighbors[0]);
            var carrier = PlacePlain(neighbors[1]);
            carrier.Item.Value = ItemType.Bomb;
            var coated = PlacePlain(neighbors[2]);
            coated.TryCoat(1);
            var unbreakable = PlaceActor(
                new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)), neighbors[3]);
            var tough = PlaceActor(
                new ToughBalloonModel(new BalloonModelConfig(typeName: BalloonType.Tough, hitsToPop: 2)), neighbors[4]);
            var heart = PlaceActor(new HeartBalloonModel(new BalloonModelConfig(typeName: BalloonType.Heart)),
                neighbors[5]);
            var faraway = PlacePlain(OtherSlot);
            _service.Tick();

            _now = Fuse;
            _service.Tick();
            _service.Tick();

            Assert.AreEqual(1, plain.Coat.Value);
            Assert.AreEqual(1, carrier.Coat.Value);
            Assert.AreEqual(1, coated.Coat.Value, "one already at the max takes no more");
            Assert.AreEqual(0, faraway.Coat.Value);
            Assert.AreSame(unbreakable, _grid.At(neighbors[3]));
            Assert.AreEqual(2, tough.HitsRemaining.Value);
            Assert.AreSame(heart, _grid.At(neighbors[5]));
            Assert.IsNull(_grid.At(Centre), "the cluster is gone");
            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(RipeBurstKind.Foam, _bursts[0].Kind);
            Assert.AreEqual(2, _bursts[0].Reached);
            Assert.AreEqual(0b11, _bursts[0].Neighbours, "the plain balloon and the carrier, by direction");
            Assert.AreEqual(MergeMax, _bursts[0].Bubbles);
            Assert.AreEqual(MergeMax - 2, CountGifts(_bursts[0], FoamGift.Leftover), "the rest pop as leftovers");
            Assert.IsEmpty(_dispatcher.Dispatched, "foam dispatches no hit");
        }

        [Test]
        public void SixPlainNeighbours_TakeACoatEach_AndTheClusterLeaves_WithNoHitAndNoScore()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var plains = new BalloonModel[6];
            for (var i = 0; i < plains.Length; i++)
            {
                plains[i] = PlacePlain(neighbors[i]);
            }

            _service.Tick();
            _now = Fuse;
            _service.Tick();

            foreach (var plain in plains)
            {
                Assert.AreEqual(1, plain.Coat.Value);
            }

            Assert.IsNull(_grid.At(Centre));
            Assert.AreSame(cluster, _left[0].Model);
            Assert.AreEqual(0b111111, _bursts[0].Neighbours);
            Assert.AreEqual(6, _bursts[0].Reached, "coats applied, for telemetry");
            for (var bubble = 0; bubble < 6; bubble++)
            {
                Assert.AreEqual(FoamGift.Coat, _bursts[0].GiftOf(bubble, out var direction));
                Assert.AreEqual(bubble, direction, "one each, in direction order");
            }

            Assert.IsEmpty(_dispatcher.Dispatched, "no hit, so nothing scores and the streak is untouched");
            Assert.IsEmpty(_nudges, "the foam shoves nothing: only the player's burst is a blast");
            Assert.AreEqual(0f, _bursts[0].Radius);
        }

        [Test]
        public void ThreePlainNeighboursAndThreeEmpty_TakeThreeCoats_AndThreeBubblesAreLeftOver()
        {
            var neighbors = NeighborSlots(Centre);
            Place(Centre, MergeMax);
            PlacePlain(neighbors[1]);
            PlacePlain(neighbors[3]);
            PlacePlain(neighbors[4]);
            _service.Tick();

            _now = Fuse;
            _service.Tick();

            var message = _bursts[0];
            Assert.AreEqual(6, message.Bubbles);
            var expected = new[] { 1, 3, 4 };
            for (var bubble = 0; bubble < 3; bubble++)
            {
                Assert.AreEqual(FoamGift.Coat, message.GiftOf(bubble, out var direction));
                Assert.AreEqual(expected[bubble], direction);
            }

            for (var bubble = 3; bubble < 6; bubble++)
            {
                Assert.AreEqual(FoamGift.Leftover, message.GiftOf(bubble, out _));
            }

            Assert.IsNull(_grid.At(Centre));
        }

        [Test]
        public void ANeighbouringClusterWithRoom_TakesBubblesUpToTheCap_AndRipensOnALaterTick()
        {
            var neighbors = NeighborSlots(Centre);
            Place(Centre, MergeMax);
            var small = Place(neighbors[2], MergeMax - 2);
            _service.Tick();

            _now = Fuse;
            _service.Tick();

            Assert.AreEqual(MergeMax, small.HitsRemaining.Value, "two bubbles fill it to the cap, no further");
            Assert.IsFalse(small.Ripe.Value, "not in the tick of the hand-out");
            var message = _bursts[0];
            Assert.AreEqual(2, CountGifts(message, FoamGift.Cluster));
            Assert.AreEqual(MergeMax - 2, CountGifts(message, FoamGift.Leftover));
            Assert.AreEqual(0, message.Reached, "a cluster's bubble is no coat");

            _service.Tick();

            Assert.IsTrue(small.Ripe.Value, "the next tick ripens it as any full cluster");
            Assert.AreEqual(2, _ripened.Count);
        }

        [Test]
        public void CoatsGoBeforeClusters_AndLaterRoundsTopTheClustersUp()
        {
            var neighbors = NeighborSlots(Centre);
            Place(Centre, MergeMax);
            var small = Place(neighbors[0], 1);
            var plain = PlacePlain(neighbors[1]);
            _service.Tick();

            _now = Fuse;
            _service.Tick();

            var message = _bursts[0];
            Assert.AreEqual(FoamGift.Coat, message.GiftOf(0, out var first));
            Assert.AreEqual(1, first, "the coat leaves first though the cluster comes first by direction");
            for (var bubble = 1; bubble < 6; bubble++)
            {
                Assert.AreEqual(FoamGift.Cluster, message.GiftOf(bubble, out var direction));
                Assert.AreEqual(0, direction);
            }

            Assert.AreEqual(1, plain.Coat.Value);
            Assert.AreEqual(MergeMax, small.HitsRemaining.Value);
        }

        [Test]
        public void AFullNeighbour_TakesNoBubble_AndBothHandOutTheirOwn()
        {
            var neighbors = NeighborSlots(Centre);
            Place(Centre, MergeMax);
            var full = Place(neighbors[0], MergeMax);
            _service.Tick();

            _now = Fuse;
            _service.Tick();

            Assert.AreEqual(2, _bursts.Count, "both fuses ran out in the same tick");
            foreach (var burst in _bursts)
            {
                Assert.AreEqual(0, CountGifts(burst, FoamGift.Cluster), "a full cluster has no room, nor a gone one");
                Assert.AreEqual(MergeMax, burst.Bubbles, "so all of each one's bubbles are leftovers");
            }

            Assert.IsNull(_grid.At(Centre));
            Assert.IsNull(_grid.At(full.SlotIndex.Value));
            Assert.AreEqual(2, _left.Count, "bubbles are never made: neither grew on the other's");
        }

        [Test]
        public void TheRipple_TakesACoatOff_LeavesTheBalloon_AndCountsItAsReached()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var coated = PlacePlain(neighbors[0]);
            coated.TryCoat(1);
            _service.Tick();

            HitDirect(cluster);
            AfterBurst();

            Assert.AreEqual(0, coated.Coat.Value);
            Assert.AreEqual(1, coated.HitsRemaining.Value);
            Assert.AreSame(coated, _grid.At(neighbors[0]));
            Assert.AreEqual(0, PopsOf(coated));
            Assert.AreEqual(1, _bursts.Count);
            Assert.AreEqual(1, _bursts[0].Reached);
        }

        // A balloon still flying in from its spawn is already in its slot, so foam reaches it.
        [Test]
        public void TheFoam_CoatsABalloonStillSettling()
        {
            var neighbors = NeighborSlots(Centre);
            Place(Centre, MergeMax);
            var settling = PlacePlain(neighbors[0]);
            settling.IsStable.Value = false;
            _service.Tick();

            _now = Fuse;
            _service.Tick();
            _service.Tick();

            Assert.AreEqual(1, settling.Coat.Value);
            Assert.AreEqual(1, CountBursts(RipeBurstKind.Foam, Centre));
        }

        [Test]
        public void AClusterPoppedTheTickItsFuseRanOut_RipplesAndSpraysNoFoam()
        {
            var neighbors = NeighborSlots(Centre);
            var cluster = Place(Centre, MergeMax);
            var durable = PlaceActor(new BalloonModel(new BalloonModelConfig(hitsToPop: 2)), neighbors[0]);
            _service.Tick();

            _now = Fuse;
            HitDirect(cluster);
            _service.Tick();
            AfterBurst();

            Assert.AreEqual(1, CountBursts(RipeBurstKind.Ripple, Centre));
            Assert.AreEqual(0, CountBursts(RipeBurstKind.Foam, Centre));
            Assert.AreEqual(0, durable.Coat.Value);
            Assert.AreEqual(1, durable.HitsRemaining.Value, "the ripple hit it; the foam never came");
        }

        // Three ripe clusters: the second next to the first, the third next to the second only.
        private (BubbleClusterModel, BubbleClusterModel, BubbleClusterModel) PlaceChainOfThree()
        {
            var firstNeighbors = NeighborSlots(Centre);
            var secondSlot = firstNeighbors[0];
            var thirdSlot = new Vector2Int(-1, -1);
            foreach (var slot in NeighborSlots(secondSlot))
            {
                if (slot != Centre && Array.IndexOf(firstNeighbors, slot) < 0 && InBounds(slot))
                {
                    thirdSlot = slot;
                    break;
                }
            }

            Assert.IsTrue(InBounds(thirdSlot));
            var first = Place(Centre, MergeMax);
            var second = Place(secondSlot, MergeMax);
            var third = Place(thirdSlot, MergeMax);
            _service.Tick();
            Assert.AreEqual(3, _service.Tracked);
            return (first, second, third);
        }

        // The level clock moves on by the burst delay, and the service looks.
        private void AfterBurst()
        {
            _now += Burst;
            _service.Tick();
        }

        private static int CountGifts(RipeBubbleBurstMessage message, FoamGift gift)
        {
            var count = 0;
            for (var bubble = 0; bubble < message.Bubbles; bubble++)
            {
                if (message.GiftOf(bubble, out _) == gift)
                {
                    count++;
                }
            }

            return count;
        }

        private void HitDirect(BubbleClusterModel cluster)
        {
            var context = new DamageContext(1, DamageFlags.DirectHit, string.Empty, _shot);
            _dispatcher.Dispatch(ActorHitMessage.From(
                cluster, _grid.IndexToWorldPosition(cluster.SlotIndex.Value), Vector3.up, context));
        }

        private void HitWith(BubbleClusterModel cluster, DamageFlags flags)
        {
            var context = new DamageContext(1, flags, string.Empty, _shot);
            _dispatcher.Dispatch(ActorHitMessage.From(
                cluster, _grid.IndexToWorldPosition(cluster.SlotIndex.Value), Vector3.up, context));
        }

        private List<ActorHitMessage> Ripples()
        {
            var ripples = new List<ActorHitMessage>();
            foreach (var hit in _dispatcher.Dispatched)
            {
                if ((hit.Context.Flags & DamageFlags.Ripple) != 0)
                {
                    ripples.Add(hit);
                }
            }

            return ripples;
        }

        private int HitsOn(object actor)
        {
            var count = 0;
            foreach (var hit in _dispatcher.Dispatched)
            {
                if (ReferenceEquals(hit.Actor, actor))
                {
                    count++;
                }
            }

            return count;
        }

        private int PopsOf(object actor)
        {
            var count = 0;
            foreach (var hit in _dispatcher.Dispatched)
            {
                if (ReferenceEquals(hit.Actor, actor) && (hit.Outcome & HitOutcome.Pop) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountBursts(RipeBurstKind kind, Vector2Int slot)
        {
            var count = 0;
            foreach (var burst in _bursts)
            {
                if (burst.Kind == kind && burst.Slot == slot)
                {
                    count++;
                }
            }

            return count;
        }

        private float[] RoundedBeats()
        {
            var rounded = new float[_beats.Count];
            for (var i = 0; i < rounded.Length; i++)
            {
                rounded[i] = Mathf.Round(_beats[i] * 10f) / 10f;
            }

            return rounded;
        }

        private Vector2Int[] NeighborSlots(Vector2Int slot)
        {
            var buffer = new Vector2Int[6];
            HexCoordinates.HexNeighborIndices(slot.x, slot.y, buffer);
            return buffer;
        }

        private bool InBounds(Vector2Int slot)
        {
            return slot.x >= 0 && slot.x < _grid.Columns && slot.y >= 0 && slot.y < _grid.Rows;
        }

        private BubbleClusterModel PlaceRipe()
        {
            var cluster = Place(Slot, MergeMax);
            _service.Tick();
            Assert.IsTrue(cluster.Ripe.Value);
            return cluster;
        }

        private void AssertForgotten(BubbleClusterModel cluster)
        {
            Assert.AreEqual(0, _service.Tracked);
            Assert.IsFalse(cluster.Ripe.Value);
            Assert.AreEqual(0f, cluster.FuseLeft.Value);

            // Its old deadline passes with nothing tracking it.
            _now = Fuse * 2f;
            _service.Tick();
            Assert.IsEmpty(_bursts);
            Assert.AreEqual(MergeMax, cluster.HitsRemaining.Value);
        }

        private BubbleClusterModel Place(Vector2Int slot, int bubbles)
        {
            var model = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: 3, scoreValue: 1), _palette);
            model.SetBubbles(bubbles);
            _grid.Place(model, null, slot);
            return model;
        }

        private BalloonModel PlacePlain(Vector2Int slot)
        {
            return PlaceActor(new BalloonModel(), slot);
        }

        private T PlaceActor<T>(T actor, Vector2Int slot)
            where T : IWriteableSlotActor
        {
            _grid.Place(actor, null, slot);
            return actor;
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

        // What HitPipeline and BalloonController do that matters here: a popped balloon leaves its slot, then the
        // hit is published. Depth tells a hit dispatched inside another's dispatch.
        private sealed class GridDispatcher : IHitDispatcher
        {
            private readonly SlotGrid _grid;
            private readonly Action<ActorHitMessage> _publish;
            private int _depth;

            public List<ActorHitMessage> Dispatched { get; } = new();
            public int MaxDepth { get; private set; }

            // Throws on the next dispatch, after recording it, as a subscriber that fails would.
            public bool ThrowOnce { get; set; }

            // Runs before the hit is heard, where the game's score stage runs.
            public Action<ActorHitMessage> BeforePublish { get; set; }

            public GridDispatcher(SlotGrid grid, Action<ActorHitMessage> publish)
            {
                _grid = grid;
                _publish = publish;
            }

            public void Dispatch(ActorHitMessage msg)
            {
                _depth++;
                MaxDepth = Math.Max(MaxDepth, _depth);
                Dispatched.Add(msg);
                var slot = msg.Actor.SlotIndex;
                if ((msg.Outcome & HitOutcome.Pop) != 0 && ReferenceEquals(_grid.At(slot), msg.Actor))
                {
                    _grid.Remove(slot);
                }

                if (ThrowOnce)
                {
                    ThrowOnce = false;
                    _depth--;
                    throw new InvalidOperationException("a subscriber failed");
                }

                BeforePublish?.Invoke(msg);
                _publish(msg);
                _depth--;
            }
        }
    }
}

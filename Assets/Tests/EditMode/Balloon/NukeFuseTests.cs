using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Level;
using BalloonParty.Nudge;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
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
    ///     The armed nuke's fuse (PLAN-NukeBalloon phase 6): the hit that arms a nuke starts a countdown on scaled
    ///     time, a beep message on each whole second, a pull on the balloons around it that grows, and at zero the
    ///     detonation. A level change, game over, run reset or the nuke leaving its slot stops it with no blast.
    /// </summary>
    [TestFixture]
    public class NukeFuseTests
    {
        private static readonly Vector2Int NukeSlot = new(0, 0);
        private static readonly Vector2Int NearSlot = new(1, 0);
        private static readonly Vector2Int OtherSlot = new(2, 2);
        private static readonly Vector2Int FarSlot = new(5, 9);

        // Exact in binary, so the fuse's three seconds are a whole number of steps with no float drift.
        private const float Step = 0.25f;

        private SlotGrid _grid;
        private IPublisher<NudgeMessage> _nudgePublisher;
        private IPublisher<NukeArmedMessage> _armedPublisher;
        private IPublisher<NukeCountdownMessage> _countdownPublisher;
        private INukeDetonator _detonator;
        private INukeBlastView _view;
        private ReactiveProperty<int> _level;
        private IMessageHandler<ActorHitMessage> _hitHandler;
        private IMessageHandler<GameOverMessage> _gameOverHandler;
        private NukeFuseSettings _settings;
        private NukeFuse _fuse;
        private ProjectileModel _shot;
        private readonly List<int> _counts = new();
        private readonly List<NudgeMessage> _nudges = new();
        private readonly List<float> _pullDistances = new();

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gridConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            // The count's own tests run it from the arming hit; the heart's lead-in has its tests below.
            _settings = new NukeFuseSettings();
            SetHeartSeconds(0f);
            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.NukeFuse.Returns(_settings);

            _counts.Clear();
            _nudges.Clear();
            _pullDistances.Clear();
            _nudgePublisher = Substitute.For<IPublisher<NudgeMessage>>();
            // The fuse reuses one override array between pulls, so a pull's distance is read as it is published.
            _nudgePublisher.When(p => p.Publish(Arg.Any<NudgeMessage>())).Do(call =>
            {
                var message = call.Arg<NudgeMessage>();
                _nudges.Add(message);
                _pullDistances.Add(message.Overrides[0].Distance);
            });
            _armedPublisher = Substitute.For<IPublisher<NukeArmedMessage>>();
            _countdownPublisher = Substitute.For<IPublisher<NukeCountdownMessage>>();
            _countdownPublisher.When(p => p.Publish(Arg.Any<NukeCountdownMessage>()))
                .Do(call => _counts.Add(call.Arg<NukeCountdownMessage>().SecondsLeft));
            _detonator = Substitute.For<INukeDetonator>();
            _view = Substitute.For<INukeBlastView>();
            _level = new ReactiveProperty<int>(1);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);

            var hits = Substitute.For<ISubscriber<ActorHitMessage>>();
            hits
                .Subscribe(Arg.Any<IMessageHandler<ActorHitMessage>>(), Arg.Any<MessageHandlerFilter<ActorHitMessage>[]>())
                .Returns(call =>
                {
                    _hitHandler = call.Arg<IMessageHandler<ActorHitMessage>>();
                    return Substitute.For<IDisposable>();
                });
            var gameOver = Substitute.For<ISubscriber<GameOverMessage>>();
            gameOver
                .Subscribe(Arg.Any<IMessageHandler<GameOverMessage>>(), Arg.Any<MessageHandlerFilter<GameOverMessage>[]>())
                .Returns(call =>
                {
                    _gameOverHandler = call.Arg<IMessageHandler<GameOverMessage>>();
                    return Substitute.For<IDisposable>();
                });

            _fuse = new NukeFuse(
                hits, gameOver, levelProgress, _grid, balloons, _nudgePublisher, _armedPublisher,
                _countdownPublisher, _detonator, _view);
            _fuse.Start();

            _shot = new ProjectileModel();
        }

        [TearDown]
        public void TearDown()
        {
            _fuse.Dispose();
        }

        [Test]
        public void ArmingHit_StartsTheFuse_AnnouncesItAndImplodesTheField()
        {
            var nuke = PlaceNuke(NukeSlot);
            var center = _grid.IndexToWorldPosition(NukeSlot);

            Hit(nuke);

            Assert.IsTrue(_fuse.IsRunning);
            _armedPublisher.Received(1).Publish(Arg.Is<NukeArmedMessage>(m => m.Position == center));
            _view.Received(1).Implode(center, _settings.Seconds);
            CollectionAssert.AreEqual(new[] { 3 }, _counts);
            Assert.AreEqual(3, nuke.FuseSecondsLeft.Value);
        }

        [Test]
        public void WhileTheHeartFlashes_TheCounterShowsNoSeconds_AndNothingBeeps()
        {
            SetHeartSeconds(1f);
            var nuke = PlaceNuke(NukeSlot);

            Hit(nuke);
            Advance(3);

            Assert.IsTrue(_fuse.IsRunning);
            Assert.AreEqual(0, nuke.FuseSecondsLeft.Value);
            CollectionAssert.IsEmpty(_counts);
        }

        [Test]
        public void AfterTheHeart_TheCountRunsItsWholeLength_ThenTheNukeDetonates()
        {
            SetHeartSeconds(1f);
            var nuke = PlaceNuke(NukeSlot);
            Hit(nuke);

            Advance(4);
            Assert.AreEqual(3, nuke.FuseSecondsLeft.Value);
            Advance(11);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, _counts);
            _detonator.DidNotReceiveWithAnyArgs().Detonate(default, default);
            Advance(1);

            _detonator.Received(1).Detonate(nuke, Arg.Any<Vector3>());
        }

        [Test]
        public void TheFieldImplodes_ForTheHeartAndTheCountTogether()
        {
            SetHeartSeconds(1f);
            var nuke = PlaceNuke(NukeSlot);

            Hit(nuke);

            _view.Received(1).Implode(Arg.Any<Vector3>(), 1f + _settings.Seconds);
        }

        [Test]
        public void TheShippedHeart_FlashesForASecond()
        {
            Assert.AreEqual(1f, new NukeFuseSettings().HeartSeconds);
        }

        [Test]
        public void ADentThatDoesNotArm_StartsNoFuse()
        {
            var nuke = new NukeBalloonModel(new BalloonModelConfig(BalloonType.Nuke, hitsToPop: 3));
            _grid.Place(nuke, null, NukeSlot);
            nuke.EvaluateHit(new DamageContext(1, DamageFlags.DirectHit));

            Hit(nuke);

            Assert.IsFalse(_fuse.IsRunning);
            _armedPublisher.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void AnArmedHitOnAnotherKindOfBalloon_StartsNoFuse()
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            _grid.Place(balloon, null, NukeSlot);

            _hitHandler.Handle(HitOf(balloon));

            Assert.IsFalse(_fuse.IsRunning);
        }

        [Test]
        public void TheSameNukeHitAgainWhileItsFuseRuns_DoesNotRestartTheFuse()
        {
            var nuke = PlaceNuke(NukeSlot);
            Hit(nuke);
            Advance(8);

            Hit(nuke);
            Advance(4);

            _armedPublisher.Received(1).Publish(Arg.Any<NukeArmedMessage>());
            _view.Received(1).Implode(Arg.Any<Vector3>(), Arg.Any<float>());
            _detonator.Received(1).Detonate(nuke, Arg.Any<Vector3>());
        }

        [Test]
        public void TheCounter_ShowsEachWholeSecondOnce_ThreeTwoOne()
        {
            var nuke = PlaceNuke(NukeSlot);
            Hit(nuke);

            Advance(6);
            Assert.AreEqual(2, nuke.FuseSecondsLeft.Value);
            Advance(4);
            Assert.AreEqual(1, nuke.FuseSecondsLeft.Value);
            Advance(1);

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, _counts);
            _detonator.DidNotReceiveWithAnyArgs().Detonate(default, default);
        }

        [Test]
        public void WhenTheFuseRunsOut_TheNukeDetonatesOnce_AtItsPosition_WithItsCounterAtZero()
        {
            var nuke = PlaceNuke(NukeSlot);
            var center = _grid.IndexToWorldPosition(NukeSlot);
            Hit(nuke);

            Advance(11);
            _detonator.DidNotReceiveWithAnyArgs().Detonate(default, default);
            Advance(1);
            Advance(20);

            _detonator.Received(1).Detonate(nuke, center);
            Assert.AreEqual(0, nuke.FuseSecondsLeft.Value);
            Assert.IsFalse(_fuse.IsRunning);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, _counts, "no beep for the zero it detonates on");
        }

        [Test]
        public void AFrameLongerThanTheFuse_DetonatesAtOnce_WithoutTheCountdownBetween()
        {
            var nuke = PlaceNuke(NukeSlot);
            Hit(nuke);

            _fuse.Advance(30f);

            _detonator.Received(1).Detonate(nuke, Arg.Any<Vector3>());
            CollectionAssert.AreEqual(new[] { 3 }, _counts);
        }

        [Test]
        public void ARunningFuse_PullsTheNearBalloonsInAtOnce_NotTheFarOnesNorTheNuke()
        {
            var nuke = PlaceNuke(NukeSlot);
            var near = PlaceBalloon(NearSlot);
            var far = PlaceBalloon(FarSlot);
            var center = _grid.IndexToWorldPosition(NukeSlot);

            Hit(nuke);

            Assert.AreEqual(1, _nudges.Count);
            Assert.AreSame(near, _nudges[0].Actor);
            Assert.AreEqual(center, _nudges[0].Origin);
            Assert.AreEqual(NudgeType.Wave, _nudges[0].Source);
            Assert.IsFalse(_nudges.Exists(m => ReferenceEquals(m.Actor, far)));
            Assert.IsFalse(_nudges.Exists(m => ReferenceEquals(m.Actor, nuke)));
        }

        [Test]
        public void ThePull_DrawsInwards_WithANegativeDistance()
        {
            var nuke = PlaceNuke(NukeSlot);
            PlaceBalloon(NearSlot);

            Hit(nuke);

            Assert.AreEqual(-_settings.PullDistanceStart, _pullDistances[0], 1e-5f);
        }

        [Test]
        public void ThePull_RepeatsEveryInterval_AndGrowsAsTheCountRunsDown()
        {
            var nuke = PlaceNuke(NukeSlot);
            PlaceBalloon(NearSlot);
            Hit(nuke);

            Advance(1);
            Assert.AreEqual(1, _pullDistances.Count, "not yet a whole interval since the arming pull");

            Advance(1);
            Assert.AreEqual(2, _pullDistances.Count);

            Advance(6);
            Assert.AreEqual(5, _pullDistances.Count);
            Assert.Less(_pullDistances[2], _pullDistances[1], "a later pull draws further in");
            Assert.Less(_pullDistances[4], _pullDistances[2]);
            Assert.GreaterOrEqual(_pullDistances[4], -_settings.PullDistanceEnd - 1e-5f);
        }

        [Test]
        public void ALevelChange_StopsTheFuse_WithNoBlast()
        {
            Hit(PlaceNuke(NukeSlot));
            Advance(4);

            _level.Value = 2;
            Advance(20);

            Assert.IsFalse(_fuse.IsRunning);
            _detonator.DidNotReceiveWithAnyArgs().Detonate(default, default);
        }

        [Test]
        public void GameOver_StopsTheFuse_WithNoBlast()
        {
            Hit(PlaceNuke(NukeSlot));
            Advance(4);

            _gameOverHandler.Handle(new GameOverMessage(3, 100));
            Advance(20);

            Assert.IsFalse(_fuse.IsRunning);
            _detonator.DidNotReceiveWithAnyArgs().Detonate(default, default);
        }

        [Test]
        public void ARunReset_StopsTheFuse_WithNoBlast_AndTheSameNukeCanArmAgain()
        {
            var nuke = PlaceNuke(NukeSlot);
            Hit(nuke);
            Advance(4);

            _fuse.ResetRun(1);
            Advance(20);

            Assert.IsFalse(_fuse.IsRunning);
            _detonator.DidNotReceiveWithAnyArgs().Detonate(default, default);

            Hit(nuke);

            Assert.IsTrue(_fuse.IsRunning);
        }

        [Test]
        public void TheNukeLeavingItsSlot_StopsTheFuse_WithNoBlast()
        {
            var nuke = PlaceNuke(NukeSlot);
            Hit(nuke);
            Advance(4);

            _grid.Remove(NukeSlot);
            Advance(20);

            Assert.IsFalse(_fuse.IsRunning);
            _detonator.DidNotReceiveWithAnyArgs().Detonate(default, default);
        }

        [Test]
        public void ASecondNukeArmedWhileOneRuns_SetsOffTheFirstAtOnce_ThenRunsItsOwnFuse()
        {
            var first = PlaceNuke(NukeSlot);
            var second = PlaceNuke(OtherSlot);
            Hit(first);
            Advance(4);

            Hit(second);

            _detonator.Received(1).Detonate(first, _grid.IndexToWorldPosition(NukeSlot));
            Assert.IsTrue(_fuse.IsRunning);
            _armedPublisher.Received(2).Publish(Arg.Any<NukeArmedMessage>());

            Advance(12);

            _detonator.Received(1).Detonate(second, _grid.IndexToWorldPosition(OtherSlot));
        }

        private void SetHeartSeconds(float seconds)
        {
            typeof(NukeFuseSettings)
                .GetField("_heartSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_settings, seconds);
        }

        private void Advance(int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                _fuse.Advance(Step);
            }
        }

        private void Hit(NukeBalloonModel nuke)
        {
            _hitHandler.Handle(HitOf(nuke));
        }

        private ActorHitMessage HitOf(IBalloonModel balloon)
        {
            var context = new DamageContext(1, DamageFlags.DirectHit, "Red", _shot);
            return new ActorHitMessage(
                balloon, _grid.IndexToWorldPosition(balloon.SlotIndex.Value), Vector3.up, HitOutcome.Deflect, context);
        }

        // Armed the way the game does it: by the hit that arms it, here a laser beam, rather than by a setter.
        private NukeBalloonModel PlaceNuke(Vector2Int slot)
        {
            var nuke = new NukeBalloonModel(new BalloonModelConfig(BalloonType.Nuke, hitsToPop: 3));
            nuke.EvaluateHit(new DamageContext(1, DamageFlags.Beam));
            _grid.Place(nuke, null, slot);
            return nuke;
        }

        private BalloonModel PlaceBalloon(Vector2Int slot)
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            model.Color.Value = "Red";
            _grid.Place(model, null, slot);
            return model;
        }
    }
}

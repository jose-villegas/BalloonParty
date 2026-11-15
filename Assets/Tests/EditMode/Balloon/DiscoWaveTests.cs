using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Nudge;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
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
    ///     The disco ball's wave (PLAN-DiscoBalloon phase 3): a disc from the pop that nudges every balloon it reaches
    ///     once, paints the paintable ones rainbow and leaves the paint-immune ones their colour, books what it
    ///     paints to the breaking shot, and stops on a run reset or a level change.
    /// </summary>
    [TestFixture]
    public class DiscoWaveTests
    {
        private static readonly Vector2Int DiscoSlot = new(0, 0);
        private static readonly Vector2Int NearSlot = new(1, 0);
        private static readonly Vector2Int FarSlot = new(5, 9);

        // At a tenth of the wave's 0.9 s the disc has reached about two slots out, short of the far corner.
        private const float EarlyStep = 0.1f;
        private const float WholeWave = 10f;

        private SlotGrid _grid;
        private IGamePalette _palette;
        private IPublisher<NudgeMessage> _nudgePublisher;
        private IPublisher<DiscoWaveAdvancedMessage> _advancedPublisher;
        private IPublisher<DiscoWaveReachedMessage> _reachedPublisher;
        private ProjectileRegistry _registry;
        private ShotPaintLedger _ledger;
        private RainbowLines _rainbowLines;
        private IDiscoWaveView _view;
        private ReactiveProperty<int> _level;
        private DiscoWave _wave;
        private ProjectileModel _shot;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gridConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _palette = Substitute.For<IGamePalette>();
            _palette.IsRainbow(GamePalette.RainbowColorId).Returns(true);
            _palette.GetColor("Red").Returns(Color.red);
            _palette.GetColor(GamePalette.RainbowColorId).Returns(Color.white);

            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.DiscoWave.Returns(new DiscoWaveSettings());

            _nudgePublisher = Substitute.For<IPublisher<NudgeMessage>>();
            _advancedPublisher = Substitute.For<IPublisher<DiscoWaveAdvancedMessage>>();
            _reachedPublisher = Substitute.For<IPublisher<DiscoWaveReachedMessage>>();
            _registry = new ProjectileRegistry();
            _ledger = new ShotPaintLedger(_registry);
            _view = Substitute.For<IDiscoWaveView>();
            _level = new ReactiveProperty<int>(1);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);
            _rainbowLines = new RainbowLines(_palette, _ledger, _registry, levelProgress);

            _wave = new DiscoWave(
                QuietSubscriber<ActorHitMessage>(), QuietSubscriber<GameOverMessage>(), levelProgress, _grid, _palette,
                balloons, _nudgePublisher, _advancedPublisher, _reachedPublisher, _ledger, _rainbowLines,
                new DisturbanceFieldService(
                    Substitute.For<IDisturbanceFieldSettings>(), Substitute.For<IGameDisplayConfiguration>(),
                    new ImpactEventBus()),
                _view);
            _wave.Start();

            _shot = new ProjectileModel();
            _registry.Load(_shot, null);
            _registry.Launch(_shot);
        }

        [TearDown]
        public void TearDown()
        {
            _wave.Dispose();
            _ledger.Dispose();
            _registry.Dispose();
        }

        [Test]
        public void TheWave_PaintsEveryPaintableBalloon_AndNoImmuneOne()
        {
            var near = PlaceBalloon(NearSlot, "Red");
            var far = PlaceBalloon(FarSlot, "Blue");
            var tough = new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: 3));
            var toughView = PlaceActor(tough, new Vector2Int(3, 3));

            PopDisco();
            _wave.Advance(WholeWave);

            Assert.AreEqual(GamePalette.RainbowColorId, near.Color.Value);
            Assert.AreEqual(GamePalette.RainbowColorId, far.Color.Value);
            // Immune: the drip plays and slides off, and nothing bursts.
            toughView.Received(1).PlayPaintDrip(Arg.Any<Color>(), true);
            toughView.DidNotReceive().PlayRecolourBurst(Arg.Any<Color>(), Arg.Any<float>());
            Assert.IsFalse(_wave.IsRunning);
            _view.Received(1).Hide();
        }

        [Test]
        public void APaintedBalloon_BurstsInItsOldColour_AndTakesTheDrip()
        {
            PlaceBalloon(NearSlot, "Red", out var view);

            PopDisco();
            _wave.Advance(WholeWave);

            view.Received(1).PlayRecolourBurst(Color.red, new DiscoWaveSettings().BurstScale);
            view.Received(1).PlayPaintDrip(Color.white, true);
        }

        [Test]
        public void AnAlreadyRainbowBalloon_IsNudged_ButNotPaintedAgain()
        {
            var rainbow = PlaceBalloon(NearSlot, GamePalette.RainbowColorId, out var view);

            PopDisco();
            _wave.Advance(WholeWave);

            view.DidNotReceive().PlayPaintDrip(Arg.Any<Color>(), Arg.Any<bool>());
            view.DidNotReceive().PlayRecolourBurst(Arg.Any<Color>(), Arg.Any<float>());
            _nudgePublisher.Received(1).Publish(Arg.Is<NudgeMessage>(m => ReferenceEquals(m.Actor, rainbow)));
        }

        [Test]
        public void TheDisc_ReachesNearBalloonsBeforeFarOnes()
        {
            var near = PlaceBalloon(NearSlot, "Red");
            var far = PlaceBalloon(FarSlot, "Red");

            PopDisco();
            _wave.Advance(EarlyStep);

            Assert.AreEqual(GamePalette.RainbowColorId, near.Color.Value);
            Assert.AreEqual("Red", far.Color.Value);
            Assert.IsTrue(_wave.IsRunning);
        }

        [Test]
        public void EachBalloon_IsReachedOnce_HoweverManyFramesTheDiscCoversIt()
        {
            var near = PlaceBalloon(NearSlot, "Red", out var view);

            PopDisco();
            _wave.Advance(EarlyStep);
            _wave.Advance(EarlyStep);
            _wave.Advance(WholeWave);

            view.Received(1).PlayPaintDrip(Arg.Any<Color>(), true);
            _nudgePublisher.Received(1).Publish(Arg.Is<NudgeMessage>(m =>
                ReferenceEquals(m.Actor, near) && m.Source == NudgeType.Wave));
        }

        [Test]
        public void ABalloonArrivingWhereTheDiscHasPassed_IsStillPainted()
        {
            PopDisco();
            _wave.Advance(EarlyStep);
            var late = PlaceBalloon(NearSlot, "Red");

            _wave.Advance(WholeWave);

            Assert.AreEqual(GamePalette.RainbowColorId, late.Color.Value);
        }

        [Test]
        public void TheDiscoBallItself_IsNotTouched()
        {
            var disco = PopDisco();

            _wave.Advance(WholeWave);

            _nudgePublisher.DidNotReceive().Publish(Arg.Is<NudgeMessage>(m => ReferenceEquals(m.Actor, disco)));
        }

        [Test]
        public void WhatTheWavePaints_IsBookedToTheBreakingShot_Only()
        {
            var near = PlaceBalloon(NearSlot, "Red");
            var other = new ProjectileModel();
            _registry.Load(other, null);

            PopDisco();
            _wave.Advance(WholeWave);

            Assert.IsTrue(_ledger.Painted(_shot, near));
            Assert.IsFalse(_ledger.Painted(other, near));
        }

        [Test]
        public void ARunReset_StopsTheWaveWhereItIs()
        {
            var far = PlaceBalloon(FarSlot, "Red");
            PopDisco();
            _wave.Advance(EarlyStep);

            _wave.ResetRun(1);
            _wave.Advance(WholeWave);

            Assert.AreEqual("Red", far.Color.Value);
            Assert.IsFalse(_wave.IsRunning);
            _view.Received(1).Hide();
        }

        [Test]
        public void ALevelChange_StopsTheWaveWhereItIs()
        {
            var far = PlaceBalloon(FarSlot, "Red");
            PopDisco();
            _wave.Advance(EarlyStep);

            _level.Value = 2;
            _wave.Advance(WholeWave);

            Assert.AreEqual("Red", far.Color.Value);
        }

        [Test]
        public void GameOver_StopsTheWaveWhereItIs()
        {
            IMessageHandler<GameOverMessage> gameOverHandler = null;
            var gameOver = Substitute.For<ISubscriber<GameOverMessage>>();
            gameOver
                .Subscribe(Arg.Any<IMessageHandler<GameOverMessage>>(), Arg.Any<MessageHandlerFilter<GameOverMessage>[]>())
                .Returns(call =>
                {
                    gameOverHandler = call.Arg<IMessageHandler<GameOverMessage>>();
                    return Substitute.For<IDisposable>();
                });
            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.DiscoWave.Returns(new DiscoWaveSettings());
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);
            using var wave = new DiscoWave(
                QuietSubscriber<ActorHitMessage>(), gameOver, levelProgress, _grid, _palette, balloons,
                _nudgePublisher, _advancedPublisher, _reachedPublisher, _ledger, _rainbowLines,
                new DisturbanceFieldService(
                    Substitute.For<IDisturbanceFieldSettings>(), Substitute.For<IGameDisplayConfiguration>(),
                    new ImpactEventBus()),
                _view);
            wave.Start();
            var far = PlaceBalloon(FarSlot, "Red");
            var disco = new DiscoBalloonModel(new BalloonModelConfig(BalloonType.Disco, hitsToPop: 1));
            _grid.Place(disco, null, DiscoSlot);
            wave.OnHit(PopOf(disco));
            wave.Advance(EarlyStep);

            gameOverHandler.Handle(new GameOverMessage(3, 100));
            wave.Advance(WholeWave);

            Assert.IsFalse(wave.IsRunning);
            Assert.AreEqual("Red", far.Color.Value);
        }

        [Test]
        public void TheSamePopPublishedTwice_StartsOneWave()
        {
            var disco = PopDisco();
            _wave.Advance(EarlyStep);

            _wave.OnHit(PopOf(disco));

            _view.Received(1).Show(Arg.Any<Vector3>());
        }

        [Test]
        public void EachBalloonTheWavePaints_PublishesItsReach_FartherOnesReachingFarther()
        {
            PlaceBalloon(NearSlot, "Red");
            PlaceBalloon(FarSlot, "Blue");
            var tough = new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: 3));
            PlaceActor(tough, new Vector2Int(3, 3));
            PlaceBalloon(new Vector2Int(2, 0), GamePalette.RainbowColorId);
            var nearReach = -1f;
            var farReach = -1f;
            _reachedPublisher.When(p => p.Publish(Arg.Any<DiscoWaveReachedMessage>())).Do(call =>
            {
                var message = call.Arg<DiscoWaveReachedMessage>();
                if (message.Position == _grid.IndexToWorldPosition(NearSlot))
                {
                    nearReach = message.Reach;
                }
                else if (message.Position == _grid.IndexToWorldPosition(FarSlot))
                {
                    farReach = message.Reach;
                }
            });

            PopDisco();
            _wave.Advance(WholeWave);

            // The red, the blue and the paint-immune tough tick; the balloon already rainbow takes nothing, and neither
            // does the disco ball itself.
            _reachedPublisher.Received(3).Publish(Arg.Any<DiscoWaveReachedMessage>());
            Assert.That(nearReach, Is.GreaterThan(0f));
            Assert.That(farReach, Is.GreaterThan(nearReach).And.LessThanOrEqualTo(1f));
        }

        [Test]
        public void TheWave_PublishesItsTime_FromNoneAtTheStartToAllOfItAtTheEnd()
        {
            PopDisco();
            _advancedPublisher.Received(1).Publish(new DiscoWaveAdvancedMessage(0f));

            _wave.Advance(WholeWave);

            _advancedPublisher.Received(1).Publish(new DiscoWaveAdvancedMessage(1f));
        }

        [Test]
        public void ThePop_OwesTheNextClockLinesRainbow()
        {
            PopDisco();

            Assert.AreEqual(3, _rainbowLines.Owed, "the shipped default");
        }

        [Test]
        public void AGoldenBalloonsPop_StartsNoWave()
        {
            var golden = new GoldenBalloonModel(new BalloonModelConfig(BalloonType.Golden, hitsToPop: 1));
            _grid.Place(golden, null, DiscoSlot);

            _wave.OnHit(PopOf(golden));

            Assert.IsFalse(_wave.IsRunning);
        }

        private static ISubscriber<T> QuietSubscriber<T>()
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(Arg.Any<IMessageHandler<T>>(), Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }

        private DiscoBalloonModel PopDisco()
        {
            var disco = new DiscoBalloonModel(new BalloonModelConfig(BalloonType.Disco, hitsToPop: 1));
            _grid.Place(disco, null, DiscoSlot);
            _wave.OnHit(PopOf(disco));
            return disco;
        }

        private ActorHitMessage PopOf(IBalloonModel balloon)
        {
            var context = new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit, "Red", _shot);
            return new ActorHitMessage(
                balloon, _grid.IndexToWorldPosition(balloon.SlotIndex.Value), Vector3.up, HitOutcome.Pop, context);
        }

        private BalloonModel PlaceBalloon(Vector2Int slot, string color)
        {
            return PlaceBalloon(slot, color, out _);
        }

        private BalloonModel PlaceBalloon(Vector2Int slot, string color, out IPaintReactive view)
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            model.Color.Value = color;
            view = PlaceActor(model, slot);
            return model;
        }

        private IPaintReactive PlaceActor(IWriteableSlotActor actor, Vector2Int slot)
        {
            var view = Substitute.For<IPaintReactive>();
            _grid.Place(actor, view, slot);
            return view;
        }
    }
}

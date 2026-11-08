using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class DeflectChainControllerTests
    {
        private IMessageHandler<ActorHitMessage> _hit;
        private IMessageHandler<WallHitMessage> _wallHit;
        private IMessageHandler<ProjectileDestroyedMessage> _destroyed;
        private List<DeflectChainMessage> _chains;
        private List<ShieldGainedMessage> _shieldsGained;
        private IRunConfig _runConfig;
        private DeflectChainController _controller;

        [SetUp]
        public void SetUp()
        {
            _chains = new List<DeflectChainMessage>();
            _shieldsGained = new List<ShieldGainedMessage>();
            var chainPublisher = Substitute.For<IPublisher<DeflectChainMessage>>();
            chainPublisher.When(p => p.Publish(Arg.Any<DeflectChainMessage>()))
                .Do(call => _chains.Add(call.Arg<DeflectChainMessage>()));
            var shieldPublisher = Substitute.For<IPublisher<ShieldGainedMessage>>();
            shieldPublisher.When(p => p.Publish(Arg.Any<ShieldGainedMessage>()))
                .Do(call => _shieldsGained.Add(call.Arg<ShieldGainedMessage>()));
            _runConfig = Substitute.For<IRunConfig>();
            _runConfig.DeflectChainShieldEvery.Returns(3);

            _controller = new DeflectChainController(
                CaptureSubscriber<ActorHitMessage>(h => _hit = h),
                CaptureSubscriber<WallHitMessage>(h => _wallHit = h),
                CaptureSubscriber<ProjectileDestroyedMessage>(h => _destroyed = h),
                chainPublisher,
                shieldPublisher,
                _runConfig);
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        [Test]
        public void OneDeflect_IsNotAChainYet()
        {
            var ball = NewBall();

            Deflect(ball);

            CollectionAssert.IsEmpty(_chains);
        }

        [Test]
        public void TwoDeflectsInARow_AnnounceAChainOfTwo()
        {
            var ball = NewBall();

            Deflect(ball);
            Deflect(ball);

            Assert.AreEqual(1, _chains.Count);
            Assert.AreEqual(2, _chains[0].Count);
            Assert.AreSame(ball, _chains[0].Projectile);
        }

        [Test]
        public void TheThirdDeflectInARow_PaysAShield()
        {
            var ball = NewBall();

            Deflect(ball);
            Deflect(ball);
            Deflect(ball, new Vector2Int(4, 2));

            Assert.AreEqual(2, ball.ShieldsRemaining.Value);
            Assert.AreEqual(1, _shieldsGained.Count);
            Assert.AreEqual(new Vector2Int(4, 2), _shieldsGained[0].SlotIndex);
        }

        [Test]
        public void AWallBetweenDeflects_BreaksTheChain()
        {
            var ball = NewBall();
            Deflect(ball);
            Deflect(ball);

            _wallHit.Handle(new WallHitMessage(ball, Vector3.zero, 0));
            Deflect(ball);
            Deflect(ball);

            Assert.AreEqual(2, _chains[^1].Count);
            CollectionAssert.IsEmpty(_shieldsGained);
        }

        [Test]
        public void APopBetweenDeflects_KeepsTheChain()
        {
            var ball = NewBall();
            Deflect(ball);

            Hit(ball, HitOutcome.Pop, DamageFlags.DirectHit, Vector2Int.zero);
            Deflect(ball);

            Assert.AreEqual(2, _chains[^1].Count);
        }

        [Test]
        public void AnItemHittingAToughBalloon_DoesNotCount()
        {
            var ball = NewBall();

            Hit(ball, HitOutcome.Deflect, DamageFlags.Normal, Vector2Int.zero);
            Hit(ball, HitOutcome.Deflect, DamageFlags.Normal, Vector2Int.zero);

            CollectionAssert.IsEmpty(_chains);
        }

        [Test]
        public void TwoBalls_ChainSeparately()
        {
            var first = NewBall();
            var second = NewBall();

            Deflect(first);
            Deflect(second);

            CollectionAssert.IsEmpty(_chains);
        }

        [Test]
        public void ABallThatDied_StartsOverIfItsModelFliesAgain()
        {
            var ball = NewBall();
            Deflect(ball);

            _destroyed.Handle(new ProjectileDestroyedMessage(ball));
            Deflect(ball);

            CollectionAssert.IsEmpty(_chains);
        }

        [Test]
        public void TheRewardSwitchedOff_StillAnnouncesButPaysNothing()
        {
            _runConfig.DeflectChainShieldEvery.Returns(0);
            var ball = NewBall();

            Deflect(ball);
            Deflect(ball);
            Deflect(ball);

            Assert.AreEqual(2, _chains.Count);
            CollectionAssert.IsEmpty(_shieldsGained);
            Assert.AreEqual(1, ball.ShieldsRemaining.Value);
        }

        private static IWriteableProjectileModel NewBall()
        {
            var ball = Substitute.For<IWriteableProjectileModel>();
            ball.ShieldsRemaining.Returns(new ReactiveProperty<int>(1));
            return ball;
        }

        private void Deflect(IProjectileModel ball, Vector2Int slot = default)
        {
            Hit(ball, HitOutcome.Deflect, DamageFlags.DirectHit, slot);
        }

        private void Hit(IProjectileModel ball, HitOutcome outcome, DamageFlags flags, Vector2Int slot)
        {
            var actor = Substitute.For<ISlotActor>();
            actor.SlotIndex.Returns(slot);
            _hit.Handle(new ActorHitMessage(actor, Vector3.zero, Vector3.up, outcome,
                new DamageContext(1, flags, "", ball)));
        }

        private static ISubscriber<T> CaptureSubscriber<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(
                    Arg.Do(capture),
                    Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}

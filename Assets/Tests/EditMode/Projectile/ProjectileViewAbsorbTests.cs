using System;
using System.Reflection;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class ProjectileViewAbsorbTests
    {
        private GameObject _go;
        private ProjectileView _view;
        private ProjectileModel _model;
        private IPublisher<ProjectileDestroyedMessage> _destroyedPublisher;
        private IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private IPublisher<ActorHitMessage> _hitPublisher;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject();
            _view = _go.AddComponent<ProjectileView>();

            _destroyedPublisher = Substitute.For<IPublisher<ProjectileDestroyedMessage>>();
            _balancePublisher = Substitute.For<IPublisher<BalanceBalloonsMessage>>();
            _hitPublisher = Substitute.For<IPublisher<ActorHitMessage>>();

            var deflectedSubscriber = Substitute.For<ISubscriber<BalloonDeflectedMessage>>();
            deflectedSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<BalloonDeflectedMessage>>(),
                    Arg.Any<MessageHandlerFilter<BalloonDeflectedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            Inject("_destroyedPublisher", _destroyedPublisher);
            Inject("_balancePublisher", _balancePublisher);
            Inject("_hitPublisher", _hitPublisher);
            Inject("_deflectedSubscriber", deflectedSubscriber);

            _model = new ProjectileModel { IsFree = true };
            _view.Bind(_model);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void ProjectileView_OnAbsorb_PublishesProjectileDestroyed()
        {
            _view.OnAbsorb(new StubActor(), Vector3.zero);

            _destroyedPublisher.Received(1).Publish(Arg.Any<ProjectileDestroyedMessage>());
        }

        [Test]
        public void ProjectileView_OnAbsorb_SetsModelNotFree()
        {
            _view.OnAbsorb(new StubActor(), Vector3.zero);

            Assert.IsFalse(_model.IsFree);
        }

        [Test]
        public void ProjectileView_OnAbsorb_PublishesActorHitMessageWithAbsorbOutcome()
        {
            var actor = new StubActor();

            _view.OnAbsorb(actor, Vector3.zero);

            _hitPublisher.Received(1).Publish(Arg.Is<ActorHitMessage>(m =>
                m.Actor == actor && m.Outcome == HitOutcome.Absorb));
        }

        private void Inject(string fieldName, object value)
        {
            typeof(ProjectileView)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_view, value);
        }

        private class StubActor : ISlotActor
        {
            public Vector2Int SlotIndex => default;
            public SlotActorKind Kind => SlotActorKind.Static;
        }
    }
}



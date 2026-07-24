using System;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class CombatSoundRouterTests
    {
        private ISoundPlayer _player;
        private IMessageHandler<ActorHitMessage> _hitHandler;
        private IMessageHandler<ProjectileFiredMessage> _firedHandler;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;
        private IMessageHandler<ProjectileCruiseStartedMessage> _cruiseStartedHandler;
        private IMessageHandler<ProjectileCruiseEndedMessage> _cruiseEndedHandler;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();

            var hitSubscriber = CaptureSubscriber<ActorHitMessage>(h => _hitHandler = h);
            var firedSubscriber = CaptureSubscriber<ProjectileFiredMessage>(h => _firedHandler = h);
            var loadedSubscriber = CaptureSubscriber<ProjectileLoadedMessage>(h => _loadedHandler = h);
            var cruiseStartedSubscriber = CaptureSubscriber<ProjectileCruiseStartedMessage>(h => _cruiseStartedHandler = h);
            var cruiseEndedSubscriber = CaptureSubscriber<ProjectileCruiseEndedMessage>(h => _cruiseEndedHandler = h);
            var doomedSubscriber = CaptureSubscriber<ProjectileDoomedStartedMessage>(_ => { });
            var pierceSubscriber = CaptureSubscriber<PierceDischargedMessage>(_ => { });
            var shieldGainedSubscriber = CaptureSubscriber<ShieldGainedMessage>(_ => { });
            var shieldLostSubscriber = CaptureSubscriber<ShieldLostMessage>(_ => { });

            var router = new CombatSoundRouter(
                _player, hitSubscriber, firedSubscriber, loadedSubscriber,
                cruiseStartedSubscriber, cruiseEndedSubscriber, doomedSubscriber,
                pierceSubscriber, shieldGainedSubscriber, shieldLostSubscriber);
            router.Start();
        }

        [Test]
        public void OnActorHit_PopOutcome_PlaysBalloonPopAtWorldPosition()
        {
            var position = new Vector3(1f, 2f, 0f);

            _hitHandler.Handle(new ActorHitMessage(null, position, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPop, position);
        }

        [Test]
        public void OnActorHit_DeflectOutcome_PlaysBalloonDeflect()
        {
            _hitHandler.Handle(new ActorHitMessage(null, Vector3.zero, Vector3.zero, HitOutcome.Deflect));

            _player.Received(1).Play(GameSoundId.BalloonDeflect, Vector3.zero);
            _player.DidNotReceive().Play(GameSoundId.BalloonPop, Arg.Any<Vector3?>());
        }

        [Test]
        public void OnActorHit_AbsorbOutcome_PlaysBalloonResist()
        {
            _hitHandler.Handle(new ActorHitMessage(null, Vector3.zero, Vector3.zero, HitOutcome.Absorb));

            _player.Received(1).Play(GameSoundId.BalloonResist, Vector3.zero);
        }

        [Test]
        public void OnActorHit_PassThroughOutcome_PlaysBalloonResist()
        {
            _hitHandler.Handle(new ActorHitMessage(null, Vector3.zero, Vector3.zero, HitOutcome.PassThrough));

            _player.Received(1).Play(GameSoundId.BalloonResist, Vector3.zero);
        }

        [Test]
        public void OnActorHit_PopCombinedWithDeflect_ResolvesToPop()
        {
            // Pop takes precedence over Deflect when both flags are set on the same outcome —
            // the branch order in OnActorHit IS the precedence contract.
            _hitHandler.Handle(new ActorHitMessage(
                null, Vector3.zero, Vector3.zero, HitOutcome.Pop | HitOutcome.Deflect));

            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero);
            _player.DidNotReceive().Play(GameSoundId.BalloonDeflect, Arg.Any<Vector3?>());
        }

        [Test]
        public void OnFired_ForwardsShotFiredAtWorldPosition()
        {
            var position = new Vector3(3f, 4f, 0f);

            _firedHandler.Handle(new ProjectileFiredMessage(position, Vector3.right));

            _player.Received(1).Play(GameSoundId.ShotFired, position);
        }

        [Test]
        public void OnLoaded_ForwardsShotReloadWithNullPosition()
        {
            _loadedHandler.Handle(new ProjectileLoadedMessage(null));

            _player.Received(1).Play(GameSoundId.ShotReload, null);
        }

        [Test]
        public void OnCruiseStarted_ThenEnded_StopsTheStoredHandle()
        {
            var handle = new SoundHandle(7, 1u);
            _player.Play(GameSoundId.CruiseLoopStart, Arg.Any<Vector3?>()).Returns(handle);

            _cruiseStartedHandler.Handle(new ProjectileCruiseStartedMessage(Vector3.zero, Vector3.right, 1));
            _cruiseEndedHandler.Handle(new ProjectileCruiseEndedMessage(Vector3.zero));

            _player.Received(1).Stop(handle);
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

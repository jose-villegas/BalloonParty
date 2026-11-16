using System;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    /// <summary>
    ///     The nuke's fuse and blast in sound (PLAN-NukeBalloon): a click and alarm when armed, a beep on each count that
    ///     climbs a step per count from the fuse's first one, the boom as it goes off, and the wind of the breather,
    ///     which blows over the whole board rather than from the nuke.
    /// </summary>
    [TestFixture]
    public class NukeSoundRouterTests
    {
        private static readonly Vector3 Position = new(1f, 2f, 0f);

        private ISoundPlayer _player;
        private IMessageHandler<NukeArmedMessage> _armed;
        private IMessageHandler<NukeCountdownMessage> _countdown;
        private IMessageHandler<NukeDetonatedMessage> _detonated;
        private IMessageHandler<NukeBreatherMessage> _breather;
        private NukeSoundRouter _router;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();
            _router = new NukeSoundRouter(_player,
                CaptureSubscriber<NukeArmedMessage>(h => _armed = h),
                CaptureSubscriber<NukeCountdownMessage>(h => _countdown = h),
                CaptureSubscriber<NukeDetonatedMessage>(h => _detonated = h),
                CaptureSubscriber<NukeBreatherMessage>(h => _breather = h));
            _router.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _router.Dispose();
        }

        [Test]
        public void Armed_PlaysTheArmingCue_AtTheNuke()
        {
            _armed.Handle(new NukeArmedMessage(Position));

            _player.Received(1).Play(GameSoundId.NukeArmed, Position);
        }

        [Test]
        public void TheCountdown_BeepsAtTheRoot_ThenAStepHigherEachCount()
        {
            _armed.Handle(new NukeArmedMessage(Position));
            _countdown.Handle(new NukeCountdownMessage(Position, 3));
            _countdown.Handle(new NukeCountdownMessage(Position, 2));
            _countdown.Handle(new NukeCountdownMessage(Position, 1));

            Received.InOrder(() =>
            {
                _player.Play(GameSoundId.NukeCountdownBeep, Position, null, 0, 1f);
                _player.Play(GameSoundId.NukeCountdownBeep, Position, null, NukeSoundRouter.BeepStepSemitones, 1f);
                _player.Play(GameSoundId.NukeCountdownBeep, Position, null, 2 * NukeSoundRouter.BeepStepSemitones, 1f);
            });
        }

        [Test]
        public void ANewFuse_StartsTheBeepsOverAtTheRoot()
        {
            _armed.Handle(new NukeArmedMessage(Position));
            _countdown.Handle(new NukeCountdownMessage(Position, 3));
            _countdown.Handle(new NukeCountdownMessage(Position, 2));

            _armed.Handle(new NukeArmedMessage(Position));
            _countdown.Handle(new NukeCountdownMessage(Position, 3));

            _player.Received(2).Play(GameSoundId.NukeCountdownBeep, Position, null, 0, 1f);
        }

        [Test]
        public void AFuseWhoseFirstCountIsShorter_BeepsItsFirstAtTheRoot()
        {
            _armed.Handle(new NukeArmedMessage(Position));
            _countdown.Handle(new NukeCountdownMessage(Position, 2));
            _countdown.Handle(new NukeCountdownMessage(Position, 1));

            Received.InOrder(() =>
            {
                _player.Play(GameSoundId.NukeCountdownBeep, Position, null, 0, 1f);
                _player.Play(GameSoundId.NukeCountdownBeep, Position, null, NukeSoundRouter.BeepStepSemitones, 1f);
            });
        }

        [Test]
        public void Detonated_PlaysTheBoom_AtTheNuke()
        {
            _detonated.Handle(new NukeDetonatedMessage(Position));

            _player.Received(1).Play(GameSoundId.BalloonPopNuke, Position);
        }

        [Test]
        public void Breather_PlaysTheWind_WithNoPosition()
        {
            _breather.Handle(new NukeBreatherMessage(Position));

            _player.Received(1).Play(GameSoundId.NukeBreatherWind, null);
        }

        private static ISubscriber<T> CaptureSubscriber<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(Arg.Any<IMessageHandler<T>>(), Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(call =>
                {
                    capture(call.Arg<IMessageHandler<T>>());
                    return Substitute.For<IDisposable>();
                });
            return subscriber;
        }
    }
}

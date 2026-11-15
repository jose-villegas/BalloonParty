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
    ///     The disco ball's wave in sound (PLAN-DiscoBalloon phase 5): the glissando climbs a degree of the key per tenth
    ///     of the wave's time and starts over with each wave, and each painted balloon ticks at the degree its distance
    ///     reaches.
    /// </summary>
    [TestFixture]
    public class DiscoSoundRouterTests
    {
        private ISoundPlayer _player;
        private IMessageHandler<DiscoWaveAdvancedMessage> _advanced;
        private IMessageHandler<DiscoWaveReachedMessage> _reached;
        private DiscoSoundRouter _router;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();
            _router = new DiscoSoundRouter(_player,
                CaptureSubscriber<DiscoWaveAdvancedMessage>(h => _advanced = h),
                CaptureSubscriber<DiscoWaveReachedMessage>(h => _reached = h));
            _router.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _router.Dispose();
        }

        [Test]
        public void TheGlissando_StartsOnTheRoot_AnOctaveDown_AndClimbsADegreePerTenth()
        {
            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));
            _advanced.Handle(new DiscoWaveAdvancedMessage(0.05f));
            _advanced.Handle(new DiscoWaveAdvancedMessage(0.1f));

            Received.InOrder(() =>
            {
                _player.Play(GameSoundId.DiscoWaveGlissando, null, 0, DiscoSoundRouter.GlissandoSemitoneOffset, 1f);
                _player.Play(GameSoundId.DiscoWaveGlissando, null, 1, DiscoSoundRouter.GlissandoSemitoneOffset, 1f);
            });
            _player.ReceivedWithAnyArgs(2).Play(default, default);
        }

        [Test]
        public void ASlowFrameCrossingSeveralNotes_PlaysOnlyTheLast()
        {
            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));
            _advanced.Handle(new DiscoWaveAdvancedMessage(1f));

            PlayGlissando(DiscoSoundRouter.ScaleDegrees);
            _player.ReceivedWithAnyArgs(2).Play(default, default);
        }

        [Test]
        public void ANewWave_StartsTheGlissandoOver()
        {
            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));
            _advanced.Handle(new DiscoWaveAdvancedMessage(0.5f));

            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));

            PlayGlissando(0, times: 2);
        }

        [Test]
        public void AFrameWithNoTime_ReplaysNothing()
        {
            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));
            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));
            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));

            PlayGlissando(0);
        }

        [Test]
        public void AWaveCutShort_ThenANewOne_StartsTheGlissandoOver()
        {
            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));
            _advanced.Handle(new DiscoWaveAdvancedMessage(0.35f));

            _advanced.Handle(new DiscoWaveAdvancedMessage(0f));
            _advanced.Handle(new DiscoWaveAdvancedMessage(0.1f));

            PlayGlissando(0, times: 2);
            PlayGlissando(1);
        }

        [TestCase(0f, 0)]
        [TestCase(0.5f, 5)]
        [TestCase(1f, DiscoSoundRouter.ScaleDegrees)]
        public void APaintedBalloon_TicksAtTheDegreeItsDistanceReaches(float reach, int degree)
        {
            var position = new Vector3(1f, 2f, 0f);

            _reached.Handle(new DiscoWaveReachedMessage(position, reach));

            _player.Received(1).Play(GameSoundId.DiscoWaveTick, position, degree, 0, 1f);
        }

        private void PlayGlissando(int degree, int times = 1)
        {
            _player.Received(times).Play(GameSoundId.DiscoWaveGlissando, null, degree,
                DiscoSoundRouter.GlissandoSemitoneOffset, 1f);
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

using System;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     The last-breath slow-mo is world-wide, so it must play only while the doomed ball flies alone.
    ///     Observed through the real <see cref="TimeScaleService" />, the only writer of Time.timeScale.
    /// </summary>
    [TestFixture]
    public class ProjectileDoomedTimeScaleControllerTests
    {
        private const float SlowScale = 0.25f;

        private ProjectileRegistry _registry;
        private TimeScaleService _timeScale;
        private ProjectileDoomedTimeScaleController _controller;
        private IProjectileFlightConfig _config;
        private IMessageHandler<ProjectileDoomedStartedMessage> _startedHandler;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            _registry = new ProjectileRegistry();
            _timeScale = new TimeScaleService();

            _config = Substitute.For<IProjectileFlightConfig>();
            _config.LastShieldApproachDuration.Returns(1f);
            _config.LastShieldTimeScaleCurve.Returns(AnimationCurve.Constant(0f, 1f, SlowScale));

            _controller = new ProjectileDoomedTimeScaleController(
                _config, _timeScale, _registry,
                Capture<ProjectileDoomedStartedMessage>(h => _startedHandler = h));
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            _registry.Dispose();
            Time.timeScale = 1f;
        }

        [Test]
        public void DoomedStarted_BallFliesAlone_ClaimsTheSlowMo()
        {
            var doomed = Fly();

            Doom(doomed);

            Assert.AreEqual(SlowScale, Time.timeScale, 0.001f);
        }

        [Test]
        public void DoomedStarted_AnotherBallInFlight_DoesNotSlowTheWorld()
        {
            var doomed = Fly();
            Fly();

            Doom(doomed);

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [Test]
        public void Tick_OtherBallsDieWhileDoomed_ClaimsTheSlowMo()
        {
            var doomed = Fly();
            var other = Fly();
            Doom(doomed);

            _registry.Land(other);
            _controller.Advance(0f);

            Assert.AreEqual(SlowScale, Time.timeScale, 0.001f);
        }

        [Test]
        public void Tick_SecondBallLaunchesDuringSlowMo_ReleasesTheClaim()
        {
            var doomed = Fly();
            Doom(doomed);
            Assert.AreEqual(SlowScale, Time.timeScale, 0.001f);

            Fly();
            _controller.Advance(0f);

            Assert.AreEqual(1f, Time.timeScale, 0.001f, "a new ball must not be dragged into the doomed slow-mo");
        }

        [Test]
        public void Tick_ResumesWhereTheLastBreathNowIs_NotFromTheStart()
        {
            _config.LastShieldTimeScaleCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var doomed = Fly();
            var other = Fly();
            Doom(doomed);

            // The wall time that passes while the other ball flies: the last breath keeps running.
            _controller.Advance(0.6f);
            _registry.Land(other);
            _controller.Advance(0f);

            // Restarting the curve would claim ~0 (frozen); resuming where the ball now is claims ~0.6.
            Assert.AreEqual(0.6f, Time.timeScale, 0.01f);
        }

        [Test]
        public void Tick_TheLaterDoomedBallDiesFirst_TheEarlierOneLeftAloneGetsTheSlowMo()
        {
            var first = Fly();
            var second = Fly();
            Doom(first);
            Doom(second);

            second.IsLastShieldApproach.Value = false;
            _registry.Land(second);
            _controller.Advance(0f);

            // The spawner and balancer already hold for the lone doomed ball; the slow-mo must agree,
            // or the board freezes at full speed.
            Assert.AreEqual(SlowScale, Time.timeScale, 0.001f);
        }

        [Test]
        public void Tick_DoomEnds_ReleasesTheClaim()
        {
            var doomed = Fly();
            Doom(doomed);

            doomed.IsLastShieldApproach.Value = false;
            _controller.Advance(0f);

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [Test]
        public void Tick_DoomedBallRetiredWithoutEnded_ReleasesTheClaim()
        {
            var doomed = Fly();
            Doom(doomed);

            _registry.Land(doomed);
            _registry.Retire(doomed);
            _controller.Advance(0f);

            Assert.AreEqual(1f, Time.timeScale, 0.001f, "game over / reset never publishes DoomedEnded");
        }

        [Test]
        public void DoomedStarted_ForABallTheRegistryNeverTracked_IsIgnored()
        {
            var stray = new ProjectileModel();
            stray.IsLastShieldApproach.Value = true;

            _startedHandler.Handle(new ProjectileDoomedStartedMessage(stray, Vector3.zero));

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        // As the view does it: the flag flips, then the message goes out.
        private void Doom(ProjectileModel ball)
        {
            ball.IsLastShieldApproach.Value = true;
            _startedHandler.Handle(new ProjectileDoomedStartedMessage(ball, Vector3.zero));
        }

        private ProjectileModel Fly()
        {
            var ball = new ProjectileModel();
            _registry.Load(ball, null);
            _registry.Launch(ball);
            return ball;
        }

        private static ISubscriber<T> Capture<T>(Action<IMessageHandler<T>> capture)
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

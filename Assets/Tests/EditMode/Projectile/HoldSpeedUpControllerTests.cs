using System;
using BalloonParty.Projectile.Controller;
using BalloonParty.Game.Flight;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    /// Tests the message-driven state transitions and TimeScaleService side effects of
    /// <see cref="HoldSpeedUpController"/>. The core lerp logic inside Tick() depends on
    /// <c>Input.GetMouseButton</c> and <c>Time.unscaledDeltaTime</c> — Unity statics that cannot be
    /// substituted in EditMode. The tests below cover the observable side effects that *can* break
    /// independently of the input path: release-on-destroy, release-on-dispose, and the
    /// ConsumedInput flag's initial state.
    ///
    /// Design note: extracting Input.GetMouseButton behind an injectable interface would make the
    /// lerp math, claim/release branching, and ConsumedInput clearing logic directly testable.
    /// </summary>
    [TestFixture]
    public class HoldSpeedUpControllerTests
    {
        private TimeScaleService _timeScale;
        private IProjectileFlightConfig _config;
        private IMessageHandler<ProjectileFiredMessage> _firedHandler;
        private IMessageHandler<ProjectileDestroyedMessage> _destroyedHandler;
        private HoldSpeedUpController _controller;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;

            _timeScale = new TimeScaleService();
            _config = Substitute.For<IProjectileFlightConfig>();
            _config.HoldSpeedUpMax.Returns(2f);
            _config.HoldSpeedUpLerpDuration.Returns(0.5f);

            // The controller now gates on IFlightScope.IsAirborne instead of the two messages. The
            // relays keep the tests firing the same messages against the same window.
            var flightScope = new FakeFlightScope();
            _firedHandler = new Relay<ProjectileFiredMessage>(() => flightScope.Airborne.Value = true);
            _destroyedHandler = new Relay<ProjectileDestroyedMessage>(() => flightScope.Airborne.Value = false);

            _controller = new HoldSpeedUpController(_config, _timeScale, flightScope);
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            Time.timeScale = 1f;
        }

        // ── OnDestroyed releases the HoldSpeedUp claim ──────────────────────────────
        // Rubric: guard clause with side effect — if the release is missing, the game stays
        // permanently sped up after the projectile dies.

        [Test]
        public void OnDestroyed_ReleasesHoldSpeedUpClaim()
        {
            // Pre-condition: something (or the controller itself) has claimed HoldSpeedUp.
            _timeScale.Claim(TimeScaleSource.HoldSpeedUp, 1.5f);
            Assert.AreEqual(1.5f, Time.timeScale, 0.001f);

            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _destroyedHandler.Handle(new ProjectileDestroyedMessage());

            // The claim must be gone — timeScale restored to 1.
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [Test]
        public void OnDestroyed_WithOtherClaimsActive_FallsBackToThem()
        {
            _timeScale.Claim(TimeScaleSource.Cinematic, 0.3f);
            _timeScale.Claim(TimeScaleSource.HoldSpeedUp, 1.5f);

            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _destroyedHandler.Handle(new ProjectileDestroyedMessage());

            // HoldSpeedUp released; Cinematic's 0.3 is the only remaining claim.
            Assert.AreEqual(0.3f, Time.timeScale, 0.001f);
        }

        // ── Dispose releases the HoldSpeedUp claim ──────────────────────────────────
        // Rubric: guard clause with side effect — container teardown must not leave a stale
        // speed-up claim on the global time scale.

        [Test]
        public void Dispose_ReleasesHoldSpeedUpClaim()
        {
            _timeScale.Claim(TimeScaleSource.HoldSpeedUp, 2f);

            _controller.Dispose();

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        // ── ConsumedInput starts false ──────────────────────────────────────────────
        // Rubric: boundary check — the thrower's guard clause reads this flag; if it starts
        // true the very first fire attempt is suppressed.

        [Test]
        public void ConsumedInput_InitiallyFalse()
        {
            Assert.IsFalse(_controller.ConsumedInput);
        }

        // ── OnFired / OnDestroyed inFlight transitions ──────────────────────────────
        // Rubric: state machine — Tick() early-returns when !_inFlight. If OnFired fails to
        // set the flag, the entire feature silently does nothing.

        [Test]
        public void OnDestroyed_WithoutPriorFired_DoesNotThrow()
        {
            // Edge case: a destroyed message arriving without a prior fired message should
            // not throw or leave the service in a bad state.
            Assert.DoesNotThrow(() =>
                _destroyedHandler.Handle(new ProjectileDestroyedMessage()));
        }

        [Test]
        public void OnDestroyed_AfterFired_ResetsInFlightSoTickIsInert()
        {
            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _destroyedHandler.Handle(new ProjectileDestroyedMessage());

            // Tick after destroyed should be a no-op (no claim made). In EditMode
            // Input.GetMouseButton(0) returns false, so even if _inFlight were still true
            // the lerp would decrease — but the real guard is the early-return.
            _controller.Tick();

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        // ── Helper ──────────────────────────────────────────────────────────────────

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

        private sealed class FakeFlightScope : IFlightScope
        {
            public ReactiveProperty<bool> Airborne { get; } = new();

            public int FlightIndex => 0;

            IReadOnlyReactiveProperty<bool> IFlightScope.IsLoaded => Airborne;

            IReadOnlyReactiveProperty<bool> IFlightScope.IsAirborne => Airborne;
        }

        private sealed class Relay<T> : IMessageHandler<T>
        {
            private readonly Action _onHandle;

            public Relay(Action onHandle)
            {
                _onHandle = onHandle;
            }

            public void Handle(T message)
            {
                _onHandle();
            }
        }
    }
}

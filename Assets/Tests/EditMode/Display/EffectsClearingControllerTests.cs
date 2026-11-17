using System;
using System.Reflection;
using BalloonParty.Configuration.Effects;
using BalloonParty.Display;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace BalloonParty.Tests.Display
{
    /// <summary>
    ///     The run-scoped controller binds itself to the persistent mask view. A new run's controller binds before
    ///     the old scope's disposes, so a stale dispose must not unbind its successor.
    /// </summary>
    [TestFixture]
    public class EffectsClearingControllerTests
    {
        private static readonly ClearingView View = new(new Vector2(1f, 2f), 5f, 0.5f);

        private IProjectileRegistry _registry;
        private IEffectsClearingSettings _settings;
        private Subject<IProjectileModel> _retired;
        private GameObject _host;
        private EffectsClearingMaskView _mask;

        [SetUp]
        public void SetUp()
        {
            _retired = new Subject<IProjectileModel>();
            _registry = Substitute.For<IProjectileRegistry>();
            _settings = Substitute.For<IEffectsClearingSettings>();
            _settings.Trail.Returns(true);
            _registry.Retired.Returns(_retired);
            _host = new GameObject("ClearingMask", typeof(Camera));
            _mask = _host.AddComponent<EffectsClearingMaskView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }
        }

        [Test]
        public void Start_BindsTheMaskToItself()
        {
            var controller = new EffectsClearingController(_registry, _mask, _settings);

            ((IStartable)controller).Start();

            Assert.AreSame(controller, BoundSource());
        }

        [Test]
        public void Dispose_UnbindsItself()
        {
            var controller = new EffectsClearingController(_registry, _mask, _settings);
            ((IStartable)controller).Start();

            ((IDisposable)controller).Dispose();

            Assert.IsNull(BoundSource());
        }

        [Test]
        public void Dispose_AfterANewerControllerBound_LeavesTheNewerBound()
        {
            var older = new EffectsClearingController(_registry, _mask, _settings);
            var newer = new EffectsClearingController(_registry, _mask, _settings);
            ((IStartable)older).Start();
            ((IStartable)newer).Start();

            ((IDisposable)older).Dispose();

            Assert.AreSame(newer, BoundSource());
        }

        [Test]
        public void Dispose_MaskAlreadyDestroyed_DoesNotThrow()
        {
            var controller = new EffectsClearingController(_registry, _mask, _settings);
            ((IStartable)controller).Start();
            Object.DestroyImmediate(_host);

            Assert.DoesNotThrow(() => ((IDisposable)controller).Dispose());
        }

        [Test]
        public void Retired_ForgetsTheBallsLastPosition()
        {
            var controller = new EffectsClearingController(_registry, _mask, _settings);
            ((IStartable)controller).Start();
            var source = (IEffectsClearingSource)controller;
            var ball = Substitute.For<IProjectileModel>();
            var stamps = new EffectsClearingStamps();
            InFlight(ball, new Vector2(1f, 2f));
            source.FillStamps(View, 0f, stamps);

            _retired.OnNext(ball);
            InFlight(ball, new Vector2(1f, 3f));
            source.FillStamps(View, 0f, stamps);

            // A disc (start == end), not a capsule trailing from the retired ball's old spot.
            Assert.AreEqual(stamps.Capsules[0].x, stamps.Capsules[0].z, 1e-5f);
            Assert.AreEqual(stamps.Capsules[0].y, stamps.Capsules[0].w, 1e-5f);
        }

        [Test]
        public void Dispose_StopsListeningForRetired()
        {
            var controller = new EffectsClearingController(_registry, _mask, _settings);
            ((IStartable)controller).Start();
            var source = (IEffectsClearingSource)controller;
            var ball = Substitute.For<IProjectileModel>();
            var stamps = new EffectsClearingStamps();
            InFlight(ball, new Vector2(1f, 2f));
            source.FillStamps(View, 0f, stamps);
            ((IDisposable)controller).Dispose();

            _retired.OnNext(ball);
            InFlight(ball, new Vector2(1f, 3f));
            source.FillStamps(View, 0f, stamps);

            // Still a capsule: the disposed controller no longer reacts to retirements.
            Assert.Greater(Mathf.Abs(stamps.Capsules[0].w - stamps.Capsules[0].y), 1e-3f);
        }

        private IEffectsClearingSource BoundSource()
        {
            var field = typeof(EffectsClearingMaskView).GetField("_source",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "EffectsClearingMaskView._source was renamed; update this helper");
            return (IEffectsClearingSource)field.GetValue(_mask);
        }

        private void InFlight(IProjectileModel ball, Vector2 position)
        {
            _registry.InFlightCount.Returns(1);
            _registry.InFlightAt(0).Returns(ball);
            _registry.InFlightPositionAt(0).Returns((Vector3)position);
        }
    }
}

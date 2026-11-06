using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BalloonParty.Game.Level;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Thrower;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Thrower
{
    /// <summary>
    ///     How <see cref="ThrowerController" /> holds its ball under rapid fire: fire and death never
    ///     replace the held ball, and tools fire only within the cap and the level phase. The reload
    ///     rules themselves are <see cref="ThrowerReload" />'s (see ThrowerReloadTests). The controller
    ///     needs a live view, pool and DI graph to construct, so these run on an uninitialised instance
    ///     with only the fields these paths read (reflection), which is why every dependency they must
    ///     NOT touch is left null: reaching it would throw and fail the test.
    /// </summary>
    [TestFixture]
    public class ThrowerControllerRapidFireTests
    {
        private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
        private const int Cap = 3;
        private const float Cooldown = 0.5f;

        private readonly List<GameObject> _spawned = new();

        private ThrowerController _thrower;
        private ReactiveProperty<LevelUpPhase> _phase;
        private List<ProjectileView> _inFlight;
        private ProjectileView _heldView;

        [SetUp]
        public void SetUp()
        {
            _thrower = (ThrowerController)RuntimeHelpers.GetUninitializedObject(typeof(ThrowerController));

            var config = Substitute.For<IProjectileFlightConfig>();
            config.MaxBallsInFlight.Returns(Cap);
            config.ReloadCooldownSeconds.Returns(Cooldown);

            _phase = new ReactiveProperty<LevelUpPhase>(LevelUpPhase.Playing);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Phase.Returns(_phase);

            _inFlight = new List<ProjectileView>();
            Set("_flightConfig", config);
            Set("_levelProgress", levelProgress);
            Set("_inFlight", _inFlight);
            Set("_isMovable", true);

            _heldView = NewView();
            Set("_loadedView", _heldView);
            Set("_loadedProjectile", new ProjectileModel());

            var reload = new ThrowerReload(config);
            reload.Restart();
            reload.Advance(Cooldown, 0);
            Set("_reload", reload);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        [Test]
        public void IsReadyToFire_HeldAndReloaded_True()
        {
            Assert.IsTrue(IsReadyToFire());
        }

        [Test]
        public void IsReadyToFire_CapReached_False()
        {
            FillFlight(Cap);

            Assert.IsFalse(IsReadyToFire(), "the controller must hand the reload its live flight count");
        }

        [Test]
        public void IsReadyToFire_NothingHeld_False()
        {
            Set("_loadedView", null);
            Set("_loadedProjectile", null);

            Assert.IsFalse(IsReadyToFire());
        }

        [Test]
        public void EnsureLoaded_BallAlreadyHeld_NeverReplacesIt()
        {
            // Replacing would go through LoadProjectile and its null registry/pool, so any attempt throws.
            Assert.DoesNotThrow(EnsureLoaded);

            Assert.AreSame(_heldView, Get("_loadedView"), "a shot leaving or dying must not throw the aimed ball away");
        }

        [Test]
        public void EnsureLoaded_EmptyButEntranceNotFinished_DoesNotLoad()
        {
            Set("_loadedView", null);
            Set("_loadedProjectile", null);
            Set("_isMovable", false);

            Assert.DoesNotThrow(EnsureLoaded);
        }

        [Test]
        public void EnsureLoaded_EmptyButNotPlaying_DoesNotLoad()
        {
            Set("_loadedView", null);
            Set("_loadedProjectile", null);
            _phase.Value = LevelUpPhase.Pending;

            Assert.DoesNotThrow(EnsureLoaded, "a level-up's new ball arrives with the new level, not from a death");
        }

        [Test]
        public void EnsureLoaded_EmptyMovableAndPlaying_AttemptsALoad()
        {
            Set("_loadedView", null);
            Set("_loadedProjectile", null);

            // The load reaches for the pool, which this harness deliberately leaves null.
            var ex = Assert.Throws<TargetInvocationException>(EnsureLoaded);
            Assert.IsInstanceOf<System.NullReferenceException>(ex.InnerException);
        }

        [Test]
        public void FireAt_CapReached_FiresNothing()
        {
            FillFlight(Cap);

            // Firing would launch through the null registry and throw.
            Assert.DoesNotThrow(() => _thrower.FireAt(Vector3.up));

            Assert.AreEqual(Cap, _inFlight.Count);
            Assert.AreSame(_heldView, Get("_loadedView"));
        }

        [Test]
        public void FireAt_NotPlaying_FiresNothing()
        {
            _phase.Value = LevelUpPhase.Completing;

            // Firing would launch through the null registry and throw.
            Assert.DoesNotThrow(() => _thrower.FireAt(Vector3.up), "a tool must not fire into a level-up");

            Assert.AreEqual(0, _inFlight.Count);
            Assert.AreSame(_heldView, Get("_loadedView"));
        }

        [Test]
        public void FireAt_NothingHeld_FiresNothing()
        {
            Set("_loadedView", null);
            Set("_loadedProjectile", null);

            Assert.DoesNotThrow(() => _thrower.FireAt(Vector3.up));

            Assert.AreEqual(0, _inFlight.Count);
        }

        private bool IsReadyToFire()
        {
            return (bool)Invoke("IsReadyToFire");
        }

        private void EnsureLoaded()
        {
            Invoke("EnsureLoaded");
        }

        private void FillFlight(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _inFlight.Add(NewView());
            }
        }

        private ProjectileView NewView()
        {
            var go = new GameObject("RapidFireTestBall");
            _spawned.Add(go);
            return go.AddComponent<ProjectileView>();
        }

        private object Invoke(string method)
        {
            return typeof(ThrowerController).GetMethod(method, Instance).Invoke(_thrower, null);
        }

        private void Set(string field, object value)
        {
            typeof(ThrowerController).GetField(field, Instance).SetValue(_thrower, value);
        }

        private object Get(string field)
        {
            return typeof(ThrowerController).GetField(field, Instance).GetValue(_thrower);
        }
    }
}

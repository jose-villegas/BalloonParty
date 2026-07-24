using System.Reflection;
using BalloonParty.Configuration.Effects;
using BalloonParty.Game.Level;
using BalloonParty.Shared.SceneLight;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Tests.Game
{
    // AngleForLevel/BeginSweep/SnapToLevel are private on TimeOfDayCycle (it exposes no public
    // read surface of its own; the observable effect is TimeOfDayService.CurrentDirection). The
    // formula tests invoke AngleForLevel via reflection rather than lerp-timing tricks — it's the
    // deterministic way to prove the continuous, never-wrapped angle math without depending on
    // Time.unscaledDeltaTime. Everything else asserts through CurrentDirection, matching what the
    // rest of the game actually reads.
    [TestFixture]
    public class TimeOfDayCycleTests
    {
        // 0 degrees so DirectionFromAngle/Angle01 round-trip without a wrap-boundary fuss.
        private static readonly Vector2 RestDirection = new(1f, 0f);

        private ISceneLightSettings _lightSettings;
        private ReactiveProperty<int> _level;
        private ReactiveProperty<LevelUpPhase> _phase;

        [SetUp]
        public void SetUp()
        {
            _lightSettings = Substitute.For<ISceneLightSettings>();
            _lightSettings.LightDirection.Returns(RestDirection);

            _level = new ReactiveProperty<int>(1);
            _phase = new ReactiveProperty<LevelUpPhase>(LevelUpPhase.Playing);
        }

        [TearDown]
        public void TearDown()
        {
            _level.Dispose();
            _phase.Dispose();
        }

        [Test]
        public void AngleForLevel_Level1_ReturnsAuthoredRestAngle()
        {
            var settings = CreateSettings(degreesPerLevel: 100f);
            var cycle = CreateCycle(settings, out _);

            var angle = InvokeAngleForLevel(cycle, 1);

            Assert.AreEqual(0f, angle, 0.001f);
        }

        [Test]
        public void AngleForLevel_HigherLevel_AddsDegreesPerLevelTimesLevelsClimbed()
        {
            var settings = CreateSettings(degreesPerLevel: 100f);
            var cycle = CreateCycle(settings, out _);

            var angle = InvokeAngleForLevel(cycle, 5);

            Assert.AreEqual(400f, angle, 0.001f);
        }

        [Test]
        public void AngleForLevel_PastFullCircle_IsNotWrapped()
        {
            // The whole point of "always-forward": the raw angle keeps climbing past 360 instead
            // of folding back to 40 — a wrapped value would make the sweep pick the short arc
            // backwards through the wrap seam instead of continuing forward.
            var settings = CreateSettings(degreesPerLevel: 100f);
            var cycle = CreateCycle(settings, out _);

            var angle = InvokeAngleForLevel(cycle, 5);

            Assert.Greater(angle, 360f);
        }

        [Test]
        public void Start_NightModeEnabled_SnapsDirectlyToLevelAngleWithNoTick()
        {
            var settings = CreateSettings(degreesPerLevel: 90f);
            _level.Value = 2; // base(0) + 90 = 90 degrees -> straight up (+y)
            var cycle = CreateCycle(settings, out var service);

            ((IStartable)cycle).Start();

            AssertDirectionNear(service.CurrentDirection, 90f);
        }

        [Test]
        public void ResetRun_NightModeEnabled_SnapsDirectlyToLevelAngleWithNoTick()
        {
            var settings = CreateSettings(degreesPerLevel: 90f);
            var cycle = CreateCycle(settings, out var service);
            ((IStartable)cycle).Start();

            // A restart can resume at a level > 1 (the StartLevel dev cheat) — ResetRun must read
            // whatever the level reset landed on, not assume level 1.
            _level.Value = 3; // base(0) + 2*90 = 180 degrees
            cycle.ResetRun(generation: 1);

            AssertDirectionNear(service.CurrentDirection, 180f);
        }

        [Test]
        public void PhaseToPending_DoesNotSweep()
        {
            var settings = CreateSettings(degreesPerLevel: 90f);
            var cycle = CreateCycle(settings, out var service);
            ((IStartable)cycle).Start(); // snaps to level 1 = 0 degrees

            _level.Value = 2;
            _phase.Value = LevelUpPhase.Pending;
            ((ITickable)cycle).Tick();

            // Still at level 1's angle — only Transitioning may move it.
            AssertDirectionNear(service.CurrentDirection, 0f);
        }

        [Test]
        public void PhaseToTransitioning_ImmediateSweepDuration_SnapsToNewLevelAngleOnFirstTick()
        {
            // SweepDuration <= 0 makes Tick's `t` unconditionally 1 on the very first call
            // (see TimeOfDayCycle.Tick), so the endpoint is reachable without depending on
            // Time.unscaledDeltaTime for a specific frame.
            var settings = CreateSettings(degreesPerLevel: 90f, sweepDuration: 0f);
            var cycle = CreateCycle(settings, out var service);
            ((IStartable)cycle).Start(); // snaps to level 1 = 0 degrees

            _level.Value = 2; // target = 90 degrees
            _phase.Value = LevelUpPhase.Transitioning;
            ((ITickable)cycle).Tick();

            AssertDirectionNear(service.CurrentDirection, 90f);
        }

        [Test]
        public void PhaseToTransitioning_AfterSweepCompletes_FurtherTicksAreNoOps()
        {
            var settings = CreateSettings(degreesPerLevel: 90f, sweepDuration: 0f);
            var cycle = CreateCycle(settings, out var service);
            ((IStartable)cycle).Start();

            _level.Value = 2;
            _phase.Value = LevelUpPhase.Transitioning;
            ((ITickable)cycle).Tick();
            ((ITickable)cycle).Tick();
            ((ITickable)cycle).Tick();

            AssertDirectionNear(service.CurrentDirection, 90f);
        }

        [Test]
        public void NightModeDisabled_Start_NeverDrivesTheService()
        {
            var settings = CreateSettings(degreesPerLevel: 90f, nightModeEnabled: false);
            var cycle = CreateCycle(settings, out var service);
            var before = service.CurrentDirection;

            ((IStartable)cycle).Start();

            Assert.AreEqual(before, service.CurrentDirection);
        }

        [Test]
        public void NightModeDisabled_PhaseTransitioningAfterStart_NeverSubscribedSoNoSweep()
        {
            var settings = CreateSettings(degreesPerLevel: 90f, nightModeEnabled: false);
            var cycle = CreateCycle(settings, out var service);
            var before = service.CurrentDirection;
            ((IStartable)cycle).Start();

            // If Start had subscribed anyway, this would flip _sweeping and the Ticks below would
            // move the direction away from its untouched value.
            _level.Value = 2;
            _phase.Value = LevelUpPhase.Transitioning;
            ((ITickable)cycle).Tick();
            ((ITickable)cycle).Tick();

            Assert.AreEqual(before, service.CurrentDirection);
        }

        [Test]
        public void NightModeDisabled_ResetRun_NeverDrivesTheService()
        {
            var settings = CreateSettings(degreesPerLevel: 90f, nightModeEnabled: false);
            var cycle = CreateCycle(settings, out var service);
            var before = service.CurrentDirection;

            cycle.ResetRun(generation: 1);

            Assert.AreEqual(before, service.CurrentDirection);
        }

        [Test]
        public void Dispose_UnsubscribesPhase_TransitioningNoLongerSweeps()
        {
            var settings = CreateSettings(degreesPerLevel: 90f, sweepDuration: 0f);
            var cycle = CreateCycle(settings, out var service);
            ((IStartable)cycle).Start();

            cycle.Dispose();

            _level.Value = 2;
            _phase.Value = LevelUpPhase.Transitioning;
            ((ITickable)cycle).Tick();

            // Still at level 1's angle: the subscription that would have started the sweep was
            // torn down by Dispose.
            AssertDirectionNear(service.CurrentDirection, 0f);
        }

        private TimeOfDayCycle CreateCycle(ITimeOfDaySettings settings, out TimeOfDayService service)
        {
            service = new TimeOfDayService(_lightSettings, settings);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);
            levelProgress.Phase.Returns(_phase);
            return new TimeOfDayCycle(settings, _lightSettings, levelProgress, service);
        }

        private static ITimeOfDaySettings CreateSettings(
            float degreesPerLevel, float sweepDuration = 1f, bool nightModeEnabled = true)
        {
            var settings = Substitute.For<ITimeOfDaySettings>();
            settings.NightModeEnabled.Returns(nightModeEnabled);
            settings.DegreesPerLevel.Returns(degreesPerLevel);
            settings.SweepDuration.Returns(sweepDuration);
            settings.SweepEase.Returns((AnimationCurve)null); // falls back to linear (Ease's null guard)
            return settings;
        }

        private static float InvokeAngleForLevel(TimeOfDayCycle cycle, int level)
        {
            return (float)typeof(TimeOfDayCycle)
                .GetMethod("AngleForLevel", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(cycle, new object[] { level });
        }

        private static void AssertDirectionNear(Vector2 actual, float expectedDegrees)
        {
            var expected = new Vector2(
                Mathf.Cos(expectedDegrees * Mathf.Deg2Rad), Mathf.Sin(expectedDegrees * Mathf.Deg2Rad));
            Assert.AreEqual(expected.x, actual.x, 0.001f);
            Assert.AreEqual(expected.y, actual.y, 0.001f);
        }
    }
}

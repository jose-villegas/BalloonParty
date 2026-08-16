using BalloonParty.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    // No fixture existed for this config at all before the aim-precision work landed three new knobs
    // (AimAngleMinDegrees/AimAngleMaxDegrees/AimLatchSeconds) alongside the earlier AimAngleStepDegrees —
    // mirrors ItemPreviewConfigTests' Defaults_AreUsable pattern for the unassigned-asset path
    // (GameLifetimeScope registers a bare CreateInstance when no asset is wired). Scoped to the Aiming
    // header only: the rest of this asset is plain [SerializeField]-backed properties, too simple to
    // break, and out of scope for this pass.
    [TestFixture]
    public class ProjectileFlightConfigTests
    {
        private ProjectileFlightConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProjectileFlightConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Defaults_AreUsable()
        {
            // 0 is legitimate for both — it is each knob's own explicit "off" state (continuous aim,
            // fire the live direction) rather than a value that would break anything downstream.
            Assert.GreaterOrEqual(_config.AimAngleStepDegrees, 0f);
            Assert.GreaterOrEqual(_config.AimLatchSeconds, 0f);
        }

        // The range is never opt-in the way the step is (ClampAndQuantizeAimDirection/ClampToReachableAngle
        // always clamp into it) — an inverted or empty default here would make the shot un-aimable in
        // every direction from the very first frame, not just a cosmetic tuning miss. OnValidate guards
        // this in the editor on every edit, but that never runs on a bare CreateInstance() (the exact
        // unassigned-asset path this fixture exercises), so the authored defaults are the only thing
        // standing between a fresh instance and a broken range.
        [Test]
        public void Defaults_AimAngleRangeIsNonEmptyAndOrdered()
        {
            Assert.Less(_config.AimAngleMinDegrees, _config.AimAngleMaxDegrees);
        }

        // AimAngleGrid.ClampToReachableAngle measures from +X (0 = due right, 90 = straight up) —
        // pins the authored default range actually sits within one full turn, not merely min < max,
        // since a range like [400, 500] would still pass the ordering check above while aiming nowhere
        // near the board.
        [Test]
        public void Defaults_AimAngleRangeIsWithinOneFullTurn()
        {
            Assert.GreaterOrEqual(_config.AimAngleMinDegrees, -360f);
            Assert.LessOrEqual(_config.AimAngleMaxDegrees, 360f);
        }
    }
}

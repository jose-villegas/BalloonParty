using BalloonParty.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class ItemPreviewConfigTests
    {
        private ItemPreviewConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ItemPreviewConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        // The unassigned-asset path: GameLifetimeScope registers a bare CreateInstance when no asset is
        // wired, so the defaults alone have to produce a working telegraph.
        [Test]
        public void Defaults_AreUsable()
        {
            Assert.Greater(_config.DashLength, 0f);
            Assert.GreaterOrEqual(_config.DashSpacing, 0f);
            Assert.Greater(_config.MaxPens, 0);
            Assert.Greater(_config.TraceSpeed, 0f);
            Assert.Greater(_config.BloomDuration, 0f);
            Assert.GreaterOrEqual(_config.SightDelaySeconds, 0f, "0 is legitimate — it means show immediately");
            Assert.GreaterOrEqual(_config.RebloomHoldSeconds, 0f, "0 is legitimate — it means never re-bloom");
        }

        // An empty AnimationCurve evaluates to 0 everywhere, which would collapse every pen's travel to
        // zero and strand the whole figure at its entry point — the fallback exists so a newly-added
        // curve field can't do that.
        [Test]
        public void BloomCurve_IsNeverEmpty()
        {
            var curve = _config.BloomCurve;

            Assert.Greater(curve.length, 0);
            Assert.AreEqual(0f, curve.Evaluate(0f), 0.001f, "no arc drawn yet");
            Assert.AreEqual(1f, curve.Evaluate(1f), 0.001f, "every pen has reached its dash slot");
        }

        // Shield's stub length is a per-item block (mirroring ItemSettings' Bomb/Laser/Paint nesting), so
        // it must be present and usable on the unassigned-asset path too.
        [Test]
        public void Shield_HasAUsableDefaultStubLength()
        {
            Assert.IsNotNull(_config.Shield);
            Assert.Greater(_config.Shield.StubLength, 0f);
        }
    }
}

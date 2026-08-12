using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
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
        }

        // Every item type must get a style, including ones with no authored entry — the ticker reads it
        // unconditionally and has no null branch.
        [Test]
        public void StyleFor_EveryItemType_IsNeverNull()
        {
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                Assert.IsNotNull(_config.StyleFor(type), $"no style for {type}");
            }
        }

        // Neutral means "use the shared value": the ticker treats 0 as no-override, so an unauthored
        // entry must report 0 rather than some plausible-looking default that would silently win.
        [Test]
        public void StyleFor_UnauthoredEntry_ReportsNeutralOverrides()
        {
            var style = _config.StyleFor(ItemType.Bomb);

            Assert.AreEqual(0f, style.RibbonSeconds, "0 means keep the prefab's authored ribbon time");
            Assert.AreEqual(ItemPreviewBloomDraw.Inherit, style.BloomDraw, "defers to the shared flag");
        }

        // An empty AnimationCurve evaluates to 0 everywhere, which would collapse every bloom to a pen
        // sitting motionless on the host — the fallback exists so a newly-added curve field can't do that.
        [Test]
        public void BloomCurve_IsNeverEmpty()
        {
            var curve = _config.BloomCurve;

            Assert.Greater(curve.length, 0);
            Assert.AreEqual(0f, curve.Evaluate(0f), 0.001f, "starts at the host");
            Assert.AreEqual(1f, curve.Evaluate(1f), 0.001f, "and lands on the stroke entry");
        }

        // Shield's stub length is a per-item block (mirroring ItemSettings' Bomb/Laser/Paint nesting), so
        // it must be present and usable on the unassigned-asset path too.
        [Test]
        public void Shield_HasAUsableDefaultStubLength()
        {
            Assert.IsNotNull(_config.Shield);
            Assert.Greater(_config.Shield.StubLength, 0f);
        }

        [Test]
        public void StyleFor_OutOfRangeType_FallsBackInsteadOfThrowing()
        {
            Assert.DoesNotThrow(() => _config.StyleFor((ItemType)999));
            Assert.IsNotNull(_config.StyleFor((ItemType)999));
        }
    }
}

using System.Reflection;
using BalloonParty.Configuration.Effects;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Display
{
    [TestFixture]
    public class EffectsClearingSettingsTests
    {
        private EffectsClearingSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<EffectsClearingSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [TestCase(0.35f, 0.05f)]
        [TestCase(0.2f, 0.2f)]
        [TestCase(0.05f, 0.5f)]
        public void EdgeHigh_AlwaysAboveEdgeLow(float edgeHigh, float edgeLow)
        {
            Set("_edgeHigh", edgeHigh);
            Set("_edgeLow", edgeLow);

            // The composite shader's smoothstep needs a gap; an authored high at or under the low must not
            // collapse it.
            Assert.Greater(_settings.EdgeHigh, _settings.EdgeLow);
        }

        [Test]
        public void EdgeHigh_AuthoredAboveEdgeLow_IsUnchanged()
        {
            Set("_edgeLow", 0.05f);
            Set("_edgeHigh", 0.35f);

            Assert.AreEqual(0.35f, _settings.EdgeHigh);
        }

        private void Set(string field, float value)
        {
            typeof(EffectsClearingSettings)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_settings, value);
        }
    }
}

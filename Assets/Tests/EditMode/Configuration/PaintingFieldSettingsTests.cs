using System.Reflection;
using BalloonParty.Configuration.Effects;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class PaintingFieldSettingsTests
    {
        private PaintingFieldSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PaintingFieldSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void WindDirection_IsNormalized()
        {
            SetField(_settings, "_windDirection", new Vector2(3f, 4f));

            var dir = _settings.WindDirection;

            Assert.AreEqual(1f, dir.magnitude, 0.001f);
        }

        [Test]
        public void WindDirection_PreservesDirection()
        {
            SetField(_settings, "_windDirection", new Vector2(3f, 4f));

            var dir = _settings.WindDirection;
            var expected = new Vector2(3f, 4f).normalized;

            Assert.AreEqual(expected.x, dir.x, 0.001f);
            Assert.AreEqual(expected.y, dir.y, 0.001f);
        }

        [Test]
        public void WindDirection_ZeroVector_ReturnsZero()
        {
            // Edge case: zero vector normalized returns zero (Unity behavior).
            SetField(_settings, "_windDirection", Vector2.zero);

            var dir = _settings.WindDirection;

            Assert.AreEqual(0f, dir.magnitude, 0.001f);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(target, value);
        }
    }
}

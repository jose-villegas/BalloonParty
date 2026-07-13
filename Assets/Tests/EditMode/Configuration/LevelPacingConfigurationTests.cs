using System.Reflection;
using BalloonParty.Configuration;
using NUnit.Framework;
using UnityEngine;
using BalloonParty.Configuration.Level;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class LevelPacingConfigurationTests
    {
        private LevelPacingConfiguration _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LevelPacingConfiguration>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Defaults_HaveOneOpenEndedRangeWithAPositiveWeight()
        {
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(1, pacing.Ranges.Count);
            Assert.IsTrue(pacing.Ranges[0].IsOpenEnded);
            Assert.Greater(pacing.Ranges[0].Parameters.BalloonWeights[0].Weight, 0f);
        }

        [Test]
        public void Defaults_ThresholdForLevelIsPositiveAndNonDecreasing()
        {
            ILevelPacingConfiguration pacing = _config;

            var previous = 0;
            for (var level = 1; level <= 10; level++)
            {
                var threshold = pacing.ThresholdForLevel(level);
                Assert.Greater(threshold, 0, $"level {level} threshold should be positive");
                Assert.GreaterOrEqual(threshold, previous, $"level {level} threshold should not drop");
                previous = threshold;
            }
        }

        [Test]
        public void OnValidate_SortsRangesByFromLevel()
        {
            var second = MakeRange(10, 0);
            var first = MakeRange(1, 9);
            SetField(_config, "_ranges", new[] { second, first });

            InvokeOnValidate();

            ILevelPacingConfiguration pacing = _config;
            Assert.AreEqual(1, pacing.Ranges[0].FromLevel);
            Assert.AreEqual(10, pacing.Ranges[1].FromLevel);
        }

        [Test]
        public void OnValidate_DoesNotThrow_WithDefaultData()
        {
            Assert.DoesNotThrow(InvokeOnValidate);
        }

        private static LevelRangeEntry MakeRange(int fromLevel, int toLevel)
        {
            return new LevelRangeEntry(fromLevel, toLevel, new RangedLevelParameters());
        }

        private void InvokeOnValidate()
        {
            typeof(LevelPacingConfiguration)
                .GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(_config, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}

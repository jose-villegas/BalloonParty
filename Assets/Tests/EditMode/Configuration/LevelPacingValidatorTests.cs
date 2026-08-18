using System;
using System.Reflection;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class LevelPacingValidatorTests
    {
        private ILevelPacingConfiguration _config;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<ILevelPacingConfiguration>();
            _config.ThresholdForLevel(Arg.Any<int>()).Returns(10);
            _config.ColorsForLevel(Arg.Any<int>()).Returns(1);
        }

        [Test]
        public void Validate_SingleOpenEndedRangeWithDefaultFallback_ReturnsNoIssues()
        {
            _config.Ranges.Returns(new[]
            {
                MakeFallbackRange(),
                new LevelRangeEntry(1, 0, new RangedLevelParameters()),
            });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 10, "Test");

            CollectionAssert.IsEmpty(issues);
        }

        [Test]
        public void Validate_GapBetweenRanges_ReportsIssue()
        {
            _config.Ranges.Returns(new[]
            {
                MakeFallbackRange(),
                new LevelRangeEntry(1, 5, new RangedLevelParameters()),
                new LevelRangeEntry(7, 10, new RangedLevelParameters()), // level 6 is a gap
            });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("gap or overlap"));
        }

        [Test]
        public void Validate_OpenEndedRangeFollowedByAnother_ReportsUnreachable()
        {
            _config.Ranges.Returns(new[]
            {
                MakeFallbackRange(),
                new LevelRangeEntry(1, 0, new RangedLevelParameters()), // open-ended — eats everything after
                new LevelRangeEntry(10, 20, new RangedLevelParameters()), // unreachable
            });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("unreachable"));
        }

        [Test]
        public void Validate_MissingDefaultFallback_ReportsIssue()
        {
            _config.Ranges.Returns(new[]
            {
                new LevelRangeEntry(1, 0, new RangedLevelParameters()),
            });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("missing default fallback"));
        }

        [Test]
        public void Validate_DuplicateFallbackId_ReportsIssue()
        {
            _config.Ranges.Returns(new[]
            {
                MakeFallbackRange(),
                MakeFallbackRange(),
                new LevelRangeEntry(1, 0, new RangedLevelParameters()),
            });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("duplicate fallback ID"));
        }

        [Test]
        public void Validate_RangeWithNoPositiveWeight_ReportsIssue()
        {
            var emptyParameters = new RangedLevelParameters();
            SetField(emptyParameters, "_balloonWeights", Array.Empty<BalloonTypeWeight>());

            _config.Ranges.Returns(new[]
            {
                MakeFallbackRange(),
                new LevelRangeEntry(1, 0, emptyParameters),
            });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("no balloon type with a positive weight"));
        }

        [Test]
        public void Validate_NonPositiveThreshold_ReportsIssue()
        {
            _config.Ranges.Returns(new[] { MakeFallbackRange(), new LevelRangeEntry(1, 0, new RangedLevelParameters()) });
            _config.ThresholdForLevel(3).Returns(0);

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 5, "Test");

            Assert.That(issues, Has.Some.Contains("non-positive"));
        }

        [Test]
        public void Validate_DroppingTotalDifficulty_ReportsIssue()
        {
            _config.Ranges.Returns(new[] { MakeFallbackRange(), new LevelRangeEntry(1, 0, new RangedLevelParameters()) });
            _config.ThresholdForLevel(1).Returns(100);
            _config.ThresholdForLevel(2).Returns(10);

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 2, "Test");

            Assert.That(issues, Has.Some.Contains("total difficulty drops"));
        }

        [Test]
        public void Validate_ZeroLevelsToCheck_SkipsThresholdScanEntirely()
        {
            _config.Ranges.Returns(new[] { MakeFallbackRange(), new LevelRangeEntry(1, 0, new RangedLevelParameters()) });
            _config.ThresholdForLevel(Arg.Any<int>()).Returns(0); // would report if the scan ever ran

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            CollectionAssert.IsEmpty(issues);
        }

        private static LevelRangeEntry MakeFallbackRange()
        {
            return new LevelRangeEntry(-1, -1, new RangedLevelParameters());
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}

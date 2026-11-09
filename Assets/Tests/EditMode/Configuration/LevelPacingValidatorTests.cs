using System;
using System.Reflection;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Slots.Actor.Archetype;
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
        public void Validate_NoPresets_ReturnsNoIssues()
        {
            _config.DebugPresets.Returns(Array.Empty<LevelRangeEntry>());

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 10, "Test");

            CollectionAssert.IsEmpty(issues);
        }

        [Test]
        public void Validate_ValidPreset_ReturnsNoIssues()
        {
            _config.DebugPresets.Returns(new[] { MakePreset(-100) });

            CollectionAssert.IsEmpty(LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test"));
        }

        [Test]
        public void Validate_PositiveIdPreset_ReportsItIsIgnored()
        {
            _config.DebugPresets.Returns(new[] { new LevelRangeEntry(3, 5, new RangedLevelParameters()) });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("positive level"));
        }

        [Test]
        public void Validate_PositiveIdPreset_DoesNotAlsoCountAsADuplicate()
        {
            _config.DebugPresets.Returns(new[]
            {
                new LevelRangeEntry(3, 5, new RangedLevelParameters()),
                new LevelRangeEntry(3, 5, new RangedLevelParameters()),
            });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.None.Contains("duplicate"));
        }

        [Test]
        public void Validate_DuplicatePresetId_ReportsIssue()
        {
            _config.DebugPresets.Returns(new[] { MakePreset(-100), MakePreset(-100) });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("duplicate preset ID"));
        }

        [Test]
        public void Validate_PresetWithNoPositiveWeight_ReportsIssue()
        {
            var emptyParameters = new RangedLevelParameters();
            SetField(emptyParameters, "_balloonWeights", Array.Empty<BalloonTypeWeight>());
            _config.DebugPresets.Returns(new[] { new LevelRangeEntry(-100, -1, emptyParameters) });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("no balloon type with a positive weight"));
        }

        [Test]
        public void Validate_PresetWithOnlyZeroWeights_ReportsIssue()
        {
            var parameters = new RangedLevelParameters();
            SetField(parameters, "_balloonWeights", new[] { new BalloonTypeWeight(BalloonParty.Balloon.Type.BalloonType.Simple, 0f) });
            _config.DebugPresets.Returns(new[] { new LevelRangeEntry(-100, -1, parameters) });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("no balloon type with a positive weight"));
        }

        [Test]
        public void Validate_PresetWithNoParameters_ReportsIssueInsteadOfThrowing()
        {
            _config.DebugPresets.Returns(new[] { new LevelRangeEntry(-100, -1, null) });

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            Assert.That(issues, Has.Some.Contains("no balloon type with a positive weight"));
        }

        [Test]
        public void Validate_NonPositiveThreshold_ReportsIssue()
        {
            _config.DebugPresets.Returns(Array.Empty<LevelRangeEntry>());
            _config.ThresholdForLevel(3).Returns(0);

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 5, "Test");

            Assert.That(issues, Has.Some.Contains("non-positive"));
        }

        [Test]
        public void Validate_DroppingTotalDifficulty_ReportsIssue()
        {
            _config.DebugPresets.Returns(Array.Empty<LevelRangeEntry>());
            _config.ThresholdForLevel(1).Returns(100);
            _config.ThresholdForLevel(2).Returns(10);

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 2, "Test");

            Assert.That(issues, Has.Some.Contains("total difficulty drops"));
        }

        [Test]
        public void Validate_ZeroLevelsToCheck_SkipsThresholdScanEntirely()
        {
            _config.DebugPresets.Returns(Array.Empty<LevelRangeEntry>());
            _config.ThresholdForLevel(Arg.Any<int>()).Returns(0); // would report if the scan ever ran

            var issues = LevelPacingValidator.Validate(_config, levelsToCheck: 0, "Test");

            CollectionAssert.IsEmpty(issues);
        }

        [Test]
        public void ValidateGroundOnlyInPresets_PresetListingAGroundOnlyActor_ReportsIt()
        {
            var presets = new[] { MakePresetWithGates(-100, new GridActorTypeGate(GridActorType.Bush, new RangedInt(3, 12))) };

            var issues = LevelPacingValidator.ValidateGroundOnlyInPresets(presets, BushOnGround(), "Test");

            Assert.That(issues, Has.Exactly(1).Contains("preset -100 lists Bush"));
        }

        [Test]
        public void ValidateGroundOnlyInPresets_GroundOnlyActorAtCountZero_IsFine()
        {
            var presets = new[] { MakePresetWithGates(-100, new GridActorTypeGate(GridActorType.Bush, new RangedInt(0, 0))) };

            CollectionAssert.IsEmpty(LevelPacingValidator.ValidateGroundOnlyInPresets(presets, BushOnGround(), "Test"));
        }

        [Test]
        public void ValidateGroundOnlyInPresets_ActorNotMarkedGroundOnly_IsFine()
        {
            var presets = new[] { MakePresetWithGates(-100, new GridActorTypeGate(GridActorType.Puff, new RangedInt(3, 6))) };

            CollectionAssert.IsEmpty(LevelPacingValidator.ValidateGroundOnlyInPresets(presets, BushOnGround(), "Test"));
        }

        [Test]
        public void ValidateGroundOnlyInPresets_PresetWithNoParameters_IsSkipped()
        {
            var presets = new[] { new LevelRangeEntry(-100, -1, null) };

            CollectionAssert.IsEmpty(LevelPacingValidator.ValidateGroundOnlyInPresets(presets, BushOnGround(), "Test"));
        }

        private static GridActorContentEntry[] BushOnGround()
        {
            return new[]
            {
                new GridActorContentEntry(GridActorType.Puff, 1, 0, new RangedInt(1, 1), new RangedInt(1, 1), 0),
                new GridActorContentEntry(GridActorType.Bush, 6, 1, new RangedInt(1, 1), new RangedInt(1, 1), 0,
                    requiresGround: true),
            };
        }

        private static LevelRangeEntry MakePresetWithGates(int id, params GridActorTypeGate[] gates)
        {
            var parameters = new RangedLevelParameters();
            SetField(parameters, "_gridActorGates", gates);
            return new LevelRangeEntry(id, -1, parameters);
        }

        private static LevelRangeEntry MakePreset(int id)
        {
            return new LevelRangeEntry(id, -1, new RangedLevelParameters());
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}

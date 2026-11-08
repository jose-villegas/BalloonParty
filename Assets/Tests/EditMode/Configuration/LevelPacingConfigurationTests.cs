using System.Linq;
using System.Reflection;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
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
        private LevelGenerationSettings _generation;

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
        public void Defaults_HaveNoDebugPresets_AndGenerateAPlayableLevelOne()
        {
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(0, pacing.DebugPresets.Count);
            Assert.Greater(pacing.ParametersForLevel(1).BalloonWeights[0].Weight, 0f);
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
        public void OnValidate_DoesNotReorderPresets()
        {
            SetField(_config, "_ranges", new[] { MakePreset(-200), MakePreset(-100) });

            InvokeOnValidate();

            ILevelPacingConfiguration pacing = _config;
            Assert.AreEqual(-200, pacing.DebugPresets[0].FromLevel);
            Assert.AreEqual(-100, pacing.DebugPresets[1].FromLevel);
        }

        [Test]
        public void ParametersForLevel_PositiveLevel_IsGeneratedFromTheSeed()
        {
            SetGeneration(ShippedLevelCatalogue.Build());
            ILevelPacingConfiguration pacing = _config;

            var viaConfig = pacing.ParametersForLevel(7);
            var direct = LevelGenerator.Generate(_generation, 7, pacing.SeedForLevel(7));

            Assert.AreEqual(Describe(direct), Describe(viaConfig));
        }

        [Test]
        public void ParametersForLevel_PositivePresetIsIgnored()
        {
            SetGeneration(ShippedLevelCatalogue.Build());
            SetField(_config, "_ranges", new[] { new LevelRangeEntry(3, 3, MakeParameters(boardLines: 99)) });
            ILevelPacingConfiguration pacing = _config;

            Assert.AreNotEqual(99, pacing.ParametersForLevel(3).BoardLines);
        }

        [Test]
        public void ParametersForLevel_NegativeLevel_ReturnsTheMatchingPreset()
        {
            var preset = MakeParameters(boardLines: 42);
            SetField(_config, "_ranges", new[] { MakePreset(-200), new LevelRangeEntry(-100, -1, preset) });
            ILevelPacingConfiguration pacing = _config;

            Assert.AreSame(preset, pacing.ParametersForLevel(-100));
        }

        [Test]
        public void ParametersForLevel_PresetWithNoParameters_FallsBackToDefaultsNotNull()
        {
            SetField(_config, "_ranges", new[] { new LevelRangeEntry(-100, -1, null) });
            ILevelPacingConfiguration pacing = _config;

            Assert.IsNotNull(pacing.ParametersForLevel(-100));
        }

        [Test]
        public void ParametersForLevel_UnknownPreset_GeneratesLevelOne()
        {
            SetGeneration(ShippedLevelCatalogue.Build());
            ILevelPacingConfiguration pacing = _config;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            try
            {
                var fallback = pacing.ParametersForLevel(-999);

                Assert.AreEqual(Describe(pacing.ParametersForLevel(1)), Describe(fallback));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void ScenarioForLevel_DebugPresetsAndLevelZero_AreAlwaysSky()
        {
            SetField(_config, "_ranges", new[] { MakePreset(-100) });
            SetField(_config, "_generation", GenerationWithScenario(firstGroundLevel: 1));
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(ScenarioKind.Sky, pacing.ScenarioForLevel(-100));
            Assert.AreEqual(ScenarioKind.Sky, pacing.ScenarioForLevel(0));
        }

        [Test]
        public void ScenarioForLevel_PositiveLevels_FollowTheGenerationSchedule()
        {
            SetField(_config, "_generation", GenerationWithScenario(firstGroundLevel: 3));
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(ScenarioKind.Sky, pacing.ScenarioForLevel(2));
            Assert.AreEqual(ScenarioKind.Ground, pacing.ScenarioForLevel(3));
        }

        [Test]
        public void ColorsForLevel_PositiveLevel_FollowsTheGenerationSteps()
        {
            SetGeneration(ShippedLevelCatalogue.Build());
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(1, pacing.ColorsForLevel(1));
            Assert.AreEqual(2, pacing.ColorsForLevel(2));
            Assert.AreEqual(3, pacing.ColorsForLevel(5));
        }

        [Test]
        public void ColorsForLevel_Preset_CountsTheMaskBits_WithAFloorOfOne()
        {
            var threeColors = MakeParameters(mask: 0b10101);
            var noColors = MakeParameters(mask: 0);
            SetField(_config, "_ranges", new[] { new LevelRangeEntry(-100, -1, threeColors), new LevelRangeEntry(-101, -1, noColors) });
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(3, pacing.ColorsForLevel(-100));
            Assert.AreEqual(1, pacing.ColorsForLevel(-101));
        }

        [Test]
        public void MaxConcurrentBalloons_UncappedCataloguedType_ScalesToTheLargestBoard()
        {
            SetGeneration(ShippedLevelCatalogue.Build());
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(6 * 8, pacing.MaxConcurrentBalloons(BalloonType.Simple, columns: 6));
        }

        [Test]
        public void MaxConcurrentBalloons_CappedType_UsesItsCap()
        {
            SetGeneration(ShippedLevelCatalogue.Build());
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(5, pacing.MaxConcurrentBalloons(BalloonType.Tough, columns: 6));
        }

        [Test]
        public void MaxConcurrentBalloons_TypeNoLevelCanInclude_IsZero()
        {
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(0, pacing.MaxConcurrentBalloons(BalloonType.Tough, columns: 6));
        }

        [Test]
        public void MaxConcurrentBalloons_PresetOnlyType_UsesThePresetBoard()
        {
            var preset = MakeParameters(boardLines: 2);
            SetField(preset, "_balloonWeights", new[] { new BalloonTypeWeight(BalloonType.Tough, 1f) });
            SetField(_config, "_ranges", new[] { new LevelRangeEntry(-100, -1, preset) });
            ILevelPacingConfiguration pacing = _config;

            Assert.AreEqual(12, pacing.MaxConcurrentBalloons(BalloonType.Tough, columns: 6));
        }

        [Test]
        public void OnValidate_DoesNotThrow_WithDefaultData()
        {
            Assert.DoesNotThrow(InvokeOnValidate);
        }

        private static LevelGenerationSettings GenerationWithScenario(int firstGroundLevel)
        {
            var generation = new LevelGenerationSettings();
            SetField(generation, "_scenario", new ScenarioScheduleSettings(firstGroundLevel, 4, 3, 5, 2, 4, 7919));
            return generation;
        }

        private static LevelRangeEntry MakePreset(int id)
        {
            return new LevelRangeEntry(id, -1, new RangedLevelParameters());
        }

        private static RangedLevelParameters MakeParameters(int boardLines = 5, int mask = ~0)
        {
            var parameters = new RangedLevelParameters();
            SetField(parameters, "_boardLines", boardLines);
            SetField(parameters, "_allowedColorsMask", mask);
            return parameters;
        }

        private static string Describe(RangedLevelParameters p)
        {
            return string.Join("|",
                string.Join(",", p.BalloonWeights.Select(w => $"{w.Type}:{w.Weight}:{w.GuaranteedInitialCount}")),
                string.Join(",", p.ItemWeights.Select(w => $"{w.Type}:{w.Weight}:{w.MaximumAllowedOverride}")),
                string.Join(",", p.GridActorGates.Select(g => g.Type)),
                p.BoardLines,
                p.AllowedColorsMask);
        }

        private void SetGeneration(LevelGenerationSettings generation)
        {
            _generation = generation;
            SetField(_config, "_generation", generation);
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

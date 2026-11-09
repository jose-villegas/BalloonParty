using System.Linq;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Slots.Actor.Archetype;
using NUnit.Framework;
using UnityEditor;

namespace BalloonParty.Tests.Configuration
{
    /// <summary>
    ///     Pins the shipped early game (levels 1-3) by loading the real <c>LevelPacingConfiguration.asset</c>,
    ///     so a tuning edit that changes the opening fails here rather than in a playtest.
    /// </summary>
    [TestFixture]
    public class ShippedEarlyLevelsTests
    {
        private const string AssetPath = "Assets/Configuration/LevelPacingConfiguration.asset";

        private LevelPacingConfiguration _config;
        private ILevelPacingConfiguration _pacing;

        // The loaded asset is shared, so it's read only and never destroyed.
        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<LevelPacingConfiguration>(AssetPath);
            Assert.IsNotNull(_config, $"{AssetPath} should load");
            _pacing = _config;
        }

        [Test]
        public void ShippedCatalogue_ValidatesClean()
        {
            CollectionAssert.IsEmpty(_config.Generation.Validate(_config.name));
        }

        [Test]
        public void ShippedCatalogue_GeneratesLevelsOneToFifty()
        {
            for (var level = 1; level <= 50; level++)
            {
                var parameters = _pacing.ParametersForLevel(level);
                Assert.IsTrue(parameters.BalloonWeights.Any(w => w.Weight > 0f), $"level {level} has a balloon");
            }
        }

        [Test]
        public void LevelOne_IsOneColourOnAnEightLineBoard()
        {
            var level = _pacing.ParametersForLevel(1);

            Assert.AreEqual(1, _pacing.ColorsForLevel(1));
            Assert.AreEqual(0b1, level.AllowedColorsMask);
            Assert.AreEqual(8, level.BoardLines);
        }

        [Test]
        public void LevelOne_HasSimpleAndToughBalloons_AndNothingElse()
        {
            var level = _pacing.ParametersForLevel(1);

            CollectionAssert.AreEquivalent(
                new[] { BalloonType.Simple, BalloonType.Tough },
                level.BalloonWeights.Select(w => w.Type));
        }

        [Test]
        public void LevelOne_ShieldIsTheOnlyItem_WithTwoGuaranteed()
        {
            var level = _pacing.ParametersForLevel(1);

            var shield = level.ItemWeights.Single();
            Assert.AreEqual(ItemType.Shield, shield.Type);
            Assert.AreEqual(2, shield.GuaranteedInitialCount);
            Assert.GreaterOrEqual(shield.MaximumAllowedOverride, 2, "the cap must leave room for the guarantee");
        }

        [Test]
        public void LevelOne_HasNoGridActors()
        {
            Assert.IsEmpty(_pacing.ParametersForLevel(1).GridActorGates);
        }

        [Test]
        public void LevelOne_IsTheSameEveryTime()
        {
            var a = _pacing.ParametersForLevel(1);
            var b = _pacing.ParametersForLevel(1);

            CollectionAssert.AreEqual(a.BalloonWeights.Select(w => (w.Type, w.Weight)), b.BalloonWeights.Select(w => (w.Type, w.Weight)));
        }

        [Test]
        public void LevelTwo_IntroducesRainbowBalloonsAndPaint_OnTwoColours()
        {
            var level = _pacing.ParametersForLevel(2);

            Assert.AreEqual(0b11, level.AllowedColorsMask);
            Assert.AreEqual(5, level.BoardLines);
            Assert.IsTrue(level.BalloonWeights.Any(w => w.Type == BalloonType.Rainbow));
            Assert.IsTrue(level.ItemWeights.Any(w => w.Type == ItemType.Paint));
            Assert.AreEqual(0, level.ItemWeights.Single(w => w.Type == ItemType.Shield).GuaranteedInitialCount,
                "the shield's 2 is a level-1 introduction only");
        }

        [Test]
        public void LevelTwo_GuaranteesOnePaint()
        {
            var paint = _pacing.ParametersForLevel(2).ItemWeights.Single(w => w.Type == ItemType.Paint);

            Assert.AreEqual(1, paint.GuaranteedInitialCount);
        }

        [Test]
        public void LevelThree_IntroducesBombWithOneGuaranteed_AndPuffs()
        {
            var level = _pacing.ParametersForLevel(3);

            Assert.AreEqual(1, level.ItemWeights.Single(w => w.Type == ItemType.Bomb).GuaranteedInitialCount);
            Assert.IsTrue(level.GridActorGates.Any(g => g.Type == GridActorType.Puff));
        }

        [Test]
        public void ShippedBushes_GrowOnlyOnGroundLevels()
        {
            var meadows = 0;
            for (var level = 1; level <= 120; level++)
            {
                var hasBushes = _pacing.ParametersForLevel(level).GridActorGates.Any(g => g.Type == GridActorType.Bush);
                if (_pacing.ScenarioForLevel(level) == ScenarioKind.Sky)
                {
                    Assert.IsFalse(hasBushes, $"sky level {level} has bushes");
                }
                else if (hasBushes)
                {
                    meadows++;
                }
            }

            Assert.That(meadows, Is.GreaterThan(0), "some meadow has bushes");
        }
    }
}

using BalloonParty.Configuration.Level;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class ScenarioScheduleTests
    {
        private const int LevelsChecked = 300;

        [Test]
        public void LevelsBeforeTheFirstGroundLevel_AreSky()
        {
            var settings = new ScenarioScheduleSettings(16, 2, 4, 7919);

            for (var level = -5; level < 16; level++)
            {
                Assert.AreEqual(ScenarioKind.Sky, ScenarioSchedule.ForLevel(settings, level), $"level {level}");
            }
        }

        [Test]
        public void TheFirstGroundLevel_IsAlwaysGround([Values(1, 7, 16, 40)] int first, [Values(1, 3, 99)] int salt)
        {
            var settings = new ScenarioScheduleSettings(first, 2, 4, salt);

            Assert.AreEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(settings, first));
        }

        [Test]
        public void SameLevel_GetsTheSameScenario_WhateverWasAskedBefore()
        {
            // A retry or a start-level cheat asks out of order; the answer can't depend on the asking history.
            var settings = new ScenarioScheduleSettings(16, 2, 4, 7919);
            var fresh = new ScenarioKind[LevelsChecked];
            for (var level = 1; level < LevelsChecked; level++)
            {
                fresh[level] = ScenarioSchedule.ForLevel(settings, level);
            }

            for (var level = LevelsChecked - 1; level >= 1; level--)
            {
                Assert.AreEqual(fresh[level], ScenarioSchedule.ForLevel(settings, level), $"level {level}");
            }
        }

        [Test]
        public void EqualMinAndMax_AlternatesEveryFixedStretch()
        {
            var settings = new ScenarioScheduleSettings(5, 2, 2, 7919);

            var expected = new[]
            {
                ScenarioKind.Ground, ScenarioKind.Ground, ScenarioKind.Sky, ScenarioKind.Sky,
                ScenarioKind.Ground, ScenarioKind.Ground, ScenarioKind.Sky, ScenarioKind.Sky,
            };
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], ScenarioSchedule.ForLevel(settings, 5 + i), $"level {5 + i}");
            }
        }

        [Test]
        public void StretchLength_NeverLeavesTheMinMaxRange([Values(1, 2, 3)] int min, [Values(3, 5)] int max)
        {
            var settings = new ScenarioScheduleSettings(1, min, max, 7919);

            for (var start = -50; start < 500; start++)
            {
                Assert.That(ScenarioSchedule.StretchLength(settings, start), Is.InRange(min, max), $"start {start}");
            }
        }

        [Test]
        public void EveryStretch_LastsBetweenMinAndMaxLevels([Values(1, 2, 3)] int min, [Values(3, 4, 6)] int max)
        {
            var settings = new ScenarioScheduleSettings(16, min, max, 7919);

            var runLength = 0;
            var previous = ScenarioSchedule.ForLevel(settings, 16);
            for (var level = 16; level < LevelsChecked; level++)
            {
                var kind = ScenarioSchedule.ForLevel(settings, level);
                if (kind == previous)
                {
                    runLength++;
                    continue;
                }

                Assert.That(runLength, Is.InRange(min, max), $"stretch ending before level {level}");
                previous = kind;
                runLength = 1;
            }
        }

        [Test]
        public void AfterTheFirstGroundLevel_BothScenariosRecur()
        {
            var settings = new ScenarioScheduleSettings(16, 2, 4, 7919);

            int ground = 0, sky = 0;
            for (var level = 16; level < 116; level++)
            {
                if (ScenarioSchedule.ForLevel(settings, level) == ScenarioKind.Ground)
                {
                    ground++;
                }
                else
                {
                    sky++;
                }
            }

            Assert.That(ground, Is.GreaterThan(20), "ground stretches recur");
            Assert.That(sky, Is.GreaterThan(20), "the run climbs back to the sky");
        }

        [Test]
        public void ZeroOrNegativeSettings_AreClampedInsteadOfLoopingForever()
        {
            var settings = new ScenarioScheduleSettings(0, 0, 0, 7919);

            Assert.AreEqual(1, settings.FirstGroundLevel);
            Assert.AreEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(settings, 1));
            Assert.AreEqual(ScenarioKind.Sky, ScenarioSchedule.ForLevel(settings, 2));
        }

        [Test]
        public void MinAboveMax_ActsAsAFixedStretchOfMin()
        {
            var settings = new ScenarioScheduleSettings(16, 3, 1, 7919);

            Assert.AreEqual(3, settings.MaxStretch);
            Assert.AreEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(settings, 18));
            Assert.AreEqual(ScenarioKind.Sky, ScenarioSchedule.ForLevel(settings, 19));
        }

        [Test]
        public void ChangingTheSalt_ReshufflesTheStretches()
        {
            var a = new ScenarioScheduleSettings(16, 2, 4, 1);
            var b = new ScenarioScheduleSettings(16, 2, 4, 2);

            var differs = false;
            for (var level = 16; level < LevelsChecked && !differs; level++)
            {
                differs = ScenarioSchedule.ForLevel(a, level) != ScenarioSchedule.ForLevel(b, level);
            }

            Assert.IsTrue(differs);
        }

        [Test]
        public void SeedForLevel_IsUnchangedByTheSharedHash()
        {
            // Values from the hash as it was inlined in SeedForLevel: a moved seed would reshuffle every level a
            // player has already seen.
            var settings = new LevelGenerationSettings();

            Assert.AreEqual(832563836, settings.SeedForLevel(1));
            Assert.AreEqual(523809828, settings.SeedForLevel(16));
            Assert.AreEqual(736111262, settings.SeedForLevel(250));
        }
    }
}

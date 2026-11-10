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
            var settings = Schedule();

            for (var level = -5; level < 16; level++)
            {
                Assert.AreEqual(ScenarioKind.Sky, ScenarioSchedule.ForLevel(settings, level), $"level {level}");
            }
        }

        [Test]
        public void TheOpeningSky_CountsFromLevelOne()
        {
            var settings = Schedule();

            AssertStretch(ScenarioSchedule.StretchAt(settings, 1), ScenarioKind.Sky, 0, 15);
            AssertStretch(ScenarioSchedule.StretchAt(settings, 15), ScenarioKind.Sky, 14, 15);
            AssertStretch(ScenarioSchedule.StretchAt(settings, -3), ScenarioKind.Sky, 0, 15);
        }

        [Test]
        public void TheFirstGroundLevel_IsAlwaysGround([Values(1, 7, 16, 40)] int first, [Values(1, 3, 99)] int salt)
        {
            var settings = Schedule(first: first, salt: salt);

            Assert.AreEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(settings, first));
        }

        [Test]
        public void TheFirstGroundStretch_IsFourLevels_WhateverTheSalt()
        {
            for (var salt = 0; salt < 50; salt++)
            {
                var settings = Schedule(salt: salt);

                for (var i = 0; i < 4; i++)
                {
                    AssertStretch(ScenarioSchedule.StretchAt(settings, 16 + i), ScenarioKind.Ground, i, 4);
                }

                Assert.AreEqual(ScenarioKind.Sky, ScenarioSchedule.ForLevel(settings, 20), $"salt {salt}");
            }
        }

        [Test]
        public void ShippedDefaults_AreTheTunedLengths()
        {
            var settings = new ScenarioScheduleSettings();

            Assert.AreEqual((6, 8), (settings.GroundFromLevel, settings.GroundByLevel));
            Assert.AreEqual(4, settings.FirstGroundStretch);
            Assert.AreEqual((3, 5), (settings.MinGroundStretch, settings.MaxGroundStretch));
            Assert.AreEqual((2, 4), (settings.MinSkyStretch, settings.MaxSkyStretch));
        }

        [Test]
        public void EveryLaterStretch_LastsWithinItsScenariosRange([Values(1, 7919, 123456)] int salt)
        {
            var settings = Schedule(salt: salt);

            var level = 20;
            while (level < LevelsChecked)
            {
                var stretch = ScenarioSchedule.StretchAt(settings, level);
                Assert.AreEqual(0, stretch.Position, $"level {level} opens a stretch");
                var range = stretch.Kind == ScenarioKind.Ground ? (3, 5) : (2, 4);
                Assert.That(stretch.Length, Is.InRange(range.Item1, range.Item2), $"{stretch.Kind} from level {level}");

                for (var i = 0; i < stretch.Length; i++)
                {
                    AssertStretch(ScenarioSchedule.StretchAt(settings, level + i), stretch.Kind, i, stretch.Length);
                }

                level += stretch.Length;
                Assert.AreNotEqual(stretch.Kind, ScenarioSchedule.ForLevel(settings, level), $"flips at level {level}");
            }
        }

        [Test]
        public void SameLevel_GetsTheSameStretch_WhateverWasAskedBefore()
        {
            // A retry or a start-level cheat asks out of order; the answer can't depend on the asking history.
            var settings = Schedule();
            var fresh = new ScenarioStretch[LevelsChecked];
            for (var level = 1; level < LevelsChecked; level++)
            {
                fresh[level] = ScenarioSchedule.StretchAt(settings, level);
            }

            var other = Schedule();
            for (var level = LevelsChecked - 1; level >= 1; level--)
            {
                AssertStretch(ScenarioSchedule.StretchAt(other, level), fresh[level].Kind, fresh[level].Position,
                    fresh[level].Length);
            }
        }

        [Test]
        public void EqualMinAndMax_AlternatesFixedStretches()
        {
            var settings = Schedule(first: 5, firstStretch: 2, minGround: 2, maxGround: 2, minSky: 3, maxSky: 3);

            var expected = new[]
            {
                ScenarioKind.Ground, ScenarioKind.Ground, ScenarioKind.Sky, ScenarioKind.Sky, ScenarioKind.Sky,
                ScenarioKind.Ground, ScenarioKind.Ground, ScenarioKind.Sky, ScenarioKind.Sky, ScenarioKind.Sky,
            };
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], ScenarioSchedule.ForLevel(settings, 5 + i), $"level {5 + i}");
            }
        }

        [Test]
        public void StretchLength_NeverLeavesItsScenariosRange([Values(1, 2, 3)] int min, [Values(3, 5)] int max)
        {
            var settings = Schedule(minGround: min, maxGround: max, minSky: min + 1, maxSky: max + 1);

            for (var start = -50; start < 500; start++)
            {
                Assert.That(ScenarioSchedule.StretchLength(settings, ScenarioKind.Ground, start, settings.Salt),
                    Is.InRange(min, max));
                Assert.That(ScenarioSchedule.StretchLength(settings, ScenarioKind.Sky, start, settings.Salt),
                    Is.InRange(min + 1, max + 1));
            }
        }

        [Test]
        public void AfterTheFirstGroundLevel_BothScenariosRecur()
        {
            var settings = Schedule();

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

            Assert.That(ground, Is.GreaterThan(30), "ground stretches recur");
            Assert.That(sky, Is.GreaterThan(20), "the run climbs back to the sky");
        }

        [Test]
        public void ZeroOrNegativeSettings_AreClampedInsteadOfLoopingForever()
        {
            var settings = Schedule(first: 0, firstStretch: 0, minGround: 0, maxGround: 0, minSky: -1, maxSky: -1);

            Assert.AreEqual(1, ScenarioSchedule.FirstGroundLevel(settings));
            Assert.AreEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(settings, 1));
            Assert.AreEqual(ScenarioKind.Sky, ScenarioSchedule.ForLevel(settings, 2));
            Assert.AreEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(settings, 3));
        }

        [Test]
        public void TheFirstGroundLevel_IsRolledWithinItsWindow_AndTheSkyHoldsUntilThen()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (var salt = 0; salt < 200; salt++)
            {
                var settings = Schedule(salt: salt, from: 11);
                var first = ScenarioSchedule.FirstGroundLevel(settings);

                Assert.That(first, Is.InRange(11, 16), $"salt {salt}");
                Assert.AreEqual(ScenarioKind.Sky, ScenarioSchedule.ForLevel(settings, first - 1), $"salt {salt}");
                AssertStretch(ScenarioSchedule.StretchAt(settings, first), ScenarioKind.Ground, 0, 4);
                seen.Add(first);
            }

            Assert.AreEqual(6, seen.Count, "every level in the window can open the first meadow");
        }

        [Test]
        public void EachRun_RollsItsOwnFirstMeadow_WithinTheWindow()
        {
            var settings = Schedule(from: 11);
            var seen = new System.Collections.Generic.HashSet<int>();

            for (var runSeed = 1; runSeed <= 200; runSeed++)
            {
                var first = ScenarioSchedule.FirstGroundLevel(settings, runSeed);

                Assert.That(first, Is.InRange(11, 16), $"run seed {runSeed}");
                Assert.AreEqual(ScenarioKind.Sky, ScenarioSchedule.ForLevel(settings, first - 1, runSeed));
                Assert.AreEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(settings, first, runSeed));
                seen.Add(first);
            }

            Assert.AreEqual(6, seen.Count, "runs spread over the whole window");
        }

        [Test]
        public void WithinARun_TheSameLevel_AlwaysGetsTheSameScenario([Values(3, 98765, -42)] int runSeed)
        {
            var settings = Schedule(from: 11);

            for (var level = 1; level < LevelsChecked; level++)
            {
                Assert.AreEqual(ScenarioSchedule.StretchAt(settings, level, runSeed),
                    ScenarioSchedule.StretchAt(settings, level, runSeed), $"level {level}");
            }
        }

        [Test]
        public void ARunSeed_ReshufflesTheLaterStretchesToo()
        {
            var settings = Schedule();
            var differ = 0;

            for (var level = 20; level < LevelsChecked; level++)
            {
                if (ScenarioSchedule.ForLevel(settings, level, 0) != ScenarioSchedule.ForLevel(settings, level, 12345))
                {
                    differ++;
                }
            }

            Assert.That(differ, Is.GreaterThan(30));
        }

        [Test]
        public void AWindowAfterItsLatestLevel_IsTheLatestLevel()
        {
            var settings = Schedule(first: 12, from: 20);

            Assert.AreEqual(12, ScenarioSchedule.FirstGroundLevel(settings));
        }

        [Test]
        public void MinAboveMax_ActsAsAFixedStretchOfMin()
        {
            var settings = Schedule(minGround: 5, maxGround: 3, minSky: 2, maxSky: 1);

            Assert.AreEqual(5, settings.MaxGroundStretch);
            Assert.AreEqual(2, settings.MaxSkyStretch);
            Assert.IsFalse(settings.IsStretchRangeValid);
            AssertStretch(ScenarioSchedule.StretchAt(settings, 20), ScenarioKind.Sky, 0, 2);
            AssertStretch(ScenarioSchedule.StretchAt(settings, 22), ScenarioKind.Ground, 0, 5);
        }

        [Test]
        public void ChangingTheSalt_ReshufflesTheStretches()
        {
            var a = Schedule(salt: 1);
            var b = Schedule(salt: 2);

            var differs = false;
            for (var level = 16; level < LevelsChecked && !differs; level++)
            {
                differs = ScenarioSchedule.ForLevel(a, level) != ScenarioSchedule.ForLevel(b, level);
            }

            Assert.IsTrue(differs);
        }

        [TestCase(1, new[] { 0.5f })]
        [TestCase(2, new[] { 0.5f, 0f })]
        [TestCase(3, new[] { 0.5f, 0f, 0f })]
        [TestCase(4, new[] { 1f, 0.5f, 0f, 0f })]
        [TestCase(5, new[] { 1f, 0.5f, 0f, 0f, 0f })]
        [TestCase(6, new[] { 1f, 1f, 0.5f, 0f, 0f, 0f })]
        public void PuffCloudShare_ClearsHalfwayThroughAMeadow(int length, float[] shares)
        {
            for (var position = 0; position < length; position++)
            {
                var stretch = new ScenarioStretch(ScenarioKind.Ground, position, length);
                Assert.AreEqual(shares[position], stretch.PuffCloudShare, $"level {position + 1} of {length}");
            }
        }

        [TestCase(3, new[] { 0.75f, 1f, 1f })]
        [TestCase(4, new[] { 0.5f, 0.75f, 1f, 1f })]
        [TestCase(5, new[] { 0.5f, 0.75f, 1f, 1f, 1f })]
        public void BushShare_GrowsAsTheCloudsGo(int length, float[] shares)
        {
            for (var position = 0; position < length; position++)
            {
                var stretch = new ScenarioStretch(ScenarioKind.Ground, position, length);
                Assert.AreEqual(shares[position], stretch.BushShare, $"level {position + 1} of {length}");
                Assert.AreEqual(stretch.PuffCloudShare < 1f, stretch.IsClearingMeadow, $"level {position + 1} of {length}");
            }
        }

        [Test]
        public void PuffCloudAndBushShares_AreFullInTheSky([Values(1, 2, 4, 15)] int length)
        {
            for (var position = 0; position < length; position++)
            {
                var stretch = new ScenarioStretch(ScenarioKind.Sky, position, length);
                Assert.AreEqual(1f, stretch.PuffCloudShare);
                Assert.AreEqual(1f, stretch.BushShare);
                Assert.IsFalse(stretch.IsClearingMeadow);
            }
        }

        [Test]
        public void TheFirstMeadow_HasCloudsOnItsFirstTwoLevels()
        {
            var settings = Schedule();

            var shares = new float[4];
            for (var i = 0; i < 4; i++)
            {
                shares[i] = ScenarioSchedule.StretchAt(settings, 16 + i).PuffCloudShare;
            }

            CollectionAssert.AreEqual(new[] { 1f, 0.5f, 0f, 0f }, shares);
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

        [Test]
        public void ShippedDefaults_MakeHalfTheLaterSurfacesWater()
        {
            Assert.AreEqual(0.5f, new ScenarioScheduleSettings().WaterShare);
        }

        [Test]
        public void Water_ChangesWhichSurfaceAStretchShows_NeverWhereItFalls([Values(0.25f, 0.5f, 1f)] float water,
            [Values(0, 3, 98765)] int runSeed)
        {
            var meadows = Schedule();
            var withWater = Schedule(water: water);

            for (var level = 1; level < LevelsChecked; level++)
            {
                var plain = ScenarioSchedule.StretchAt(meadows, level, runSeed);
                var wet = ScenarioSchedule.StretchAt(withWater, level, runSeed);
                var expected = wet.Kind == ScenarioKind.Water ? ScenarioKind.Ground : wet.Kind;
                AssertStretch(plain, expected, wet.Position, wet.Length);
            }
        }

        [Test]
        public void TheFirstMeadow_IsNeverWater_EvenAtFullShare([Values(1, 7919, 123456)] int salt)
        {
            var settings = Schedule(water: 1f, salt: salt);

            for (var i = 0; i < 4; i++)
            {
                Assert.AreEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(settings, 16 + i), $"level {16 + i}");
            }
        }

        [Test]
        public void AtFullShare_EveryLaterSurfaceIsWater_AndAtNoneEveryOneIsAMeadow()
        {
            var allWater = Schedule(water: 1f);
            var noWater = Schedule(water: 0f);

            for (var level = 20; level < LevelsChecked; level++)
            {
                Assert.AreNotEqual(ScenarioKind.Ground, ScenarioSchedule.ForLevel(allWater, level), $"level {level}");
                Assert.AreNotEqual(ScenarioKind.Water, ScenarioSchedule.ForLevel(noWater, level), $"level {level}");
            }
        }

        [Test]
        public void TwoSurfaces_AreAlwaysSeparatedBySky([Values(1, 7919, 123456)] int salt, [Values(0, 42)] int runSeed)
        {
            // The mist reveal only plays sky to surface and back: a meadow never turns straight into water.
            var settings = Schedule(water: 0.5f, salt: salt);

            for (var level = 2; level < LevelsChecked; level++)
            {
                var before = ScenarioSchedule.ForLevel(settings, level - 1, runSeed);
                var now = ScenarioSchedule.ForLevel(settings, level, runSeed);
                if (before != now)
                {
                    Assert.IsTrue(before == ScenarioKind.Sky || now == ScenarioKind.Sky, $"{before} to {now} at {level}");
                }
            }
        }

        [Test]
        public void AtHalfShare_AllThreeScenariosRecur()
        {
            var settings = Schedule(water: 0.5f);

            int sky = 0, ground = 0, water = 0;
            for (var level = 20; level < LevelsChecked; level++)
            {
                switch (ScenarioSchedule.ForLevel(settings, level))
                {
                    case ScenarioKind.Sky:
                        sky++;
                        break;
                    case ScenarioKind.Ground:
                        ground++;
                        break;
                    default:
                        water++;
                        break;
                }
            }

            Assert.That(sky, Is.GreaterThan(60), "sky");
            Assert.That(ground, Is.GreaterThan(40), "meadows");
            Assert.That(water, Is.GreaterThan(40), "water");
        }

        [Test]
        public void WithThreeScenarios_TheSameLevelOfARun_AlwaysGetsTheSameStretch([Values(0, 3, -42)] int runSeed)
        {
            var settings = Schedule(water: 0.5f);
            var fresh = new ScenarioStretch[LevelsChecked];
            for (var level = 1; level < LevelsChecked; level++)
            {
                fresh[level] = ScenarioSchedule.StretchAt(settings, level, runSeed);
            }

            var other = Schedule(water: 0.5f);
            for (var level = LevelsChecked - 1; level >= 1; level--)
            {
                AssertStretch(ScenarioSchedule.StretchAt(other, level, runSeed), fresh[level].Kind,
                    fresh[level].Position, fresh[level].Length);
            }
        }

        [Test]
        public void EachRun_RollsItsOwnWater()
        {
            var settings = Schedule(water: 0.5f);

            var differs = false;
            for (var level = 20; level < LevelsChecked && !differs; level++)
            {
                var a = ScenarioSchedule.ForLevel(settings, level, 11) == ScenarioKind.Water;
                var b = ScenarioSchedule.ForLevel(settings, level, 12) == ScenarioKind.Water;
                differs = a != b;
            }

            Assert.IsTrue(differs, "two runs lay out the same water");
        }

        [Test]
        public void PuffCloudAndBushShares_AreZeroOverWater([Values(1, 3, 5)] int length)
        {
            // The water surface has no clouds or bushes, at any point of its stretch.
            for (var position = 0; position < length; position++)
            {
                var stretch = new ScenarioStretch(ScenarioKind.Water, position, length);
                Assert.AreEqual(0f, stretch.PuffCloudShare);
                Assert.AreEqual(0f, stretch.BushShare);
                Assert.IsFalse(stretch.IsClearingMeadow);
            }
        }

        // first is the latest level the first meadow can start on; without from, it starts there exactly.
        private static ScenarioScheduleSettings Schedule(int first = 16, int firstStretch = 4, int minGround = 3,
            int maxGround = 5, int minSky = 2, int maxSky = 4, int salt = 7919, int from = 0, float water = 0f)
        {
            return new ScenarioScheduleSettings(first, firstStretch, minGround, maxGround, minSky, maxSky, salt, from,
                water);
        }

        private static void AssertStretch(ScenarioStretch stretch, ScenarioKind kind, int position, int length)
        {
            Assert.AreEqual((kind, position, length), (stretch.Kind, stretch.Position, stretch.Length));
        }
    }
}

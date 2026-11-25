using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    /// <summary>
    ///     How far apart the extra-colour (bonus) levels come: never closer than the gap allows, never further than the
    ///     soft pity lets a run wait, and the same levels each time a run's seed is asked.
    /// </summary>
    [TestFixture]
    public class ExtraColorLevelSpacingTests
    {
        private const int Seeds = 200;
        private const int LastLevel = 120;

        // The shipped numbers: the first on level 7, then 15% after two ordinary levels, 10% more for each level
        // that misses.
        private static ExtraColorLevelSettings Shipped()
        {
            return new ExtraColorLevelSettings(8, 0.15f, 0.7f, 0.3f, minLevelsBetween: 2, chanceGrowthPerLevel: 0.1f,
                firstLevel: 7);
        }

        [Test]
        public void TwoBonusLevels_HaveAtLeastTheGapBetweenThem()
        {
            foreach (var gap in Gaps(Shipped()))
            {
                Assert.GreaterOrEqual(gap, 3, "two ordinary levels between");
            }
        }

        // 15% + 10% a level reaches certain after nine misses: two ordinary levels, nine misses, then the one.
        [Test]
        public void ARun_NeverWaitsPastWhereThePityMakesItSure()
        {
            foreach (var gap in Gaps(Shipped()))
            {
                Assert.LessOrEqual(gap, 12);
            }
        }

        [Test]
        public void OnAverage_OneComesAboutEveryFiveLevels()
        {
            var gaps = Gaps(Shipped());
            var sum = 0;
            foreach (var gap in gaps)
            {
                sum += gap;
            }

            Assert.That(sum / (float)gaps.Count, Is.InRange(4.5f, 6f));
        }

        [Test]
        public void TheFirst_IsTheFirstLevel_InEveryRun()
        {
            var settings = Shipped();
            for (var seed = 0; seed < Seeds; seed++)
            {
                for (var level = 1; level < 7; level++)
                {
                    Assert.IsFalse(settings.IsExtraColorLevel(level, seed), $"seed {seed} level {level}");
                }

                Assert.IsTrue(settings.IsExtraColorLevel(7, seed), $"seed {seed}");
            }
        }

        [Test]
        public void AfterTheFirst_TheGapHolds()
        {
            var settings = Shipped();
            for (var seed = 0; seed < Seeds; seed++)
            {
                Assert.IsFalse(settings.IsExtraColorLevel(8, seed), $"seed {seed}");
                Assert.IsFalse(settings.IsExtraColorLevel(9, seed), $"seed {seed}");
            }
        }

        [Test]
        public void WithNoFirstLevel_TheFirstIsRolled_NoEarlierThanFromLevel()
        {
            var settings = new ExtraColorLevelSettings(8, 0.15f, 0.7f, 0.3f, minLevelsBetween: 2,
                chanceGrowthPerLevel: 0.1f);
            var firsts = new HashSet<int>();
            for (var seed = 0; seed < Seeds; seed++)
            {
                var level = 1;
                while (!settings.IsExtraColorLevel(level, seed))
                {
                    level++;
                }

                Assert.GreaterOrEqual(level, 8, $"seed {seed}");
                firsts.Add(level);
            }

            Assert.Greater(firsts.Count, 1, "runs roll their own first");
        }

        // Each answer walks the run from its start, so asking out of order, or again, changes nothing.
        [Test]
        public void TheAnswer_IsTheRunsOwn_InWhateverOrderItIsAsked()
        {
            var settings = Shipped();
            for (var seed = 0; seed < 20; seed++)
            {
                var forward = new List<bool>();
                for (var level = 1; level <= 60; level++)
                {
                    forward.Add(settings.IsExtraColorLevel(level, seed));
                }

                for (var level = 60; level >= 1; level--)
                {
                    Assert.AreEqual(forward[level - 1], settings.IsExtraColorLevel(level, seed), $"seed {seed} level {level}");
                }
            }
        }

        [Test]
        public void Runs_DifferInTheirBonusLevels()
        {
            var settings = Shipped();
            var differs = false;
            for (var level = 8; level <= 40 && !differs; level++)
            {
                differs = settings.IsExtraColorLevel(level, 1) != settings.IsExtraColorLevel(level, 2);
            }

            Assert.IsTrue(differs);
        }

        [TestCase(0, 0.15f)]
        [TestCase(1, 0.25f)]
        [TestCase(5, 0.65f)]
        [TestCase(9, 1f)]
        [TestCase(30, 1f)]
        public void ChanceAfter_GrowsWithEachLevelMissed_UpToCertain(int missed, float expected)
        {
            Assert.AreEqual(expected, Shipped().ChanceAfter(missed), 1e-5f);
        }

        [Test]
        public void NoChance_NeverABonusLevel_HoweverLongTheWait()
        {
            var settings = new ExtraColorLevelSettings(8, 0f, 0.7f, 0f, minLevelsBetween: 2, chanceGrowthPerLevel: 0.1f,
                firstLevel: 7);
            for (var level = 1; level <= LastLevel; level++)
            {
                Assert.IsFalse(settings.IsExtraColorLevel(level, 7), $"level {level}");
            }
        }

        private static List<int> Gaps(ExtraColorLevelSettings settings)
        {
            var gaps = new List<int>();
            for (var seed = 0; seed < Seeds; seed++)
            {
                var last = -1;
                for (var level = 1; level <= LastLevel; level++)
                {
                    if (!settings.IsExtraColorLevel(level, seed))
                    {
                        continue;
                    }

                    if (last > 0)
                    {
                        gaps.Add(level - last);
                    }

                    last = level;
                }
            }

            return gaps;
        }
    }
}

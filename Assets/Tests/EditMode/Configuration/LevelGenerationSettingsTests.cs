using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Slots.Actor.Archetype;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class LevelGenerationSettingsTests
    {
        [Test]
        public void BudgetFormula_GrowsPerLevelAfterTheFirst()
        {
            var formula = new BudgetFormula(2f, 1.5f, 100f);

            Assert.AreEqual(2f, formula.ForLevel(1), 0.0001f);
            Assert.AreEqual(3.5f, formula.ForLevel(2), 0.0001f);
            Assert.AreEqual(8f, formula.ForLevel(5), 0.0001f);
        }

        [Test]
        public void BudgetFormula_NeverExceedsItsCap()
        {
            Assert.AreEqual(12f, new BudgetFormula(2f, 1.5f, 12f).ForLevel(50), 0.0001f);
        }

        [Test]
        public void BudgetFormula_LevelBelowOne_ActsAsLevelOne()
        {
            var formula = new BudgetFormula(2f, 1.5f, 12f);

            Assert.AreEqual(2f, formula.ForLevel(0), 0.0001f);
            Assert.AreEqual(2f, formula.ForLevel(-100), 0.0001f);
        }

        [Test]
        public void BudgetFormula_BaseAboveCap_IsCapped()
        {
            Assert.AreEqual(3f, new BudgetFormula(5f, 1f, 3f).ForLevel(1), 0.0001f);
        }

        [Test]
        public void BudgetForLevel_ReadsTheFormulaOfTheRole()
        {
            var settings = Settings(threat: new BudgetFormula(2f, 0.75f, 30f), aid: new BudgetFormula(3f, 0.25f, 5f));

            Assert.AreEqual(2f, settings.BudgetForLevel(ContentRole.Threat, 1), 0.0001f);
            Assert.AreEqual(3f, settings.BudgetForLevel(ContentRole.Aid, 1), 0.0001f);
            Assert.AreEqual(30f, settings.BudgetForLevel(ContentRole.Threat, 500), 0.0001f);
            Assert.AreEqual(5f, settings.BudgetForLevel(ContentRole.Aid, 500), 0.0001f);
        }

        [Test]
        public void FeaturedLevels_NeverBelowOne()
        {
            Assert.AreEqual(1, Settings(featuredLevels: 0).FeaturedLevels);
            Assert.AreEqual(3, Settings(featuredLevels: 3).FeaturedLevels);
        }

        [Test]
        public void ColorsForLevel_StepHoldsUntilTheNextOne()
        {
            var settings = Settings(colors: new[] { new IntLevelStep(1, 1), new IntLevelStep(3, 2), new IntLevelStep(6, 4) });

            Assert.AreEqual(1, settings.ColorsForLevel(1));
            Assert.AreEqual(1, settings.ColorsForLevel(2));
            Assert.AreEqual(2, settings.ColorsForLevel(3));
            Assert.AreEqual(2, settings.ColorsForLevel(5));
            Assert.AreEqual(4, settings.ColorsForLevel(6));
            Assert.AreEqual(4, settings.ColorsForLevel(500));
        }

        [Test]
        public void ColorsForLevel_FirstStepAlsoCoversLevelsBeforeItsStart()
        {
            var settings = Settings(colors: new[] { new IntLevelStep(4, 3), new IntLevelStep(8, 5) });

            Assert.AreEqual(3, settings.ColorsForLevel(1));
        }

        [Test]
        public void ColorsForLevel_ClampsToTheMaskWidth()
        {
            var settings = Settings(colors: new[] { new IntLevelStep(1, 0), new IntLevelStep(2, 99) });

            Assert.AreEqual(1, settings.ColorsForLevel(1), "zero colours would be an empty palette");
            Assert.AreEqual(31, settings.ColorsForLevel(2), "32 would overflow the int mask");
        }

        [Test]
        public void ColorsForLevel_NoSteps_FallsBackToOne()
        {
            var settings = Settings(colors: Array.Empty<IntLevelStep>());

            Assert.AreEqual(1, settings.ColorsForLevel(7));
        }

        [Test]
        public void BoardLinesForLevel_FollowsStepsAndNeverDropsBelowOne()
        {
            var settings = Settings(boardLines: new[] { new IntLevelStep(1, 8), new IntLevelStep(2, 5), new IntLevelStep(9, 0) });

            Assert.AreEqual(8, settings.BoardLinesForLevel(1));
            Assert.AreEqual(5, settings.BoardLinesForLevel(8));
            Assert.AreEqual(1, settings.BoardLinesForLevel(9));
        }

        [Test]
        public void MaxBoardLines_IsTheLargestStep()
        {
            var settings = Settings(boardLines: new[] { new IntLevelStep(1, 8), new IntLevelStep(2, 5) });

            Assert.AreEqual(8, settings.MaxBoardLines);
        }

        [Test]
        public void ItemCountsForLevel_ReturnsTheStepInForce()
        {
            var early = new AnimationCurve(new Keyframe(0f, 1f));
            var late = new AnimationCurve(new Keyframe(0f, 2f));
            var settings = Settings(itemCounts: new[]
            {
                new ItemCountStep(1, early, early),
                new ItemCountStep(5, late, late),
            });

            Assert.AreSame(early, settings.ItemCountsForLevel(4).InitialItemCountWeights);
            Assert.AreSame(late, settings.ItemCountsForLevel(5).InitialItemCountWeights);
        }

        [Test]
        public void SeedForLevel_SameLevelSameSeed_AndNeverNegative()
        {
            var settings = Settings();

            for (var level = -100; level <= 50; level++)
            {
                Assert.AreEqual(settings.SeedForLevel(level), settings.SeedForLevel(level));
                Assert.GreaterOrEqual(settings.SeedForLevel(level), 0);
            }
        }

        [Test]
        public void SeedForLevel_DifferentLevelsGetDifferentSeeds()
        {
            var settings = Settings();
            var seen = new HashSet<int>();

            for (var level = 1; level <= 50; level++)
            {
                Assert.IsTrue(seen.Add(settings.SeedForLevel(level)), $"level {level} repeated an earlier seed");
            }
        }

        [Test]
        public void SeedForLevel_SaltChangesTheSeed()
        {
            var a = Settings(seedSalt: 1);
            var b = Settings(seedSalt: 2);

            Assert.AreNotEqual(a.SeedForLevel(3), b.SeedForLevel(3));
        }

        [Test]
        public void IndexAt_UnorderedSteps_PicksHighestStartAtOrBelowLevel()
        {
            var steps = new[] { new IntLevelStep(5, 50), new IntLevelStep(1, 10), new IntLevelStep(3, 30) };

            Assert.AreEqual(1, LevelSteps.IndexAt(steps, 2));
            Assert.AreEqual(2, LevelSteps.IndexAt(steps, 4));
            Assert.AreEqual(0, LevelSteps.IndexAt(steps, 5));
            Assert.AreEqual(0, LevelSteps.IndexAt(steps, 99));
        }

        [Test]
        public void IndexAt_BeforeEveryStart_UsesTheEarliestStepWhateverItsPosition()
        {
            var steps = new[] { new IntLevelStep(8, 80), new IntLevelStep(4, 40), new IntLevelStep(6, 60) };

            Assert.AreEqual(1, LevelSteps.IndexAt(steps, 1));
        }

        [Test]
        public void IndexAt_NoSteps_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, LevelSteps.IndexAt(Array.Empty<IntLevelStep>(), 3));
            Assert.AreEqual(-1, LevelSteps.IndexAt<IntLevelStep>(null, 3));
        }

        [Test]
        public void Validate_FixtureCatalogue_IsClean()
        {
            CollectionAssert.IsEmpty(ShippedLevelCatalogue.Build().Validate("Test"));
        }

        [Test]
        public void Validate_NoLevelOneStapleBalloon_ReportsIssue()
        {
            var settings = Settings(balloons: new[]
            {
                Balloon(BalloonType.Tough, 1, 1),
                Balloon(BalloonType.Simple, 3, 0),
            });

            Assert.That(settings.Validate("Test"), Has.Some.Contains("level-1 staple"));
        }

        [Test]
        public void Validate_StapleWithNoPositiveWeight_DoesNotCountAsStaple()
        {
            var settings = Settings(balloons: new[] { Balloon(BalloonType.Simple, 1, 0, ContentWeightRamp.Flat(0f, 0f)) });

            Assert.That(settings.Validate("Test"), Has.Some.Contains("level-1 staple"));
        }

        [Test]
        public void Validate_RampWithMinAboveMax_ReportsIssue()
        {
            var settings = Settings(balloons: new[]
            {
                Balloon(BalloonType.Simple, 1, 0),
                Balloon(BalloonType.Tough, 1, 1, ContentWeightRamp.Flat(2f, 1f)),
            });

            Assert.That(settings.Validate("Test"), Has.Some.Contains("Tough"));
        }

        [Test]
        public void Validate_LateRangeMinAboveMax_ReportsIssue()
        {
            var settings = Settings(items: new[]
            {
                new ItemContentEntry(ItemType.Bomb, 1, 1, new ContentWeightRamp(1f, 1f, 3f, 2f, 4)),
            });

            Assert.That(settings.Validate("Test"), Has.Some.Contains("Bomb"));
        }

        [Test]
        public void Validate_ItemWithNoPositiveWeight_ReportsIssue()
        {
            var settings = Settings(items: new[] { new ItemContentEntry(ItemType.Bomb, 1, 0, ContentWeightRamp.Flat(0f, 0f)) });

            Assert.That(settings.Validate("Test"), Has.Some.Contains("Bomb"));
        }

        [Test]
        public void Validate_IntroWeightAlone_MakesARampValid()
        {
            var settings = Settings(items: new[]
            {
                new ItemContentEntry(ItemType.Bomb, 1, 1, new ContentWeightRamp(0f, 0f, 0f, 0f, 0, 2f)),
            });

            Assert.That(settings.Validate("Test"), Has.None.Contains("Bomb"));
        }

        [Test]
        public void Validate_UnlockBelowOne_ReportsEveryCatalogue()
        {
            var settings = Settings(
                balloons: new[] { Balloon(BalloonType.Simple, 1, 0), Balloon(BalloonType.Tough, 0, 1) },
                items: new[] { new ItemContentEntry(ItemType.Bomb, -2, 1, ContentWeightRamp.Flat(1f, 1f)) },
                gridActors: new[] { Actor(GridActorType.Puff, 0, 1) });

            var issues = settings.Validate("Test");

            Assert.That(issues, Has.Some.Matches<string>(m => m.Contains("Tough") && m.Contains("levels start at 1")));
            Assert.That(issues, Has.Some.Matches<string>(m => m.Contains("Bomb") && m.Contains("levels start at 1")));
            Assert.That(issues, Has.Some.Matches<string>(m => m.Contains("Puff") && m.Contains("levels start at 1")));
        }

        [Test]
        public void Validate_DuplicateTypes_ReportsEachCatalogue()
        {
            var settings = Settings(
                balloons: new[] { Balloon(BalloonType.Simple, 1, 0), Balloon(BalloonType.Simple, 4, 1) },
                items: new[]
                {
                    new ItemContentEntry(ItemType.Bomb, 1, 1, ContentWeightRamp.Flat(1f, 1f)),
                    new ItemContentEntry(ItemType.Bomb, 2, 1, ContentWeightRamp.Flat(1f, 1f)),
                },
                gridActors: new[] { Actor(GridActorType.Puff, 1, 1), Actor(GridActorType.Puff, 2, 1) });

            var issues = settings.Validate("Test");

            Assert.That(issues, Has.Some.Matches<string>(m => m.Contains("Simple") && m.Contains("twice")));
            Assert.That(issues, Has.Some.Matches<string>(m => m.Contains("Bomb") && m.Contains("twice")));
            Assert.That(issues, Has.Some.Matches<string>(m => m.Contains("Puff") && m.Contains("twice")));
        }

        private static BalloonContentEntry Balloon(BalloonType type, int unlock, int cost, ContentWeightRamp? weight = null)
        {
            return new BalloonContentEntry(type, ContentRole.Threat, unlock, cost, weight ?? ContentWeightRamp.Flat(1f, 1f));
        }

        private static GridActorContentEntry Actor(GridActorType type, int unlock, int cost)
        {
            return new GridActorContentEntry(type, unlock, cost, new RangedInt(1, 1), new RangedInt(1, 1), 0);
        }

        private static LevelGenerationSettings Settings(
            BudgetFormula? threat = null, BudgetFormula? aid = null,
            IntLevelStep[] colors = null, IntLevelStep[] boardLines = null, ItemCountStep[] itemCounts = null,
            BalloonContentEntry[] balloons = null, ItemContentEntry[] items = null,
            GridActorContentEntry[] gridActors = null, int seedSalt = 1, int featuredLevels = 2)
        {
            return new LevelGenerationSettings(
                threat ?? new BudgetFormula(2f, 1.5f, 12f),
                aid ?? new BudgetFormula(2f, 1.5f, 12f),
                colors ?? new[] { new IntLevelStep(1, 1) },
                boardLines ?? new[] { new IntLevelStep(1, 5) },
                itemCounts ?? new[] { new ItemCountStep(1, new AnimationCurve(), new AnimationCurve()) },
                balloons ?? new[] { Balloon(BalloonType.Simple, 1, 0) },
                items ?? Array.Empty<ItemContentEntry>(),
                gridActors ?? Array.Empty<GridActorContentEntry>(),
                seedSalt: seedSalt,
                featuredLevels: featuredLevels);
        }
    }
}

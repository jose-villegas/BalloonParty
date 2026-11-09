using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Slots.Actor.Archetype;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class LevelGeneratorTests
    {
        private const int SeedCount = 25;

        [Test]
        public void Generate_Levels1To50_ProduceAPlayableMix()
        {
            var settings = ShippedLevelCatalogue.Build();

            for (var level = 1; level <= 50; level++)
            {
                RangedLevelParameters result = null;
                Assert.DoesNotThrow(() => result = LevelGenerator.Generate(settings, level, settings.SeedForLevel(level)));

                Assert.IsNotEmpty(result.BalloonWeights, $"level {level}");
                Assert.IsTrue(result.BalloonWeights.Any(w => w.Weight > 0f), $"level {level} has nothing to spawn");
                Assert.AreEqual(result.BalloonWeights.Length, result.BalloonWeights.Select(w => w.Type).Distinct().Count());
                Assert.AreEqual(result.ItemWeights.Length, result.ItemWeights.Select(w => w.Type).Distinct().Count());
                Assert.AreNotEqual(0, result.AllowedColorsMask, $"level {level}");
                Assert.GreaterOrEqual(result.BoardLines, 1);
            }
        }

        [Test]
        public void Generate_OnlyIncludesPiecesUnlockedByTheLevel()
        {
            var settings = ShippedLevelCatalogue.Build();

            for (var level = 1; level <= 50; level++)
            {
                var result = LevelGenerator.Generate(settings, level, settings.SeedForLevel(level));

                foreach (var weight in result.BalloonWeights.Where(w => settings.Balloons.Any(b => b.Type == w.Type)))
                {
                    Assert.LessOrEqual(settings.Balloons.First(b => b.Type == weight.Type).UnlockLevel, level);
                }

                foreach (var weight in result.ItemWeights)
                {
                    Assert.LessOrEqual(settings.Items.First(i => i.Type == weight.Type).UnlockLevel, level);
                }

                foreach (var gate in result.GridActorGates)
                {
                    Assert.LessOrEqual(settings.GridActors.First(a => a.Type == gate.Type).UnlockLevel, level);
                }
            }
        }

        [Test]
        public void Generate_EveryCataloguePiece_IsInOnItsUnlockLevel_WhateverTheSeed()
        {
            var settings = ShippedLevelCatalogue.Build();

            for (var seed = 0; seed < SeedCount; seed++)
            {
                foreach (var entry in settings.Balloons)
                {
                    var result = LevelGenerator.Generate(settings, entry.UnlockLevel, seed);
                    Assert.IsTrue(result.BalloonWeights.Any(w => w.Type == entry.Type),
                        $"balloon {entry.Type} missing on its unlock level {entry.UnlockLevel}, seed {seed}");
                }

                foreach (var entry in settings.Items)
                {
                    var result = LevelGenerator.Generate(settings, entry.UnlockLevel, seed);
                    Assert.IsTrue(result.ItemWeights.Any(w => w.Type == entry.Type),
                        $"item {entry.Type} missing on its unlock level {entry.UnlockLevel}, seed {seed}");
                }

                foreach (var entry in settings.GridActors)
                {
                    var result = LevelGenerator.Generate(settings, entry.UnlockLevel, seed);
                    Assert.IsTrue(result.GridActorGates.Any(g => g.Type == entry.Type),
                        $"actor {entry.Type} missing on its unlock level {entry.UnlockLevel}, seed {seed}");
                }
            }
        }

        [Test]
        public void Generate_SameSeed_GivesTheSameMix()
        {
            var settings = ShippedLevelCatalogue.Build();

            for (var level = 1; level <= 30; level++)
            {
                var a = LevelGenerator.Generate(settings, level, 1234);
                var b = LevelGenerator.Generate(settings, level, 1234);

                Assert.AreEqual(Signature(a), Signature(b), $"level {level}");
            }
        }

        [Test]
        public void Generate_DifferentSeeds_CanGiveDifferentMixes()
        {
            var settings = ShippedLevelCatalogue.Build();

            var signatures = new HashSet<string>();
            for (var seed = 0; seed < SeedCount; seed++)
            {
                signatures.Add(Signature(LevelGenerator.Generate(settings, 20, seed)));
            }

            Assert.Greater(signatures.Count, 1);
        }

        [Test]
        public void Generate_Staples_AreInAtEveryLevelAndSeed_EvenWithNoBudget()
        {
            var settings = Settings(
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Rainbow, ContentRole.Aid, 3, 0),
                },
                items: new[] { Item(ItemType.Shield, 1, 0) },
                gridActors: new[] { Actor(GridActorType.Puff, 4, 0) });

            for (var level = 1; level <= 50; level++)
            {
                for (var seed = 0; seed < 5; seed++)
                {
                    var result = LevelGenerator.Generate(settings, level, seed);

                    Assert.IsTrue(result.BalloonWeights.Any(w => w.Type == BalloonType.Simple));
                    Assert.AreEqual(level >= 3, result.BalloonWeights.Any(w => w.Type == BalloonType.Rainbow));
                    Assert.IsTrue(result.ItemWeights.Any(w => w.Type == ItemType.Shield));
                    // A meadow's clear levels drop the puff clouds even when they're a staple.
                    var clouded = ScenarioSchedule.StretchAt(settings.Scenario, level).PuffCloudShare > 0f;
                    Assert.AreEqual(level >= 4 && clouded, result.GridActorGates.Any(g => g.Type == GridActorType.Puff));
                }
            }
        }

        [Test]
        public void Generate_Budget_CapsHowManyOptionalPiecesAreBought()
        {
            // Threat budget 2 over five cost-1 optional balloons: exactly two get bought, whatever the seed.
            var settings = Settings(
                threat: new BudgetFormula(2f, 0f, 1000f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Unbreakable, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Tougher, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.BubbleCluster, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.SimpleSilver, ContentRole.Threat, 1, 1),
                });

            for (var seed = 0; seed < SeedCount; seed++)
            {
                var result = LevelGenerator.Generate(settings, 3, seed);

                Assert.AreEqual(3, result.BalloonWeights.Length, $"seed {seed}: the staple plus two bought pieces");
            }
        }

        [Test]
        public void Generate_OptionalPieceCostingMoreThanTheBudget_IsNeverPicked()
        {
            var settings = Settings(
                threat: new BudgetFormula(2f, 0f, 1000f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 3),
                });

            for (var seed = 0; seed < SeedCount; seed++)
            {
                var result = LevelGenerator.Generate(settings, 3, seed);

                Assert.IsFalse(result.BalloonWeights.Any(w => w.Type == BalloonType.Tough));
            }
        }

        [Test]
        public void Generate_FeaturedPiece_IsInEvenOverBudget_AndSpendsIt()
        {
            var settings = Settings(
                threat: new BudgetFormula(2f, 0f, 1000f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Unbreakable, ContentRole.Threat, 5, 9),
                });

            for (var seed = 0; seed < SeedCount; seed++)
            {
                var result = LevelGenerator.Generate(settings, 5, seed);

                Assert.IsTrue(result.BalloonWeights.Any(w => w.Type == BalloonType.Unbreakable));
                Assert.IsFalse(result.BalloonWeights.Any(w => w.Type == BalloonType.Tough),
                    "the featured piece overspent the budget, so no optional piece is affordable");
            }
        }

        [Test]
        public void Generate_Featured_StaysForcedThroughUnlockPlusFeaturedMinusOne_ThenBecomesOptional()
        {
            // No budget at all: a piece is only ever in while it is featured.
            var settings = Settings(
                threat: new BudgetFormula(0f, 0f, 0f),
                featuredLevels: 3,
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 2, 1),
                });

            for (var seed = 0; seed < 5; seed++)
            {
                Assert.IsFalse(Has(LevelGenerator.Generate(settings, 1, seed), BalloonType.Tough), "before unlock");

                foreach (var level in new[] { 2, 3, 4 })
                {
                    Assert.IsTrue(Has(LevelGenerator.Generate(settings, level, seed), BalloonType.Tough),
                        $"level {level} is within the 3 featured levels");
                }

                Assert.IsFalse(Has(LevelGenerator.Generate(settings, 5, seed), BalloonType.Tough),
                    "no budget to buy it once the featured levels end");
            }
        }

        [Test]
        public void Generate_AfterFeatured_PieceCompetesForTheBudgetLikeAnyOther()
        {
            var settings = Settings(
                threat: new BudgetFormula(1f, 0f, 1000f),
                featuredLevels: 2,
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 2, 1),
                });

            for (var seed = 0; seed < 5; seed++)
            {
                Assert.IsTrue(Has(LevelGenerator.Generate(settings, 4, seed), BalloonType.Tough),
                    "the only optional piece fits the budget of 1");
            }
        }

        [Test]
        public void Generate_FeaturedItem_AndActor_AreForcedForTheirFeaturedLevelsToo()
        {
            var settings = Settings(
                threat: new BudgetFormula(0f, 0f, 0f),
                aid: new BudgetFormula(0f, 0f, 0f),
                featuredLevels: 2,
                items: new[] { Item(ItemType.Bomb, 3, 1) },
                gridActors: new[] { Actor(GridActorType.Puff, 3, 1) });

            Assert.IsTrue(LevelGenerator.Generate(settings, 4, 0).ItemWeights.Any(w => w.Type == ItemType.Bomb));
            Assert.IsTrue(LevelGenerator.Generate(settings, 4, 0).GridActorGates.Any(g => g.Type == GridActorType.Puff));
            Assert.IsFalse(LevelGenerator.Generate(settings, 5, 0).ItemWeights.Any());
            Assert.IsFalse(LevelGenerator.Generate(settings, 5, 0).GridActorGates.Any());
        }

        [Test]
        public void Generate_Budgets_AreSeparatePerRole_ThreatsCannotSpendTheAidBudget()
        {
            var settings = Settings(
                threat: new BudgetFormula(0f, 0f, 0f),
                aid: new BudgetFormula(5f, 0f, 5f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Rainbow, ContentRole.Aid, 1, 1),
                },
                items: new[] { Item(ItemType.Bomb, 1, 1) },
                gridActors: new[] { Actor(GridActorType.Puff, 1, 1) });

            for (var seed = 0; seed < SeedCount; seed++)
            {
                var result = LevelGenerator.Generate(settings, 5, seed);

                Assert.IsFalse(Has(result, BalloonType.Tough), "threat budget is 0");
                Assert.IsEmpty(result.GridActorGates, "threat budget is 0");
                Assert.IsTrue(Has(result, BalloonType.Rainbow), "aid budget covers it");
                Assert.IsTrue(result.ItemWeights.Any(w => w.Type == ItemType.Bomb), "aid budget covers it");
            }
        }

        [Test]
        public void Generate_Budgets_AreSeparatePerRole_AidsCannotSpendTheThreatBudget()
        {
            var settings = Settings(
                threat: new BudgetFormula(5f, 0f, 5f),
                aid: new BudgetFormula(0f, 0f, 0f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Rainbow, ContentRole.Aid, 1, 1),
                },
                items: new[] { Item(ItemType.Bomb, 1, 1) },
                gridActors: new[] { Actor(GridActorType.Puff, 1, 1) });

            for (var seed = 0; seed < SeedCount; seed++)
            {
                var result = LevelGenerator.Generate(settings, 5, seed);

                Assert.IsTrue(Has(result, BalloonType.Tough));
                Assert.AreEqual(1, result.GridActorGates.Length);
                Assert.IsFalse(Has(result, BalloonType.Rainbow), "aid budget is 0");
                Assert.IsEmpty(result.ItemWeights, "aid budget is 0");
            }
        }

        [Test]
        public void Generate_FeaturedAid_PaysFromTheAidBudgetOnly()
        {
            // The featured aid costs 3 against an aid budget of 0, but the threat budget of 1 is untouched.
            var settings = Settings(
                threat: new BudgetFormula(1f, 0f, 1f),
                aid: new BudgetFormula(0f, 0f, 0f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Rainbow, ContentRole.Aid, 5, 3),
                });

            for (var seed = 0; seed < SeedCount; seed++)
            {
                var result = LevelGenerator.Generate(settings, 5, seed);

                Assert.IsTrue(Has(result, BalloonType.Rainbow));
                Assert.IsTrue(Has(result, BalloonType.Tough));
            }
        }

        [Test]
        public void Generate_Picks_AreWeightedByCost_DearerPiecesWinMoreOften()
        {
            // Budget 2 fits either. The cost-2 piece is picked first with chance 2/3 (and then fills the
            // budget); the cost-1 piece first with chance 1/3, after which the cost-2 piece no longer fits.
            var settings = Settings(
                threat: new BudgetFormula(2f, 0f, 2f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Unbreakable, ContentRole.Threat, 1, 2),
                });

            var cheap = 0;
            var dear = 0;
            for (var seed = 0; seed < 300; seed++)
            {
                var result = LevelGenerator.Generate(settings, 4, seed);
                cheap += Has(result, BalloonType.Tough) ? 1 : 0;
                dear += Has(result, BalloonType.Unbreakable) ? 1 : 0;
            }

            Assert.Greater(dear, cheap);
            Assert.Greater(cheap, 0, "the cheap piece still wins sometimes");
        }

        [Test]
        public void Generate_PieceNotYetUnlocked_IsExcluded()
        {
            var settings = Settings(
                threat: new BudgetFormula(100f, 0f, 100f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 5, 1),
                });

            for (var seed = 0; seed < SeedCount; seed++)
            {
                Assert.IsFalse(Has(LevelGenerator.Generate(settings, 4, seed), BalloonType.Tough));
            }
        }

        [Test]
        public void Generate_PicksAreListedInCatalogueOrder()
        {
            var settings = Settings(
                threat: new BudgetFormula(100f, 0f, 100f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Unbreakable, ContentRole.Threat, 1, 1),
                });

            var result = LevelGenerator.Generate(settings, 5, 7);

            CollectionAssert.AreEqual(
                new[] { BalloonType.Simple, BalloonType.Tough, BalloonType.Unbreakable },
                result.BalloonWeights.Select(w => w.Type));
        }

        [Test]
        public void Generate_ColorMask_IsOneBitPerColour()
        {
            var settings = Settings(colors: new[] { new IntLevelStep(1, 1), new IntLevelStep(2, 3), new IntLevelStep(3, 31) });

            Assert.AreEqual(0b1, LevelGenerator.Generate(settings, 1, 0).AllowedColorsMask);
            Assert.AreEqual(0b111, LevelGenerator.Generate(settings, 2, 0).AllowedColorsMask);
            Assert.AreEqual(int.MaxValue, LevelGenerator.Generate(settings, 3, 0).AllowedColorsMask);
        }

        [Test]
        public void Generate_BoardLinesAndColours_FollowTheLevelSteps()
        {
            var settings = Settings(
                colors: new[] { new IntLevelStep(1, 1), new IntLevelStep(4, 2) },
                boardLines: new[] { new IntLevelStep(1, 8), new IntLevelStep(4, 5) });

            Assert.AreEqual(8, LevelGenerator.Generate(settings, 3, 0).BoardLines);
            Assert.AreEqual(5, LevelGenerator.Generate(settings, 4, 0).BoardLines);
            Assert.AreEqual(0b11, LevelGenerator.Generate(settings, 4, 0).AllowedColorsMask);
        }

        [Test]
        public void Generate_ItemCountCurves_ComeFromTheStepInForce()
        {
            var early = new AnimationCurve(new Keyframe(0f, 1f));
            var late = new AnimationCurve(new Keyframe(0f, 2f));
            var settings = Settings(itemCounts: new[] { new ItemCountStep(1, early, early), new ItemCountStep(6, late, late) });

            Assert.AreSame(early, LevelGenerator.Generate(settings, 5, 0).InitialItemCountWeights);
            Assert.AreSame(late, LevelGenerator.Generate(settings, 6, 0).ItemCountWeights);
        }

        [Test]
        public void Generate_ItemCadence_IsFixedToTheSetting()
        {
            var settings = Settings(itemCadence: 3);

            var cadence = LevelGenerator.Generate(settings, 1, 0).ItemCadence;

            Assert.AreEqual(3, cadence.Min);
            Assert.AreEqual(3, cadence.Max);
        }

        [Test]
        public void Generate_NoBalloonUnlockedYet_FallsBackToASingleSimple()
        {
            var settings = Settings(balloons: new[] { Balloon(BalloonType.Tough, ContentRole.Threat, 5, 0) });

            var result = LevelGenerator.Generate(settings, 1, 0);

            Assert.AreEqual(1, result.BalloonWeights.Length);
            Assert.AreEqual(BalloonType.Simple, result.BalloonWeights[0].Type);
            Assert.AreEqual(1f, result.BalloonWeights[0].Weight);
        }

        [Test]
        public void Generate_EmptyBalloonCatalogue_FallsBackToASingleSimple()
        {
            var settings = Settings(balloons: Array.Empty<BalloonContentEntry>());

            var result = LevelGenerator.Generate(settings, 10, 0);

            Assert.AreEqual(BalloonType.Simple, result.BalloonWeights.Single().Type);
        }

        [Test]
        public void Generate_RolledWeights_StayWithinTheEntryRange_AndFixedRangesAreExact()
        {
            var settings = Settings(
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0, ContentWeightRamp.Flat(2f, 2f)),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 0, ContentWeightRamp.Flat(1f, 3f)),
                },
                items: new[] { Item(ItemType.Bomb, 1, 0, ContentWeightRamp.Flat(0.5f, 1.5f)) });

            for (var seed = 0; seed < SeedCount; seed++)
            {
                var result = LevelGenerator.Generate(settings, 1, seed);

                Assert.AreEqual(2f, result.BalloonWeights[0].Weight);
                Assert.That(result.BalloonWeights[1].Weight, Is.InRange(1f, 3f));
                Assert.That(result.ItemWeights[0].Weight, Is.InRange(0.5f, 1.5f));
            }
        }

        [Test]
        public void Generate_IntroWeight_AppliesOnTheUnlockLevelOnly()
        {
            var settings = Settings(
                featuredLevels: 3,
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Rainbow, ContentRole.Aid, 2, 0, new ContentWeightRamp(0.1f, 0.2f, 0.1f, 0.2f, 0, 5f)),
                });

            Assert.AreEqual(5f, Weight(LevelGenerator.Generate(settings, 2, 0), BalloonType.Rainbow).Weight);
            Assert.That(Weight(LevelGenerator.Generate(settings, 3, 0), BalloonType.Rainbow).Weight, Is.InRange(0.1f, 0.2f));
        }

        [Test]
        public void Generate_WeightRamp_MovesWithTheLevel()
        {
            var settings = Settings(balloons: new[]
            {
                Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0, new ContentWeightRamp(1f, 1f, 3f, 3f, 4)),
            });

            Assert.AreEqual(1f, Weight(LevelGenerator.Generate(settings, 1, 0), BalloonType.Simple).Weight, 0.0001f);
            Assert.AreEqual(2f, Weight(LevelGenerator.Generate(settings, 3, 0), BalloonType.Simple).Weight, 0.0001f);
            Assert.AreEqual(3f, Weight(LevelGenerator.Generate(settings, 9, 0), BalloonType.Simple).Weight, 0.0001f);
        }

        [Test]
        public void Generate_IntroBalloon_GuaranteesItsIntroCountOnlyOnTheUnlockLevel()
        {
            var settings = Settings(featuredLevels: 3, balloons: new[]
            {
                Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                new BalloonContentEntry(BalloonType.Tough, ContentRole.Threat, 2, 0, ContentWeightRamp.Flat(1f, 1f),
                    introGuaranteedCount: 4, guaranteedInitialCount: 1),
            });

            Assert.AreEqual(4, Weight(LevelGenerator.Generate(settings, 2, 0), BalloonType.Tough).GuaranteedInitialCount);
            Assert.AreEqual(1, Weight(LevelGenerator.Generate(settings, 3, 0), BalloonType.Tough).GuaranteedInitialCount);
        }

        [Test]
        public void Generate_IntroItem_GuaranteeRaisesAnUndersizedMaximumOnTheUnlockLevelOnly()
        {
            var settings = Settings(items: new[]
            {
                new ItemContentEntry(ItemType.Shield, 1, 0, ContentWeightRamp.Flat(1f, 1f), maximumAllowedOverride: 6,
                    introGuaranteedCount: 12, guaranteedInitialCount: 2),
            });

            var intro = LevelGenerator.Generate(settings, 1, 0).ItemWeights[0];
            var later = LevelGenerator.Generate(settings, 2, 0).ItemWeights[0];

            Assert.AreEqual(12, intro.GuaranteedInitialCount);
            Assert.AreEqual(12, intro.MaximumAllowedOverride, "a cap of 6 would silently cut the 12 guaranteed");
            Assert.AreEqual(2, later.GuaranteedInitialCount);
            Assert.AreEqual(6, later.MaximumAllowedOverride);
        }

        [Test]
        public void Generate_IntroItem_RoomyMaximumIsKept_AndNoOverrideStaysZero()
        {
            var settings = Settings(items: new[]
            {
                new ItemContentEntry(ItemType.Shield, 1, 0, ContentWeightRamp.Flat(1f, 1f), maximumAllowedOverride: 20,
                    introGuaranteedCount: 12),
                new ItemContentEntry(ItemType.Bomb, 1, 0, ContentWeightRamp.Flat(1f, 1f), maximumAllowedOverride: 0,
                    introGuaranteedCount: 3),
            });

            var result = LevelGenerator.Generate(settings, 1, 0);

            Assert.AreEqual(20, result.ItemWeights[0].MaximumAllowedOverride);
            Assert.AreEqual(0, result.ItemWeights[1].MaximumAllowedOverride, "0 defers to the item catalogue");
        }

        [Test]
        public void Generate_GridActor_CarriesItsRampedCountAndClusterCap()
        {
            var settings = Settings(gridActors: new[]
            {
                new GridActorContentEntry(GridActorType.Puff, 1, 0, new RangedInt(6, 12), new RangedInt(6, 12), 0, 5),
            });

            var gate = LevelGenerator.Generate(settings, 1, 0).GridActorGates.Single();

            Assert.AreEqual(GridActorType.Puff, gate.Type);
            Assert.AreEqual(6, gate.Count.Min);
            Assert.AreEqual(12, gate.Count.Max);
            Assert.AreEqual(5, gate.MaxPerCluster);
        }

        [Test]
        public void Generate_WithheldPieces_NeverAppear_EvenAsStaplesOrFeatured()
        {
            // Staples (cost 0) and featured pieces skip the budget, so a missed check there would leak them in.
            var settings = Settings(featuredLevels: 3, balloons: new[]
            {
                Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                new BalloonContentEntry(BalloonType.BubbleCluster, ContentRole.Threat, 1, 0,
                    ContentWeightRamp.Flat(1f, 1f), withheld: true),
            }, items: new[]
            {
                new ItemContentEntry(ItemType.Shield, 1, 0, ContentWeightRamp.Flat(1f, 1f), withheld: true),
            }, gridActors: new[]
            {
                new GridActorContentEntry(GridActorType.Bush, 1, 0, new RangedInt(1, 1), new RangedInt(1, 1), 0,
                    withheld: true),
            });

            for (var level = 1; level <= 10; level++)
            {
                var result = LevelGenerator.Generate(settings, level, level);

                Assert.IsTrue(Has(result, BalloonType.Simple), $"level {level}");
                Assert.IsFalse(Has(result, BalloonType.BubbleCluster), $"level {level}");
                Assert.IsEmpty(result.ItemWeights, $"level {level}");
                Assert.IsEmpty(result.GridActorGates, $"level {level}");
            }
        }

        [Test]
        public void Generate_TheFirstMeadow_ThinsItsPuffCloudsThenClears([Values(0, 1, 7)] int seed)
        {
            // The pinned schedule (see Settings): levels 16-19 are the first ground stretch, fixed at 4 levels.
            var settings = Settings(gridActors: new[]
            {
                new GridActorContentEntry(GridActorType.Puff, 1, 0, new RangedInt(6, 12), new RangedInt(6, 12), 0, 5),
                new GridActorContentEntry(GridActorType.Bush, 1, 0, new RangedInt(3, 6), new RangedInt(3, 6), 0, 3),
            });

            var sky = LevelGenerator.Generate(settings, 15, seed).GridActorGates.Single(g => g.Type == GridActorType.Puff);
            var full = LevelGenerator.Generate(settings, 16, seed).GridActorGates.Single(g => g.Type == GridActorType.Puff);
            var half = LevelGenerator.Generate(settings, 17, seed).GridActorGates.Single(g => g.Type == GridActorType.Puff);

            Assert.AreEqual((6, 12), (sky.Count.Min, sky.Count.Max));
            Assert.AreEqual((6, 12), (full.Count.Min, full.Count.Max));
            Assert.AreEqual((3, 6), (half.Count.Min, half.Count.Max));
            Assert.AreEqual(5, half.MaxPerCluster);
            for (var level = 18; level <= 19; level++)
            {
                var gates = LevelGenerator.Generate(settings, level, seed).GridActorGates;
                Assert.IsFalse(gates.Any(g => g.Type == GridActorType.Puff), $"level {level} is clear");
                Assert.IsTrue(gates.Any(g => g.Type == GridActorType.Bush), $"level {level} keeps its other actors");
            }
        }

        [Test]
        public void Generate_TheFirstMeadow_GrowsItsBushesAsTheCloudsGo([Values(0, 1, 7)] int seed)
        {
            var settings = Settings(gridActors: new[]
            {
                new GridActorContentEntry(GridActorType.Puff, 1, 0, new RangedInt(8, 8), new RangedInt(8, 8), 0),
                new GridActorContentEntry(GridActorType.Bush, 1, 0, new RangedInt(4, 8), new RangedInt(4, 8), 0, 3),
            });

            var bushes = new (int, int)[6];
            for (var i = 0; i < bushes.Length; i++)
            {
                var gate = LevelGenerator.Generate(settings, 15 + i, seed).GridActorGates
                    .Single(g => g.Type == GridActorType.Bush);
                bushes[i] = (gate.Count.Min, gate.Count.Max);
            }

            // Sky 15, then the meadow 16-19 at half, three quarters, full, full; sky again at 20.
            CollectionAssert.AreEqual(new[] { (4, 8), (2, 4), (3, 6), (4, 8), (4, 8), (4, 8) }, bushes);
        }

        [Test]
        public void Generate_AClearingMeadow_BuysItsBushesWithTheCloudsBudget([Values(0, 1, 7)] int seed)
        {
            // Bushes too dear for a zero threat budget and past their featured levels: only the trade puts them in.
            var settings = Settings(gridActors: new[]
            {
                new GridActorContentEntry(GridActorType.Puff, 1, 0, new RangedInt(8, 8), new RangedInt(8, 8), 0),
                new GridActorContentEntry(GridActorType.Bush, 2, 5, new RangedInt(4, 8), new RangedInt(4, 8), 0, 3),
            });

            bool HasBush(int level)
            {
                return LevelGenerator.Generate(settings, level, seed).GridActorGates.Any(g => g.Type == GridActorType.Bush);
            }

            Assert.IsFalse(HasBush(15), "sky");
            Assert.IsFalse(HasBush(16), "the meadow's first level is still under full clouds");
            Assert.IsTrue(HasBush(17), "half clouds");
            Assert.IsTrue(HasBush(18), "clear");
            Assert.IsTrue(HasBush(19), "clear");
            Assert.IsFalse(HasBush(20), "sky");
        }

        [Test]
        public void Generate_TheBushTrade_LeavesTheRestOfTheMixAlone()
        {
            var settings = Settings(
                threat: new BudgetFormula(3f, 0f, 3f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 1),
                    Balloon(BalloonType.Rainbow, ContentRole.Threat, 1, 2),
                },
                gridActors: new[]
                {
                    new GridActorContentEntry(GridActorType.Puff, 1, 1, new RangedInt(8, 8), new RangedInt(8, 8), 0),
                    new GridActorContentEntry(GridActorType.Bush, 2, 5, new RangedInt(4, 8), new RangedInt(4, 8), 0),
                });
            var sky = Settings(
                threat: new BudgetFormula(3f, 0f, 3f),
                balloons: settings.Balloons.ToArray(),
                gridActors: settings.GridActors.ToArray());
            SetScenario(sky, new ScenarioScheduleSettings(500, 4, 3, 5, 2, 4, 7919));

            for (var seed = 0; seed < 20; seed++)
            {
                var meadow = LevelGenerator.Generate(settings, 18, seed);
                var open = LevelGenerator.Generate(sky, 18, seed);
                CollectionAssert.AreEqual(open.BalloonWeights.Select(w => (w.Type, w.Weight)),
                    meadow.BalloonWeights.Select(w => (w.Type, w.Weight)), $"seed {seed}");
            }
        }

        [Test]
        public void Generate_HalvedPuffClouds_KeepAtLeastOneSlot()
        {
            var settings = Settings(gridActors: new[]
            {
                new GridActorContentEntry(GridActorType.Puff, 1, 0, new RangedInt(1, 1), new RangedInt(1, 1), 0),
            });

            var gate = LevelGenerator.Generate(settings, 17, 0).GridActorGates.Single();

            Assert.AreEqual((1, 1), (gate.Count.Min, gate.Count.Max));
        }

        [Test]
        public void Generate_EveryMeadowLevel_MatchesItsStretchsCloudShare()
        {
            var settings = Settings(gridActors: new[]
            {
                new GridActorContentEntry(GridActorType.Puff, 1, 0, new RangedInt(8, 8), new RangedInt(8, 8), 0),
            });

            for (var level = 16; level < 200; level++)
            {
                var share = ScenarioSchedule.StretchAt(settings.Scenario, level).PuffCloudShare;
                var puff = LevelGenerator.Generate(settings, level, level).GridActorGates
                    .Where(g => g.Type == GridActorType.Puff).Select(g => g.Count.Max).DefaultIfEmpty(0).Single();
                Assert.AreEqual((int)(8 * share), puff, $"level {level}");
            }
        }

        [Test]
        public void Generate_AGroundOnlyActor_AppearsOnGroundLevelsAndNeverInTheSky()
        {
            var settings = Settings(gridActors: new[]
            {
                new GridActorContentEntry(GridActorType.Puff, 1, 0, new RangedInt(8, 8), new RangedInt(8, 8), 0),
                new GridActorContentEntry(GridActorType.Bush, 1, 0, new RangedInt(4, 8), new RangedInt(4, 8), 0, 3,
                    requiresGround: true),
            });

            for (var level = 1; level < 200; level++)
            {
                var gates = LevelGenerator.Generate(settings, level, level).GridActorGates;
                var onGround = ScenarioSchedule.ForLevel(settings.Scenario, level) == ScenarioKind.Ground;
                Assert.AreEqual(onGround, gates.Any(g => g.Type == GridActorType.Bush), $"level {level}");
                if (!onGround)
                {
                    Assert.IsTrue(gates.Any(g => g.Type == GridActorType.Puff), $"sky level {level} keeps its clouds");
                }
            }
        }

        [Test]
        public void Generate_AGroundOnlyActor_LeavesASkyLevelsBudgetToTheRest([Values(0, 1, 7, 42)] int seed)
        {
            // Bush and Tough each cost the whole budget: on a sky level the bush mustn't be a pick that comes out
            // empty, so the budget always goes to Tough.
            var settings = Settings(
                threat: new BudgetFormula(2f, 0f, 2f),
                balloons: new[]
                {
                    Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0),
                    Balloon(BalloonType.Tough, ContentRole.Threat, 1, 2),
                },
                gridActors: new[]
                {
                    new GridActorContentEntry(GridActorType.Bush, 1, 2, new RangedInt(4, 8), new RangedInt(4, 8), 0,
                        requiresGround: true),
                });

            var sky = LevelGenerator.Generate(settings, 15, seed);

            Assert.IsTrue(Has(sky, BalloonType.Tough));
            Assert.IsEmpty(sky.GridActorGates);
        }

        private static void SetScenario(LevelGenerationSettings settings, ScenarioScheduleSettings scenario)
        {
            typeof(LevelGenerationSettings)
                .GetField("_scenario", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(settings, scenario);
        }

        private static bool Has(RangedLevelParameters parameters, BalloonType type)
        {
            return parameters.BalloonWeights.Any(w => w.Type == type);
        }

        private static BalloonTypeWeight Weight(RangedLevelParameters parameters, BalloonType type)
        {
            return parameters.BalloonWeights.First(w => w.Type == type);
        }

        private static string Signature(RangedLevelParameters p)
        {
            return string.Join("|",
                string.Join(",", p.BalloonWeights.Select(w => $"{w.Type}:{w.Weight}:{w.GuaranteedInitialCount}")),
                string.Join(",", p.ItemWeights.Select(w => $"{w.Type}:{w.Weight}:{w.MaximumAllowedOverride}")),
                string.Join(",", p.GridActorGates.Select(g => $"{g.Type}:{g.Count.Min}-{g.Count.Max}")),
                p.BoardLines,
                p.AllowedColorsMask);
        }

        private static BalloonContentEntry Balloon(
            BalloonType type, ContentRole role, int unlock, int cost, ContentWeightRamp? weight = null)
        {
            return new BalloonContentEntry(type, role, unlock, cost, weight ?? ContentWeightRamp.Flat(1f, 1f));
        }

        private static ItemContentEntry Item(ItemType type, int unlock, int cost, ContentWeightRamp? weight = null)
        {
            return new ItemContentEntry(type, unlock, cost, weight ?? ContentWeightRamp.Flat(1f, 1f));
        }

        private static GridActorContentEntry Actor(GridActorType type, int unlock, int cost)
        {
            return new GridActorContentEntry(type, unlock, cost, new RangedInt(1, 1), new RangedInt(1, 1), 0);
        }

        // Featured defaults to 1 (the unlock level only) and both budgets to 0 unless a test sets them. The first
        // meadow is pinned to levels 16-19, so the scenario tests don't follow the shipped roll.
        private static LevelGenerationSettings Settings(
            BudgetFormula? threat = null, BudgetFormula? aid = null,
            IntLevelStep[] colors = null, IntLevelStep[] boardLines = null, ItemCountStep[] itemCounts = null,
            BalloonContentEntry[] balloons = null, ItemContentEntry[] items = null,
            GridActorContentEntry[] gridActors = null, int itemCadence = 1, int featuredLevels = 1)
        {
            var settings = new LevelGenerationSettings(
                threat ?? new BudgetFormula(0f, 0f, 0f),
                aid ?? new BudgetFormula(0f, 0f, 0f),
                colors ?? new[] { new IntLevelStep(1, 1) },
                boardLines ?? new[] { new IntLevelStep(1, 5) },
                itemCounts ?? new[] { new ItemCountStep(1, new AnimationCurve(), new AnimationCurve()) },
                balloons ?? new[] { Balloon(BalloonType.Simple, ContentRole.Threat, 1, 0) },
                items ?? Array.Empty<ItemContentEntry>(),
                gridActors ?? Array.Empty<GridActorContentEntry>(),
                itemCadence,
                featuredLevels: featuredLevels);
            SetScenario(settings, new ScenarioScheduleSettings(16, 4, 3, 5, 2, 4, 7919));
            return settings;
        }
    }
}

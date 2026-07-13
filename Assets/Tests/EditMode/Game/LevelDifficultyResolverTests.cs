using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor.Archetype;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class LevelDifficultyResolverTests
    {
        private ILevelPacingConfiguration _pacing;
        private IBalloonsConfiguration _balloonsConfig;
        private IItemConfiguration _itemConfig;
        private IGamePalette _palette;
        private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private IMessageHandler<ScoreLevelUpMessage> _levelUpHandler;
        private readonly List<GameObject> _prefabObjects = new();

        [SetUp]
        public void SetUp()
        {
            _pacing = Substitute.For<ILevelPacingConfiguration>();
            _pacing.ThresholdModifier(Arg.Any<int>()).Returns(1f);
            // Identity rounding by default — tests that care about snapping stub it themselves.
            _pacing.RoundThreshold(Arg.Any<int>()).Returns(ci => ci.Arg<int>());

            _balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            _itemConfig = Substitute.For<IItemConfiguration>();
            _itemConfig.Items.Returns(new List<ItemSettings>());

            _palette = Substitute.For<IGamePalette>();
            _palette.ColorNamesForMask(Arg.Any<int>()).Returns(new List<string>());

            _levelUpSubscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            _levelUpSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreLevelUpMessage>>(h => _levelUpHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _prefabObjects)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            _prefabObjects.Clear();
        }

        [Test]
        public void AllowedColors_UsesPaletteConversionOfResolvedMask()
        {
            var expected = new List<string> { "Red", "Blue" };
            _palette.ColorNamesForMask(~0).Returns(expected);
            SetSingleRange(1, 0, BalloonType.Simple, 1f);

            var resolver = BuildResolver();
            resolver.Start();

            Assert.AreSame(expected, resolver.Current.AllowedColors);
        }

        [Test]
        public void PointsRequiredForLevel_ScalesWithThresholdModifier()
        {
            // The base curve is private math now; assert the resolver composes it with the modifier by
            // checking the result scales with the modifier (doubling it doubles the requirement) rather
            // than mocking a base value.
            SetSingleRange(1, 0, BalloonType.Simple, 1f);

            _pacing.ThresholdModifier(5).Returns(1f);
            var baseline = BuildResolver().PointsRequiredForLevel(5);

            _pacing.ThresholdModifier(5).Returns(2f);
            var doubled = BuildResolver().PointsRequiredForLevel(5);

            Assert.Greater(baseline, 0);
            Assert.AreEqual(baseline * 2, doubled);
        }

        [Test]
        public void PickBalloonEntry_TypeAbsentFromRange_NeverPicked()
        {
            var simple = CreateCatalogEntry("Simple", BalloonType.Simple, weight: 1f, maxCount: 0);
            var tough = CreateCatalogEntry("Tough", BalloonType.Tough, weight: 1f, maxCount: 0);
            _balloonsConfig.Entries.Returns(new[] { simple, tough });

            SetSingleRange(1, 0, BalloonType.Simple, 1f);
            var resolver = BuildResolver();
            resolver.Start();

            var activeCounts = new Dictionary<string, int>();
            for (var i = 0; i < 50; i++)
            {
                var picked = resolver.Current.PickBalloonEntry(activeCounts);
                Assert.AreSame(simple, picked, "Tough is absent from the range's weighted set and must never be picked.");
            }
        }

        [Test]
        public void PickBalloonEntry_MaxCountOverride_ExcludesAtCap()
        {
            var entry = CreateCatalogEntry("Simple", BalloonType.Simple, weight: 1f, maxCount: 0);
            _balloonsConfig.Entries.Returns(new[] { entry });

            SetSingleRange(1, 0, BalloonType.Simple, 1f, maxCountOverride: 2);
            var resolver = BuildResolver();
            resolver.Start();

            var activeCounts = new Dictionary<string, int> { [entry.PoolKey] = 2 };

            Assert.IsNull(resolver.Current.PickBalloonEntry(activeCounts));
        }

        [Test]
        public void ResolveFor_ReResolvesOnLevelUpMessage()
        {
            var early = CreateCatalogEntry("Simple", BalloonType.Simple, weight: 1f, maxCount: 0);
            var late = CreateCatalogEntry("Tough", BalloonType.Tough, weight: 1f, maxCount: 0);
            _balloonsConfig.Entries.Returns(new[] { early, late });

            var earlyRange = MakeRange(1, 4, new RangedInt(1, 1), new[] { new BalloonTypeWeight(BalloonType.Simple, 1f) });
            var lateRange = MakeRange(5, 0, new RangedInt(3, 3), new[] { new BalloonTypeWeight(BalloonType.Tough, 1f) });
            _pacing.Ranges.Returns(new[] { earlyRange, lateRange });

            var resolver = BuildResolver();
            resolver.Start();

            Assert.AreEqual(1, resolver.Current.SpawnLines);
            Assert.AreSame(early, resolver.Current.PickBalloonEntry(new Dictionary<string, int>()));

            _levelUpHandler.Handle(new ScoreLevelUpMessage(5));

            Assert.AreEqual(3, resolver.Current.SpawnLines);
            Assert.AreSame(late, resolver.Current.PickBalloonEntry(new Dictionary<string, int>()));
        }

        [Test]
        public void ResetRun_ReResolvesLevelOne()
        {
            var entry = CreateCatalogEntry("Simple", BalloonType.Simple, weight: 1f, maxCount: 0);
            _balloonsConfig.Entries.Returns(new[] { entry });

            var earlyRange = MakeRange(1, 4, new RangedInt(1, 1), new[] { new BalloonTypeWeight(BalloonType.Simple, 1f) });
            var lateRange = MakeRange(5, 0, new RangedInt(9, 9), new[] { new BalloonTypeWeight(BalloonType.Simple, 1f) });
            _pacing.Ranges.Returns(new[] { earlyRange, lateRange });

            var resolver = BuildResolver();
            resolver.Start();
            _levelUpHandler.Handle(new ScoreLevelUpMessage(5));
            Assert.AreEqual(9, resolver.Current.SpawnLines);

            resolver.ResetRun(2);

            Assert.AreEqual(1, resolver.Current.SpawnLines);
        }

        [Test]
        public void TryGetGridActorGate_TypeInGate_ReturnsResolvedCount()
        {
            var range = MakeRange(1, 0, new RangedInt(1, 1), new[] { new BalloonTypeWeight(BalloonType.Simple, 1f) });
            SetField(range.Parameters, "_gridActorGates", new[] { new GridActorTypeGate(GridActorType.Puff, new RangedInt(5, 5)) });
            _pacing.Ranges.Returns(new[] { range });

            var resolver = BuildResolver();
            resolver.Start();

            Assert.IsTrue(resolver.Current.TryGetGridActorGate(GridActorType.Puff, out var gate));
            Assert.AreEqual(5, gate.Count);
        }

        [Test]
        public void TryGetGridActorGate_TypeAbsentFromGate_ReturnsFalse()
        {
            var range = MakeRange(1, 0, new RangedInt(1, 1), new[] { new BalloonTypeWeight(BalloonType.Simple, 1f) });
            SetField(range.Parameters, "_gridActorGates", new[] { new GridActorTypeGate(GridActorType.Puff, new RangedInt(5, 5)) });
            _pacing.Ranges.Returns(new[] { range });

            var resolver = BuildResolver();
            resolver.Start();

            Assert.IsFalse(resolver.Current.TryGetGridActorGate(GridActorType.Bush, out _));
        }

        [Test]
        public void ItemCadence_ReturnsResolvedValue()
        {
            var range = MakeRangeWithItems(1, 0, new RangedInt(4, 4), Array.Empty<ItemTypeWeight>());
            _pacing.Ranges.Returns(new[] { range });

            var resolver = BuildResolver();
            resolver.Start();

            Assert.AreEqual(4, resolver.Current.ItemCadence);
        }

        [Test]
        public void Items_OnlyIncludesTypeGatedInEntries()
        {
            var bomb = CreateCatalogItem(ItemType.Bomb);
            var shield = CreateCatalogItem(ItemType.Shield);
            _itemConfig.Items.Returns(new List<ItemSettings> { bomb, shield });

            var range = MakeRangeWithItems(1, 0, new RangedInt(1, 1), new[] { new ItemTypeWeight(ItemType.Bomb, 1f) });
            _pacing.Ranges.Returns(new[] { range });

            var resolver = BuildResolver();
            resolver.Start();

            CollectionAssert.AreEqual(new[] { bomb }, resolver.Current.Items);
        }

        [Test]
        public void PickItemEntry_TypeAbsentFromRange_NeverPicked()
        {
            var bomb = CreateCatalogItem(ItemType.Bomb);
            var shield = CreateCatalogItem(ItemType.Shield);
            _itemConfig.Items.Returns(new List<ItemSettings> { bomb, shield });

            var range = MakeRangeWithItems(1, 0, new RangedInt(1, 1), new[] { new ItemTypeWeight(ItemType.Bomb, 1f) });
            _pacing.Ranges.Returns(new[] { range });

            var resolver = BuildResolver();
            resolver.Start();

            var activeCounts = new Dictionary<string, int>();
            for (var i = 0; i < 50; i++)
            {
                var picked = resolver.Current.PickItemEntry(activeCounts);
                Assert.AreSame(bomb, picked, "Shield is absent from the range's weighted set and must never be picked.");
            }
        }

        [Test]
        public void PickItemEntry_MaximumAllowedOverride_ExcludesAtCap()
        {
            var bomb = CreateCatalogItem(ItemType.Bomb, maximumAllowed: 0);
            _itemConfig.Items.Returns(new List<ItemSettings> { bomb });

            var range = MakeRangeWithItems(
                1, 0, new RangedInt(1, 1),
                new[] { new ItemTypeWeight(ItemType.Bomb, 1f, maximumAllowedOverride: 2) });
            _pacing.Ranges.Returns(new[] { range });

            var resolver = BuildResolver();
            resolver.Start();

            var activeCounts = new Dictionary<string, int> { [ItemType.Bomb.ToString()] = 2 };

            Assert.IsNull(resolver.Current.PickItemEntry(activeCounts));
        }

        [Test]
        public void ExhaustiveResolve_Levels1To50_NeverThrows()
        {
            var entry = CreateCatalogEntry("Simple", BalloonType.Simple, weight: 1f, maxCount: 0);
            _balloonsConfig.Entries.Returns(new[] { entry });
            SetSingleRange(1, 0, BalloonType.Simple, 1f, spawnLinesMode: RangeMode.Random, spawnLinesMax: 5);

            var resolver = BuildResolver();
            resolver.Start();

            for (var level = 1; level <= 50; level++)
            {
                Assert.DoesNotThrow(() => _levelUpHandler.Handle(new ScoreLevelUpMessage(level)));
                Assert.IsNotNull(resolver.Current.PickBalloonEntry(new Dictionary<string, int>()));
            }
        }

        private LevelDifficultyResolver BuildResolver()
        {
            return new LevelDifficultyResolver(_pacing, _balloonsConfig, _itemConfig, _palette, _levelUpSubscriber);
        }

        private void SetSingleRange(
            int fromLevel, int toLevel, BalloonType type, float weight,
            int maxCountOverride = 0, RangeMode spawnLinesMode = RangeMode.Fixed, int spawnLinesMax = 1)
        {
            var range = MakeRange(
                fromLevel, toLevel,
                new RangedInt(1, spawnLinesMax, spawnLinesMode),
                new[] { new BalloonTypeWeight(type, weight, maxCountOverride) });
            _pacing.Ranges.Returns(new[] { range });
        }

        private static LevelRangeEntry MakeRange(int fromLevel, int toLevel, RangedInt spawnLines, BalloonTypeWeight[] weights)
        {
            var parameters = new RangedLevelParameters();
            SetField(parameters, "_spawnLines", spawnLines);
            SetField(parameters, "_balloonWeights", weights);
            return new LevelRangeEntry(fromLevel, toLevel, parameters);
        }

        private static LevelRangeEntry MakeRangeWithItems(
            int fromLevel, int toLevel, RangedInt itemCadence, ItemTypeWeight[] itemWeights)
        {
            var parameters = new RangedLevelParameters();
            SetField(parameters, "_itemCadence", itemCadence);
            SetField(parameters, "_itemWeights", itemWeights);
            return new LevelRangeEntry(fromLevel, toLevel, parameters);
        }

        private static ItemSettings CreateCatalogItem(ItemType type, float weight = 1f, int maximumAllowed = 0)
        {
            var item = new ItemSettings();
            SetField(item, "_type", type);
            SetField(item, "_maximumAllowed", maximumAllowed);
            return item;
        }

        private BalloonPrefabEntry CreateCatalogEntry(string name, BalloonType type, float weight, int maxCount)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var view = go.AddComponent<BalloonView>();
            _prefabObjects.Add(go);

            var entry = new BalloonPrefabEntry();
            SetField(entry, "_prefab", view);
            SetField(entry, "_balloonType", type);
            return entry;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}

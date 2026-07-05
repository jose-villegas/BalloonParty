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

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class LevelDifficultyResolverTests
    {
        private ILevelPacingConfiguration _pacing;
        private IBalloonsConfiguration _balloonsConfig;
        private IItemConfiguration _itemConfig;
        private IGameConfiguration _gameConfig;
        private IGamePalette _palette;
        private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private IMessageHandler<ScoreLevelUpMessage> _levelUpHandler;
        private readonly List<GameObject> _prefabObjects = new();

        [SetUp]
        public void SetUp()
        {
            _pacing = Substitute.For<ILevelPacingConfiguration>();
            _pacing.ThresholdModifier(Arg.Any<int>()).Returns(1f);

            _balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            _itemConfig = Substitute.For<IItemConfiguration>();
            _itemConfig.Items.Returns(new List<ItemSettings>());

            _gameConfig = Substitute.For<IGameConfiguration>();
            _gameConfig.PointsRequiredForLevel(Arg.Any<int>()).Returns(100);

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

            Assert.AreSame(expected, resolver.AllowedColors);
        }

        [Test]
        public void PointsRequiredForLevel_ComposesFormulaWithModifier()
        {
            _pacing.ThresholdModifier(5).Returns(0.5f);
            SetSingleRange(1, 0, BalloonType.Simple, 1f);
            var resolver = BuildResolver();

            Assert.AreEqual(50, resolver.PointsRequiredForLevel(5));
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
                var picked = resolver.PickBalloonEntry(activeCounts);
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

            Assert.IsNull(resolver.PickBalloonEntry(activeCounts));
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

            Assert.AreEqual(1, resolver.SpawnLines);
            Assert.AreSame(early, resolver.PickBalloonEntry(new Dictionary<string, int>()));

            _levelUpHandler.Handle(new ScoreLevelUpMessage(5));

            Assert.AreEqual(3, resolver.SpawnLines);
            Assert.AreSame(late, resolver.PickBalloonEntry(new Dictionary<string, int>()));
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
            Assert.AreEqual(9, resolver.SpawnLines);

            resolver.ResetRun(2);

            Assert.AreEqual(1, resolver.SpawnLines);
        }

        [Test]
        public void TryGetGridActorCount_TypeInGate_ReturnsResolvedCount()
        {
            var range = MakeRange(1, 0, new RangedInt(1, 1), new[] { new BalloonTypeWeight(BalloonType.Simple, 1f) });
            SetField(range.Parameters, "_gridActorGates", new[] { new GridActorTypeGate(GridActorType.Puff, new RangedInt(5, 5)) });
            _pacing.Ranges.Returns(new[] { range });

            var resolver = BuildResolver();
            resolver.Start();

            Assert.IsTrue(resolver.TryGetGridActorCount(GridActorType.Puff, out var count));
            Assert.AreEqual(5, count);
        }

        [Test]
        public void TryGetGridActorCount_TypeAbsentFromGate_ReturnsFalse()
        {
            var range = MakeRange(1, 0, new RangedInt(1, 1), new[] { new BalloonTypeWeight(BalloonType.Simple, 1f) });
            SetField(range.Parameters, "_gridActorGates", new[] { new GridActorTypeGate(GridActorType.Puff, new RangedInt(5, 5)) });
            _pacing.Ranges.Returns(new[] { range });

            var resolver = BuildResolver();
            resolver.Start();

            Assert.IsFalse(resolver.TryGetGridActorCount(GridActorType.Bush, out _));
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
                Assert.IsNotNull(resolver.PickBalloonEntry(new Dictionary<string, int>()));
            }
        }

        private LevelDifficultyResolver BuildResolver()
        {
            return new LevelDifficultyResolver(_pacing, _balloonsConfig, _itemConfig, _gameConfig, _palette, _levelUpSubscriber);
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

        private BalloonPrefabEntry CreateCatalogEntry(string name, BalloonType type, float weight, int maxCount)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var view = go.AddComponent<BalloonView>();
            _prefabObjects.Add(go);

            var entry = new BalloonPrefabEntry();
            SetField(entry, "_prefab", view);
            SetField(entry, "_balloonType", type);
            SetField(entry, "_weight", weight);
            SetField(entry, "_maxCount", maxCount);
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

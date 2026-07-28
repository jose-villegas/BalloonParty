using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class LevelParametersGuaranteedFillTests
    {
        private readonly List<GameObject> _prefabObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _prefabObjects)
            {
                Object.DestroyImmediate(go);
            }

            _prefabObjects.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // FillGuaranteedBalloons
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void FillGuaranteedBalloons_ZeroGuaranteed_AddsNothing()
        {
            var entry = CreateResolvedBalloonEntry("Plain", guaranteed: 0, maxCount: 0);
            var parameters = CreateLevelParametersWithBalloons(new[] { entry });

            var output = new List<BalloonPrefabEntry>();
            var activeCounts = new Dictionary<string, int>();

            parameters.FillGuaranteedBalloons(output, activeCounts);

            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void FillGuaranteedBalloons_GuaranteedCount_AddsThatMany()
        {
            var entry = CreateResolvedBalloonEntry("Tough", guaranteed: 3, maxCount: 0);
            var parameters = CreateLevelParametersWithBalloons(new[] { entry });

            var output = new List<BalloonPrefabEntry>();
            var activeCounts = new Dictionary<string, int>();

            parameters.FillGuaranteedBalloons(output, activeCounts);

            Assert.AreEqual(3, output.Count);
            Assert.AreEqual(3, activeCounts["Tough"]);
        }

        [Test]
        public void FillGuaranteedBalloons_MaxCountCaps_AddsCappedAmount()
        {
            // guaranteed=5 but maxCount=3 → only 3 added
            var entry = CreateResolvedBalloonEntry("Tough", guaranteed: 5, maxCount: 3);
            var parameters = CreateLevelParametersWithBalloons(new[] { entry });

            var output = new List<BalloonPrefabEntry>();
            var activeCounts = new Dictionary<string, int>();

            parameters.FillGuaranteedBalloons(output, activeCounts);

            Assert.AreEqual(3, output.Count);
            Assert.AreEqual(3, activeCounts["Tough"]);
        }

        [Test]
        public void FillGuaranteedBalloons_ActiveCountsReduceHeadroom()
        {
            // guaranteed=4, maxCount=5, already 3 active → only 2 more
            var entry = CreateResolvedBalloonEntry("Tough", guaranteed: 4, maxCount: 5);
            var parameters = CreateLevelParametersWithBalloons(new[] { entry });

            var output = new List<BalloonPrefabEntry>();
            var activeCounts = new Dictionary<string, int> { ["Tough"] = 3 };

            parameters.FillGuaranteedBalloons(output, activeCounts);

            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(5, activeCounts["Tough"]);
        }

        [Test]
        public void FillGuaranteedBalloons_ActiveCountsAlreadyAtMax_AddsNothing()
        {
            var entry = CreateResolvedBalloonEntry("Tough", guaranteed: 3, maxCount: 2);
            var parameters = CreateLevelParametersWithBalloons(new[] { entry });

            var output = new List<BalloonPrefabEntry>();
            var activeCounts = new Dictionary<string, int> { ["Tough"] = 2 };

            parameters.FillGuaranteedBalloons(output, activeCounts);

            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void FillGuaranteedBalloons_MultipleEntries_FillsEachIndependently()
        {
            var entryA = CreateResolvedBalloonEntry("TypeA", guaranteed: 2, maxCount: 0);
            var entryB = CreateResolvedBalloonEntry("TypeB", guaranteed: 1, maxCount: 0);
            var parameters = CreateLevelParametersWithBalloons(new[] { entryA, entryB });

            var output = new List<BalloonPrefabEntry>();
            var activeCounts = new Dictionary<string, int>();

            parameters.FillGuaranteedBalloons(output, activeCounts);

            Assert.AreEqual(3, output.Count);
            Assert.AreEqual(2, activeCounts["TypeA"]);
            Assert.AreEqual(1, activeCounts["TypeB"]);
        }

        [Test]
        public void FillGuaranteedBalloons_MixOfGuaranteedAndNonGuaranteed_OnlyAddsGuaranteed()
        {
            var guaranteed = CreateResolvedBalloonEntry("Special", guaranteed: 2, maxCount: 0);
            var plain = CreateResolvedBalloonEntry("Plain", guaranteed: 0, maxCount: 0);
            var parameters = CreateLevelParametersWithBalloons(new[] { guaranteed, plain });

            var output = new List<BalloonPrefabEntry>();
            var activeCounts = new Dictionary<string, int>();

            parameters.FillGuaranteedBalloons(output, activeCounts);

            Assert.AreEqual(2, output.Count);
            Assert.IsFalse(activeCounts.ContainsKey("Plain"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // FillGuaranteedItems
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void FillGuaranteedItems_ZeroGuaranteed_AddsNothing()
        {
            var entry = CreateResolvedItemEntry(ItemType.Bomb, guaranteed: 0, maxCount: 0);
            var parameters = CreateLevelParametersWithItems(new[] { entry });

            var output = new List<ItemSettings>();
            var activeCounts = new Dictionary<string, int>();

            var placed = parameters.FillGuaranteedItems(output, activeCounts);

            Assert.AreEqual(0, output.Count);
            Assert.AreEqual(0, placed);
        }

        [Test]
        public void FillGuaranteedItems_GuaranteedCount_AddsThatMany()
        {
            var entry = CreateResolvedItemEntry(ItemType.Bomb, guaranteed: 2, maxCount: 0);
            var parameters = CreateLevelParametersWithItems(new[] { entry });

            var output = new List<ItemSettings>();
            var activeCounts = new Dictionary<string, int>();

            var placed = parameters.FillGuaranteedItems(output, activeCounts);

            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(2, placed);
            Assert.AreEqual(2, activeCounts[ItemType.Bomb.ToString()]);
        }

        [Test]
        public void FillGuaranteedItems_MaxCountCaps_AddsCappedAmount()
        {
            var entry = CreateResolvedItemEntry(ItemType.Laser, guaranteed: 4, maxCount: 2);
            var parameters = CreateLevelParametersWithItems(new[] { entry });

            var output = new List<ItemSettings>();
            var activeCounts = new Dictionary<string, int>();

            var placed = parameters.FillGuaranteedItems(output, activeCounts);

            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(2, placed);
            Assert.AreEqual(2, activeCounts[ItemType.Laser.ToString()]);
        }

        [Test]
        public void FillGuaranteedItems_ActiveCountsReduceHeadroom()
        {
            // guaranteed=3, maxCount=4, already 2 → only 2 more
            var entry = CreateResolvedItemEntry(ItemType.Shield, guaranteed: 3, maxCount: 4);
            var parameters = CreateLevelParametersWithItems(new[] { entry });

            var output = new List<ItemSettings>();
            var activeCounts = new Dictionary<string, int> { [ItemType.Shield.ToString()] = 2 };

            var placed = parameters.FillGuaranteedItems(output, activeCounts);

            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(2, placed);
            Assert.AreEqual(4, activeCounts[ItemType.Shield.ToString()]);
        }

        [Test]
        public void FillGuaranteedItems_ActiveCountsAtMax_AddsNothing()
        {
            var entry = CreateResolvedItemEntry(ItemType.Paint, guaranteed: 3, maxCount: 2);
            var parameters = CreateLevelParametersWithItems(new[] { entry });

            var output = new List<ItemSettings>();
            var activeCounts = new Dictionary<string, int> { [ItemType.Paint.ToString()] = 2 };

            var placed = parameters.FillGuaranteedItems(output, activeCounts);

            Assert.AreEqual(0, output.Count);
            Assert.AreEqual(0, placed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private ResolvedBalloonEntry CreateResolvedBalloonEntry(string name, int guaranteed, int maxCount)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var view = go.AddComponent<BalloonView>();
            _prefabObjects.Add(go);

            var source = new BalloonPrefabEntry();
            SetField(source, "_prefab", view);
            SetField(source, "_balloonType", BalloonType.Simple);

            return new ResolvedBalloonEntry(source, weight: 1f, maxCount: maxCount, guaranteedInitialCount: guaranteed);
        }

        private static ResolvedItemEntry CreateResolvedItemEntry(ItemType type, int guaranteed, int maxCount)
        {
            var source = new ItemSettings();
            SetField(source, "_type", type);

            return new ResolvedItemEntry(source, weight: 1f, maxCount: maxCount, guaranteedInitialCount: guaranteed);
        }

        private static LevelParameters CreateLevelParametersWithBalloons(ResolvedBalloonEntry[] balloonEntries)
        {
            var parameters = new LevelParameters();
            parameters.BindResolved(
                balloonEntries,
                System.Array.Empty<ResolvedItemEntry>(),
                System.Array.Empty<ItemSettings>(),
                System.Array.Empty<string>());
            return parameters;
        }

        private static LevelParameters CreateLevelParametersWithItems(ResolvedItemEntry[] itemEntries)
        {
            var parameters = new LevelParameters();
            parameters.BindResolved(
                System.Array.Empty<ResolvedBalloonEntry>(),
                itemEntries,
                System.Array.Empty<ItemSettings>(),
                System.Array.Empty<string>());
            return parameters;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}

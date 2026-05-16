using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class BalloonsConfigurationTests
    {
        private BalloonsConfiguration _config;
        private readonly List<GameObject> _prefabObjects = new();

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<BalloonsConfiguration>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);

            foreach (var go in _prefabObjects)
            {
                Object.DestroyImmediate(go);
            }

            _prefabObjects.Clear();
        }

        [Test]
        public void PickRandom_AllEntriesAtMax_ReturnsNull()
        {
            var entryA = CreateEntry("A", weight: 1f, maxCount: 1);
            var entryB = CreateEntry("B", weight: 1f, maxCount: 1);
            SetEntries(entryA, entryB);

            var activeCounts = new Dictionary<string, int> { ["A"] = 1, ["B"] = 1 };

            Assert.IsNull(_config.PickRandom(activeCounts));
        }

        [Test]
        public void PickRandom_MaxCountZero_NeverExcluded()
        {
            var entry = CreateEntry("Unlimited", weight: 1f, maxCount: 0);
            SetEntries(entry);

            var activeCounts = new Dictionary<string, int> { ["Unlimited"] = 999 };

            Assert.IsNotNull(_config.PickRandom(activeCounts));
        }

        [Test]
        public void PickRandom_SingleCandidate_AlwaysReturnsThatEntry()
        {
            var entry = CreateEntry("Only", weight: 1f, maxCount: 0);
            SetEntries(entry);

            var result = _config.PickRandom(new Dictionary<string, int>());

            Assert.AreSame(entry, result);
        }

        [Test]
        public void PickRandom_CappedEntryExcluded_ReturnsOther()
        {
            var capped = CreateEntry("Capped", weight: 100f, maxCount: 1);
            var available = CreateEntry("Available", weight: 1f, maxCount: 0);
            SetEntries(capped, available);

            var activeCounts = new Dictionary<string, int> { ["Capped"] = 1 };
            var result = _config.PickRandom(activeCounts);

            Assert.AreSame(available, result);
        }

        private BalloonPrefabEntry CreateEntry(string name, float weight, int maxCount)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var view = go.AddComponent<BalloonView>();
            _prefabObjects.Add(go);

            var entry = new BalloonPrefabEntry();
            SetField(entry, "_prefab", view);
            SetField(entry, "_weight", weight);
            SetField(entry, "_maxCount", maxCount);
            return entry;
        }

        private void SetEntries(params BalloonPrefabEntry[] entries)
        {
            SetField(_config, "_entries", entries);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}


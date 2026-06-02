using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class WeightedPickTests
    {
        [Test]
        public void PickRandom_SingleEntry_AlwaysReturnsIt()
        {
            var entries = new List<TestWeightedEntry>
            {
                new("Only", weight: 1f, maxCount: 0)
            };

            var result = entries.PickRandom(new Dictionary<string, int>());

            Assert.AreSame(entries[0], result);
        }

        [Test]
        public void PickRandom_AllEntriesAtMax_ReturnsNull()
        {
            var entries = new List<TestWeightedEntry>
            {
                new("A", weight: 1f, maxCount: 1),
                new("B", weight: 1f, maxCount: 1)
            };

            var counts = new Dictionary<string, int> { ["A"] = 1, ["B"] = 1 };

            Assert.IsNull(entries.PickRandom(counts));
        }

        [Test]
        public void PickRandom_CappedEntry_IsExcluded_UncappedSelected()
        {
            var entries = new List<TestWeightedEntry>
            {
                new("Capped", weight: 100f, maxCount: 1),
                new("Free", weight: 1f, maxCount: 0)
            };

            var counts = new Dictionary<string, int> { ["Capped"] = 1 };
            var result = entries.PickRandom(counts);

            Assert.AreSame(entries[1], result);
        }

        [Test]
        public void PickRandom_ZeroMaxCount_NeverCapped()
        {
            var entries = new List<TestWeightedEntry>
            {
                new("Unlimited", weight: 1f, maxCount: 0)
            };

            var counts = new Dictionary<string, int> { ["Unlimited"] = 999 };

            Assert.IsNotNull(entries.PickRandom(counts));
        }

        private class TestWeightedEntry : IWeightedEntry
        {
            public float Weight { get; }
            public int MaxCount { get; }
            public string PoolKey { get; }

            public TestWeightedEntry(string poolKey, float weight, int maxCount)
            {
                PoolKey = poolKey;
                Weight = weight;
                MaxCount = maxCount;
            }
        }
    }
}


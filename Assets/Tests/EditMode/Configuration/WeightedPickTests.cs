using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using NUnit.Framework;
using BalloonParty.Configuration.Ranges;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class WeightedPickTests
    {
        [Test]
        public void PickRandom_FullNeed_RaisesABoostedEntrysShare()
        {
            var plain = new TestWeightedEntry("Plain", weight: 1f, maxCount: 0);
            var aid = new TestWeightedEntry("Aid", weight: 1f, maxCount: 0) { NeedBoost = 3f };
            var entries = new List<TestWeightedEntry> { plain, aid };
            var counts = new Dictionary<string, int>();

            Random.InitState(7);
            var calm = 0;
            var needy = 0;
            for (var i = 0; i < 4000; i++)
            {
                calm += entries.PickRandom(counts) == aid ? 1 : 0;
                needy += entries.PickRandom(counts, need: 1f) == aid ? 1 : 0;
            }

            Assert.AreEqual(0.5f, calm / 4000f, 0.03f, "out of need the weights stand");
            Assert.AreEqual(0.8f, needy / 4000f, 0.03f, "1 × (1 + 3) against 1: four in five");
        }

        [Test]
        public void PickRandom_ZeroWeight_StaysOutAtFullNeed()
        {
            var entries = new List<TestWeightedEntry>
            {
                new("Off", weight: 0f, maxCount: 0) { NeedBoost = 5f },
                new("On", weight: 1f, maxCount: 0)
            };

            for (var i = 0; i < 20; i++)
            {
                Assert.AreSame(entries[1], entries.PickRandom(new Dictionary<string, int>(), need: 1f));
            }
        }

        [Test]
        public void ChanceOfAnyCount_IsOneLessTheShareOfZero()
        {
            var curve = new AnimationCurve(new Keyframe(0f, 3f), new Keyframe(1f, 1f));

            Assert.AreEqual(0.25f, curve.ChanceOfAnyCount(), 1e-5f);
            Assert.AreEqual(0f, new AnimationCurve().ChanceOfAnyCount());
            Assert.AreEqual(0f, new AnimationCurve(new Keyframe(0f, 1f)).ChanceOfAnyCount(), "only 0 to draw");
        }

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
            public float NeedBoost { get; set; }

            public TestWeightedEntry(string poolKey, float weight, int maxCount)
            {
                PoolKey = poolKey;
                Weight = weight;
                MaxCount = maxCount;
            }
        }
    }
}


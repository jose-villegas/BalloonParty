using System;
using System.Linq;
using BalloonParty.Configuration.HeadsUp;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     The shipped defaults: every kind of heads-up has exactly one style, the asset looks each up by id, and
    ///     the ranges the routers lean on (no <c>x2!</c>, taps only up to the third) hold.
    /// </summary>
    [TestFixture]
    public class HeadsUpSettingsTests
    {
        private HeadsUpSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<HeadsUpSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void TheDefaults_HaveExactlyOneStylePerId()
        {
            var ids = HeadsUpSettings.Defaults().Select(style => style.Id).ToArray();

            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(HeadsUpStyleId)), ids);
        }

        [Test]
        public void Style_FindsEachIdsOwn()
        {
            foreach (HeadsUpStyleId id in Enum.GetValues(typeof(HeadsUpStyleId)))
            {
                Assert.AreEqual(id, _settings.Style(id).Id);
            }
        }

        [Test]
        public void OnlyReady_IsHeld()
        {
            foreach (HeadsUpStyleId id in Enum.GetValues(typeof(HeadsUpStyleId)))
            {
                Assert.AreEqual(id == HeadsUpStyleId.Ready, _settings.Style(id).Held, id.ToString());
            }
        }

        [Test]
        public void TheStreak_StartsAtThree()
        {
            var streak = _settings.Style(HeadsUpStyleId.Streak);

            Assert.IsFalse(streak.Accepts(2));
            Assert.IsTrue(streak.Accepts(3));
            Assert.IsTrue(streak.Accepts(40));
        }

        [Test]
        public void SpeedTaps_StopAfterTheThird()
        {
            var tap = _settings.Style(HeadsUpStyleId.SpeedTap);

            Assert.IsTrue(tap.Accepts(3));
            Assert.IsFalse(tap.Accepts(4));
        }

        [Test]
        public void AShatter_NeedsTwo()
        {
            var shattered = _settings.Style(HeadsUpStyleId.Shattered);

            Assert.IsFalse(shattered.Accepts(1));
            Assert.IsTrue(shattered.Accepts(2));
        }

        [Test]
        public void ARicochetsSize_GrowsFromItsSecond()
        {
            var ricochet = _settings.Style(HeadsUpStyleId.Ricochet);

            Assert.AreEqual(ricochet.Look.SizeAt(1), ricochet.SizeAt(2), 1e-4f);
            Assert.Greater(ricochet.SizeAt(3), ricochet.SizeAt(2));
        }

        [Test]
        public void SizeAt_CountsFromWhereEscalationStarts_AndNeverShrinksBelowIt()
        {
            var style = new HeadsUpStyleSettings(HeadsUpStyleId.Streak, "{0}",
                _settings.Style(HeadsUpStyleId.Streak).Look, 3).EscalatingFrom(3);

            Assert.AreEqual(style.Look.SizeAt(1), style.SizeAt(3), 1e-4f);
            Assert.AreEqual(style.Look.SizeAt(2), style.SizeAt(4), 1e-4f);
            Assert.AreEqual(style.Look.SizeAt(1), style.SizeAt(1), 1e-4f);
        }

        [TestCase(0, 0, 0, true)]
        [TestCase(0, 0, int.MaxValue, true)]
        [TestCase(3, 0, 2, false)]
        [TestCase(3, 0, 1000, true)]
        [TestCase(0, 3, 0, true)]
        [TestCase(0, 3, 4, false)]
        public void Accepts_AZeroBoundIsNoBound(int min, int max, int arg, bool accepted)
        {
            var style = new HeadsUpStyleSettings(HeadsUpStyleId.Streak, "{0}", default, 1).WithArgRange(min, max);

            Assert.AreEqual(accepted, style.Accepts(arg));
        }

        [Test]
        public void OnlyABigStreak_IsUnderlined()
        {
            var streak = _settings.Style(HeadsUpStyleId.Streak);

            Assert.IsFalse(streak.UnderlinesAt(5));
            Assert.IsTrue(streak.UnderlinesAt(6));
            foreach (HeadsUpStyleId id in Enum.GetValues(typeof(HeadsUpStyleId)))
            {
                Assert.AreEqual(id == HeadsUpStyleId.Streak, _settings.Style(id).UnderlinesAt(100), id.ToString());
            }
        }

        [TestCase(0, 0, false)]
        [TestCase(0, 1000, false)]
        [TestCase(4, 3, false)]
        [TestCase(4, 4, true)]
        public void UnderlinesAt_ZeroMeansNever(int from, int arg, bool underlined)
        {
            var style = new HeadsUpStyleSettings(HeadsUpStyleId.Streak, "{0}", default, 1).UnderlinedFrom(from);

            Assert.AreEqual(underlined, style.UnderlinesAt(arg));
        }
    }
}

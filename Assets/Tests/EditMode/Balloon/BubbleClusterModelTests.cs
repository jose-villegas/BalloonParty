using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class BubbleClusterModelTests
    {
        private IGamePalette _palette;
        private BubbleClusterModel _model;

        [SetUp]
        public void SetUp()
        {
            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());

            _model = new BubbleClusterModel(
                new BalloonModelConfig(hitsToPop: 5, scoreValue: 1),
                _palette);
        }

        [Test]
        public void BubbleClusterModel_IsIHasDurability()
        {
            Assert.IsTrue(_model is IHasDurability);
        }

        [Test]
        public void BubbleClusterModel_IsIHasScoreColor()
        {
            Assert.IsTrue(_model is IHasScoreColor);
        }

        [Test]
        public void BubbleClusterModel_EvaluateHit_Survives_ReturnsPassThrough()
        {
            var outcome = _model.EvaluateHit(new DamageContext(1));

            Assert.AreEqual(HitOutcome.PassThrough, outcome);
        }

        [Test]
        public void BubbleClusterModel_EvaluateHit_KillingBlow_ReturnsPop()
        {
            _model.HitsRemaining.Value = 1;

            var outcome = _model.EvaluateHit(new DamageContext(1));

            Assert.AreEqual(HitOutcome.Pop, outcome);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_TotalPointsEqualHitsRemainingPlusOne()
        {
            _model.HitsRemaining.Value = 3;
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            Assert.AreEqual(3 + 1, total);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_AggregatesToOneEntryPerColor()
        {
            _model.HitsRemaining.Value = 9;
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            var seen = new HashSet<string>();
            foreach (var attr in results)
            {
                Assert.IsTrue(seen.Add(attr.ColorId), $"duplicate colour entry '{attr.ColorId}'");
            }
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_AllEntriesHaveBreaksStreak()
        {
            _model.HitsRemaining.Value = 2;
            SetupPaletteWithColors("Red");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            foreach (var attr in results)
            {
                Assert.IsTrue(attr.BreaksStreak);
            }
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_SingleColor_OneAggregatedEntry()
        {
            _model.HitsRemaining.Value = 2;
            SetupPaletteWithColors("Red");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Red", results[0].ColorId);
            Assert.AreEqual(2 + 1, results[0].Points);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_WithAllowedColors_OnlyPicksFromThem()
        {
            SetupPaletteWithColors("Red", "Blue", "Green");
            var model = new BubbleClusterModel(
                new BalloonModelConfig(hitsToPop: 5, scoreValue: 1), _palette, new[] { "Red" });
            model.HitsRemaining.Value = 5;

            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.IsTrue(results.Count > 0);
            foreach (var attr in results)
            {
                Assert.AreEqual("Red", attr.ColorId);
            }
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_EmptyPalette_AddsNothing()
        {
            _model.HitsRemaining.Value = 3;
            _palette.Colors.Returns(new List<PaletteEntry>());

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_OneColorComplete_AllPointsGoToRemainingIncomplete()
        {
            _model.HitsRemaining.Value = 6; // 7 points total
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), new[] { "Blue" }, results);

            Assert.IsFalse(results.Exists(a => a.ColorId == "Red"), "Red is already complete — must not receive points");

            var total = 0;
            foreach (var attr in results)
            {
                Assert.AreEqual("Blue", attr.ColorId);
                total += attr.Points;
            }

            Assert.AreEqual(7, total);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_AllColorsComplete_FallsBackToScatteringOverAll()
        {
            _model.HitsRemaining.Value = 6; // 7 points total
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), System.Array.Empty<string>(), results);

            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            Assert.AreEqual(7, total);
        }

        private void SetupPaletteWithColors(params string[] names)
        {
            var entries = new List<PaletteEntry>();
            foreach (var name in names)
            {
                var entry = CreatePaletteEntry(name);
                entries.Add(entry);
            }

            _palette.Colors.Returns(entries);
            _palette.ColorNames.Returns(names);
            _palette.ProgressColorNames.Returns(names);
        }

        private static PaletteEntry CreatePaletteEntry(string name)
        {
            var entry = new PaletteEntry();
            var field = typeof(PaletteEntry).GetField("_name",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field!.SetValue(entry, name);
            return entry;
        }
    }
}



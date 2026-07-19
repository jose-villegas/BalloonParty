using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class ToughBalloonModelTests
    {
        private IGamePalette _palette;
        private ToughBalloonModel _model;

        [SetUp]
        public void SetUp()
        {
            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());

            _model = new ToughBalloonModel(new BalloonModelConfig(scoreValue: 7), _palette);
        }

        [Test]
        public void ToughBalloonModel_IsIHasScoreColor()
        {
            Assert.IsTrue(_model is IHasScoreColor);
        }

        [Test]
        public void ResolveScoreAttribution_TotalPointsEqualScoreValue()
        {
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            Assert.AreEqual(7, total);
        }

        [Test]
        public void ResolveScoreAttribution_AggregatesToOneEntryPerColor()
        {
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
        public void ResolveScoreAttribution_AllEntriesHaveBreaksStreak()
        {
            SetupPaletteWithColors("Red");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            foreach (var attr in results)
            {
                Assert.IsTrue(attr.BreaksStreak);
            }
        }

        [Test]
        public void ResolveScoreAttribution_OneColorComplete_AllPointsGoToRemainingIncomplete()
        {
            // A 7-point tough with "Red" already complete must land the full 7 on "Blue" — completing
            // a colour must not waste the points that would've landed on it.
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
        public void ResolveScoreAttribution_SeveralColorsIncomplete_ScattersOnlyOverThem()
        {
            SetupPaletteWithColors("Red", "Blue", "Green");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), new[] { "Blue", "Green" }, results);

            Assert.IsFalse(results.Exists(a => a.ColorId == "Red"), "Red is already complete — must not receive points");

            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            Assert.AreEqual(7, total);
        }

        [Test]
        public void ResolveScoreAttribution_AllColorsComplete_FallsBackToScatteringOverAll()
        {
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

        [Test]
        public void ResolveScoreAttribution_WithAllowedColors_OnlyPicksFromThem()
        {
            SetupPaletteWithColors("Red", "Blue", "Green");
            var model = new ToughBalloonModel(
                new BalloonModelConfig(scoreValue: 5), _palette, new[] { "Red" });

            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.IsTrue(results.Count > 0);
            foreach (var attr in results)
            {
                Assert.AreEqual("Red", attr.ColorId);
            }
        }

        [Test]
        public void ResolveScoreAttribution_EmptyPalette_AddsNothing()
        {
            _palette.Colors.Returns(new List<PaletteEntry>());

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.AreEqual(0, results.Count);
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

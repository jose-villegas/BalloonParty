using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Palette;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class UnbreakableBalloonModelTests
    {
        private IGamePalette _palette;
        private UnbreakableBalloonModel _model;

        [SetUp]
        public void SetUp()
        {
            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());

            _model = new UnbreakableBalloonModel(new BalloonModelConfig(scoreValue: 5), _palette);
        }

        // --- Interface conformance (structural contract) ---

        [Test]
        public void UnbreakableBalloonModel_IsIHitable()
        {
            Assert.IsTrue(_model is IHitable);
        }

        [Test]
        public void UnbreakableBalloonModel_IsNotIHasDurability()
        {
            Assert.IsFalse(_model is IHasDurability);
        }

        // --- EvaluateHit (unchanged; kept as regression) ---

        [Test]
        public void UnbreakableBalloonModel_EvaluateHit_NoFlags_ReturnsDeflect()
        {
            Assert.AreEqual(HitOutcome.Deflect, _model.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void UnbreakableBalloonModel_EvaluateHit_HighDamage_NoFlags_ReturnsDeflect()
        {
            Assert.AreEqual(HitOutcome.Deflect, _model.EvaluateHit(new DamageContext(99)));
        }

        [Test]
        public void UnbreakableBalloonModel_EvaluateHit_PiercingFlag_ReturnsPop()
        {
            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing)));
        }

        [Test]
        public void UnbreakableBalloonModel_EvaluateHit_PiercingFlag_DoesNotMutateState()
        {
            var initialHits = _model.HitsRemaining.Value;

            _model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing));

            Assert.AreEqual(initialHits, _model.HitsRemaining.Value);
        }

        // --- ResolveScoreAttribution (new scatter behavior) ---

        [Test]
        public void ResolveScoreAttribution_TotalPointsEqualScoreValue()
        {
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Piercing), null, results);

            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            Assert.AreEqual(5, total);
        }

        [Test]
        public void ResolveScoreAttribution_AggregatesToOneEntryPerColor()
        {
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Piercing), null, results);

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
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Piercing), null, results);

            foreach (var attr in results)
            {
                Assert.IsTrue(attr.BreaksStreak);
            }
        }

        [Test]
        public void ResolveScoreAttribution_OneColorComplete_AllPointsGoToRemainingIncomplete()
        {
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Piercing), new[] { "Blue" }, results);

            Assert.IsFalse(results.Exists(a => a.ColorId == "Red"), "Red is already complete — must not receive points");

            var total = 0;
            foreach (var attr in results)
            {
                Assert.AreEqual("Blue", attr.ColorId);
                total += attr.Points;
            }

            Assert.AreEqual(5, total);
        }

        [Test]
        public void ResolveScoreAttribution_SeveralColorsIncomplete_ScattersOnlyOverThem()
        {
            SetupPaletteWithColors("Red", "Blue", "Green");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Piercing), new[] { "Blue", "Green" }, results);

            Assert.IsFalse(results.Exists(a => a.ColorId == "Red"), "Red is already complete — must not receive points");

            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            Assert.AreEqual(5, total);
        }

        [Test]
        public void ResolveScoreAttribution_AllColorsComplete_FallsBackToScatteringOverAll()
        {
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Piercing), System.Array.Empty<string>(), results);

            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            Assert.AreEqual(5, total);
        }

        [Test]
        public void ResolveScoreAttribution_WithAllowedColors_OnlyPicksFromThem()
        {
            SetupPaletteWithColors("Red", "Blue", "Green");
            var model = new UnbreakableBalloonModel(
                new BalloonModelConfig(scoreValue: 5), _palette, new[] { "Red" });

            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Piercing), null, results);

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
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Piercing), null, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ResolveScoreAttribution_IgnoresSourceColorId_ScattersAcrossPalette()
        {
            // Regression: the old implementation attributed ALL points to context.SourceColorId.
            // The new scatter behavior must distribute across palette colors, NOT just the source.
            SetupPaletteWithColors("Red");
            var model = new UnbreakableBalloonModel(
                new BalloonModelConfig(scoreValue: 5), _palette);

            var results = new List<ScoreAttribution>();
            // Passing "Blue" as source — old code would put 5 pts on "Blue"; new code ignores it.
            model.ResolveScoreAttribution(
                new DamageContext(1, DamageFlags.Piercing, "Blue"), null, results);

            // All points must land on "Red" (the only palette color), not "Blue" (the source).
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Red", results[0].ColorId);
            Assert.AreEqual(5, results[0].Points);
        }

        // --- Helpers ---

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


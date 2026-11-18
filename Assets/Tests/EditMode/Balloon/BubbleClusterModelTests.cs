using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
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
        public void BubbleClusterModel_ResolveScoreAttribution_TotalPointsEqualTheCountBeforeTheHit()
        {
            HitAt(4);
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            Assert.AreEqual(4, total);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_AggregatesToOneEntryPerColor()
        {
            HitAt(10);
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
            HitAt(3);
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
            HitAt(3);
            SetupPaletteWithColors("Red");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Red", results[0].ColorId);
            Assert.AreEqual(3, results[0].Points);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_WithAllowedColors_OnlyPicksFromThem()
        {
            SetupPaletteWithColors("Red", "Blue", "Green");
            var model = new BubbleClusterModel(
                new BalloonModelConfig(hitsToPop: 5, scoreValue: 1), _palette, new[] { "Red" });
            model.HitsRemaining.Value = 5;
            model.EvaluateHit(new DamageContext(1));

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
            HitAt(4);
            _palette.Colors.Returns(new List<PaletteEntry>());

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_OneColorComplete_AllPointsGoToRemainingIncomplete()
        {
            HitAt(7);
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
            HitAt(7);
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

        [TestCase(1, DamageFlags.DirectHit)]
        [TestCase(1, DamageFlags.Piercing | DamageFlags.DirectHit)]
        [TestCase(1, DamageFlags.Normal)]
        [TestCase(3, DamageFlags.Beam)]
        public void Ripe_AnyDamage_PopsTheWholeCluster(int damage, DamageFlags flags)
        {
            _model.Ripen();

            var outcome = _model.EvaluateHit(new DamageContext(damage, flags));

            Assert.AreEqual(HitOutcome.Pop, outcome);
            Assert.AreEqual(0, _model.HitsRemaining.Value);
        }

        [Test]
        public void Ripe_ABurst_ScoresTheBubblesItHeld()
        {
            _model.Ripen();
            SetupPaletteWithColors("Red");

            var context = new DamageContext(1, DamageFlags.DirectHit);
            _model.EvaluateHit(context);

            Assert.AreEqual(5, TotalPoints(context));
        }

        [Test]
        public void Unripe_APiercingPop_ScoresTheBubblesItHeld()
        {
            // It used to score 1: the count after the pop, plus one.
            _model.SetBubbles(4);
            SetupPaletteWithColors("Red");

            var context = new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit);
            var outcome = _model.EvaluateHit(context);

            Assert.AreEqual(HitOutcome.Pop, outcome);
            Assert.AreEqual(4, TotalPoints(context));
        }

        [Test]
        public void Unripe_ASurvivingHit_ScoresTheCountBeforeIt()
        {
            _model.SetBubbles(4);
            SetupPaletteWithColors("Red");

            var context = new DamageContext(1, DamageFlags.DirectHit);
            var outcome = _model.EvaluateHit(context);

            Assert.AreEqual(HitOutcome.PassThrough, outcome);
            Assert.AreEqual(3, _model.HitsRemaining.Value);
            Assert.AreEqual(4, TotalPoints(context));
        }

        [Test]
        public void Unripe_ASurvivingHitOfTwoDamage_ScoresTheCountBeforeIt()
        {
            _model.SetBubbles(4);
            SetupPaletteWithColors("Red");

            var context = new DamageContext(2, DamageFlags.DirectHit);
            var outcome = _model.EvaluateHit(context);

            Assert.AreEqual(HitOutcome.PassThrough, outcome);
            Assert.AreEqual(2, _model.HitsRemaining.Value);
            Assert.AreEqual(4, TotalPoints(context));
        }

        [Test]
        public void Unripe_AHitOfNoDamage_ScoresTheCountItFound()
        {
            _model.SetBubbles(3);
            SetupPaletteWithColors("Red");

            var context = new DamageContext(0);
            var outcome = _model.EvaluateHit(context);

            Assert.AreEqual(HitOutcome.PassThrough, outcome);
            Assert.AreEqual(3, _model.HitsRemaining.Value);
            Assert.AreEqual(3, TotalPoints(context));
        }

        [Test]
        public void AHitNeverEvaluated_ScoresAtLeastOne()
        {
            // A cheat dispatches a pop without asking the cluster first.
            _model.SetBubbles(3);
            SetupPaletteWithColors("Red");

            Assert.AreEqual(1, TotalPoints(new DamageContext(1)));
        }

        [Test]
        public void Unripe_TheLastBubble_ScoresOne()
        {
            _model.SetBubbles(1);
            SetupPaletteWithColors("Red");

            var context = new DamageContext(1, DamageFlags.DirectHit);
            _model.EvaluateHit(context);

            Assert.AreEqual(1, TotalPoints(context));
        }

        [Test]
        public void SetBubbles_BelowWhatARipeClusterHolds_ClearsRipeness()
        {
            _model.Ripen();
            _model.SetFuseLeft(2f);

            _model.SetBubbles(4);

            Assert.IsFalse(_model.Ripe.Value);
            Assert.AreEqual(0f, _model.FuseLeft.Value);
        }

        [Test]
        public void SetBubbles_ToTheSameCount_KeepsItRipe()
        {
            _model.Ripen();

            _model.SetBubbles(5);

            Assert.IsTrue(_model.Ripe.Value);
        }

        [Test]
        public void Ripe_TakesOneHitToPop_AndIsNotTough()
        {
            _model.Ripen();

            Assert.AreEqual(1, _model.HitsToPop());
            Assert.IsFalse(_model.IsTough(), "a piercing shot must burst it on contact, not plow through it");
        }

        [Test]
        public void Unripe_TakesItsBubblesToPop_AndIsTough()
        {
            _model.SetBubbles(4);

            Assert.AreEqual(4, _model.HitsToPop());
            Assert.IsTrue(_model.IsTough());
        }

        // A cluster scores the count a hit found, so the attribution tests hit it at that count first.
        private void HitAt(int held)
        {
            _model.HitsRemaining.Value = held;
            _model.EvaluateHit(new DamageContext(1));
        }

        private int TotalPoints(DamageContext context)
        {
            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(context, null, results);
            var total = 0;
            foreach (var attr in results)
            {
                total += attr.Points;
            }

            return total;
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



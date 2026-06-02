using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;

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
        public void BubbleClusterModel_ResolveScoreAttribution_EntryCountEqualsHitsRemainingPlusOne()
        {
            _model.HitsRemaining.Value = 3;
            SetupPaletteWithColors("Red", "Blue");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), results);

            Assert.AreEqual(3 + 1, results.Count);
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_AllEntriesHaveBreaksStreak()
        {
            _model.HitsRemaining.Value = 2;
            SetupPaletteWithColors("Red");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), results);

            foreach (var attr in results)
            {
                Assert.IsTrue(attr.BreaksStreak);
            }
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_EachEntryScoresOnePoint()
        {
            _model.HitsRemaining.Value = 2;
            SetupPaletteWithColors("Red");

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), results);

            foreach (var attr in results)
            {
                Assert.AreEqual(1, attr.Points);
            }
        }

        [Test]
        public void BubbleClusterModel_ResolveScoreAttribution_EmptyPalette_AddsNothing()
        {
            _model.HitsRemaining.Value = 3;
            _palette.Colors.Returns(new List<PaletteEntry>());

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), results);

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



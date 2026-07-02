using System;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class CinematicsSettingsTests
    {
        private CinematicsSettings _settings;

        [SetUp]
        public void SetUp()
        {
            // The field initializers are the canonical declarations — a fresh instance carries
            // them, and the shipped asset starts from them.
            _settings = ScriptableObject.CreateInstance<CinematicsSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void EveryState_HasDeclaredEntry()
        {
            // EntryOf throws on an unmapped state, so a new CinematicState value without a declared
            // default fails here instead of silently behaving trait-less.
            foreach (CinematicState state in Enum.GetValues(typeof(CinematicState)))
            {
                Assert.DoesNotThrow(() => _settings.EntryOf(state),
                    $"CinematicState.{state} has no entry declared in CinematicsSettings.");
            }
        }

        [Test]
        public void LevelUpStates_BlockLossAndShake()
        {
            Assert.That(_settings.EntryOf(CinematicState.LevelUpPanIn).Traits,
                Is.EqualTo(CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake));
            Assert.That(_settings.EntryOf(CinematicState.LevelUpRestore).Traits,
                Is.EqualTo(CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake));
        }

        [Test]
        public void HeartDrainStatesAndNone_DeclareNoTraits()
        {
            Assert.That(_settings.EntryOf(CinematicState.HeartDrain).Traits, Is.EqualTo(CinematicTraits.None));
            Assert.That(_settings.EntryOf(CinematicState.HeartDrainRestore).Traits, Is.EqualTo(CinematicTraits.None));
            Assert.That(_settings.EntryOf(CinematicState.None).Traits, Is.EqualTo(CinematicTraits.None));
        }

        [Test]
        public void Defaults_CarryTheAuthoredSegmentValues()
        {
            // The initializers must match the values recovered off Cinema.prefab (see the plan's
            // recovered-values table) — a fresh instance is the shipped tuning, not placeholders.
            var panIn = _settings.EntryOf(CinematicState.LevelUpPanIn).Rig;
            Assert.That(panIn.ZoomAmount, Is.EqualTo(2f));
            Assert.That(panIn.PanWeight, Is.EqualTo(0.6f));
            Assert.That(panIn.FollowSpeed, Is.EqualTo(0.7f));
            Assert.That(panIn.TimeScaleCurve.Duration(), Is.EqualTo(3f));

            var levelUpRestore = _settings.EntryOf(CinematicState.LevelUpRestore).Rig;
            Assert.That(levelUpRestore.TimeScaleCurve.Duration(), Is.EqualTo(3f));
            Assert.That(levelUpRestore.TimeScaleCurve.Evaluate(3f), Is.EqualTo(1f));

            var drain = _settings.EntryOf(CinematicState.HeartDrain).Rig;
            Assert.That(drain.ZoomAmount, Is.EqualTo(0.15f));
            Assert.That(drain.PanWeight, Is.EqualTo(0.1f));
            Assert.That(drain.FollowSpeed, Is.EqualTo(2f));
            Assert.That(drain.TimeScaleCurve.Duration(), Is.EqualTo(0.6f));

            // The heart-drain restore segment's curve carries the old restoreSeconds as its duration.
            var drainRestore = _settings.EntryOf(CinematicState.HeartDrainRestore).Rig;
            Assert.That(drainRestore.TimeScaleCurve.Duration(), Is.EqualTo(0.85f));
            Assert.That(drainRestore.TimeScaleCurve.Evaluate(0.85f), Is.EqualTo(1f));
        }

        [Test]
        public void LevelUpTrackedTrail_PulsesToFourTimesMidFlight()
        {
            var curve = _settings.EntryOf(CinematicState.LevelUpPanIn).TrackedTrail.ScaleCurve;
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(4f).Within(0.001f));
        }
    }
}

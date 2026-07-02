using System;
using BalloonParty.Configuration;
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
            // The field initializers are the canonical trait declarations — a fresh instance carries
            // them, and the shipped asset starts from them.
            _settings = ScriptableObject.CreateInstance<CinematicsSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void EveryState_HasDeclaredTraits()
        {
            // TraitsOf throws on an unmapped state, so a new CinematicState value without a declared
            // default fails here instead of silently behaving trait-less.
            foreach (CinematicState state in Enum.GetValues(typeof(CinematicState)))
            {
                Assert.DoesNotThrow(() => _settings.TraitsOf(state),
                    $"CinematicState.{state} has no traits declared in CinematicsSettings.");
            }
        }

        [Test]
        public void LevelUpStates_BlockLossAndShake()
        {
            Assert.That(_settings.TraitsOf(CinematicState.LevelUpPanIn),
                Is.EqualTo(CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake));
            Assert.That(_settings.TraitsOf(CinematicState.LevelUpRestore),
                Is.EqualTo(CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake));
        }

        [Test]
        public void HeartDrainAndNone_DeclareNoTraits()
        {
            Assert.That(_settings.TraitsOf(CinematicState.HeartDrain), Is.EqualTo(CinematicTraits.None));
            Assert.That(_settings.TraitsOf(CinematicState.None), Is.EqualTo(CinematicTraits.None));
        }
    }
}

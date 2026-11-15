using BalloonParty.Configuration.Level;
using BalloonParty.Scenario;
using NUnit.Framework;

namespace BalloonParty.Tests.Scenario
{
    [TestFixture]
    public class BackdropMixTests
    {
        [Test]
        public void PaperOutOfTheSky_IsAllPaper_WhateverTheHole()
        {
            var mix = new BackdropMix();

            mix.Set(ScenarioKind.Paper, 0.3f);

            Assert.AreEqual(1f, mix.PaperShareOfSurfaces);
        }

        [Test]
        public void PaperOverTheMeadow_IsTheHolesShare()
        {
            var mix = new BackdropMix();

            mix.Set(ScenarioKind.Paper, ScenarioKind.Ground, 0.4f);

            Assert.AreEqual(0.4f, mix.PaperShareOfSurfaces, 1e-5f);
        }

        [Test]
        public void TheMeadowAndTheWater_HaveNoPaper()
        {
            var mix = new BackdropMix();

            mix.Set(ScenarioKind.Ground, 1f);
            Assert.AreEqual(0f, mix.PaperShareOfSurfaces);

            mix.Set(ScenarioKind.Water, 1f);
            Assert.AreEqual(0f, mix.PaperShareOfSurfaces);
        }
    }
}

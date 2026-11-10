using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Level;
using BalloonParty.Scenario;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Scenario
{
    /// <summary>The grade's blend between scenarios, and the backdrop mix it is blended by.</summary>
    [TestFixture]
    public class ScenarioGradeTests
    {
        private static readonly ScenarioGrade Warm = new(1f, 20f, -40f, 30f, 10f, Color.red, Color.blue, 0.4f, Color.green);

        [Test]
        public void Lerp_AtTheEnds_IsEachGrade()
        {
            Assert.AreEqual(ScenarioGrade.Neutral, ScenarioGrade.Lerp(ScenarioGrade.Neutral, Warm, 0f));
            Assert.AreEqual(Warm, ScenarioGrade.Lerp(ScenarioGrade.Neutral, Warm, 1f));
        }

        [Test]
        public void Lerp_HalfWay_IsHalfOfEverySetting()
        {
            var half = ScenarioGrade.Lerp(ScenarioGrade.Neutral, Warm, 0.5f);

            Assert.AreEqual(0.5f, half.Exposure, 1e-5f);
            Assert.AreEqual(-20f, half.Saturation, 1e-5f);
            Assert.AreEqual(15f, half.Temperature, 1e-5f);
            Assert.AreEqual(new Color(1f, 0.5f, 0.5f, 1f), half.Filter);
            Assert.AreEqual(new Color(0.5f, 0.5f, 1f, 1f), half.Shadows);
            Assert.AreEqual(0.2f, half.ShadowLift, 1e-5f);
        }

        [Test]
        public void Lerp_PastTheEnds_StopsAtThem()
        {
            Assert.AreEqual(Warm, ScenarioGrade.Lerp(ScenarioGrade.Neutral, Warm, 2f));
            Assert.AreEqual(ScenarioGrade.Neutral, ScenarioGrade.Lerp(ScenarioGrade.Neutral, Warm, -1f));
        }

        [Test]
        public void BackdropMix_StartsOnTheSky_AndKeepsCoverageInRange()
        {
            var mix = new BackdropMix();
            Assert.AreEqual(0f, mix.Coverage);

            mix.Set(ScenarioKind.Water, 1.5f);
            Assert.AreEqual(ScenarioKind.Water, mix.Surface);
            Assert.AreEqual(1f, mix.Coverage);

            mix.Set(ScenarioKind.Ground, -0.5f);
            Assert.AreEqual(0f, mix.Coverage);
        }
    }
}

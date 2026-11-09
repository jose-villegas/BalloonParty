using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Level;
using BalloonParty.Scenario;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Scenario
{
    [TestFixture]
    public class ActiveScenarioTests
    {
        [Test]
        public void ARun_StartsOnTheSky()
        {
            Assert.AreEqual(new ScenarioShift(ScenarioKind.Sky, 0f), new ActiveScenario().Current.Value);
        }

        [Test]
        public void Reveal_ToANewScenario_CarriesItsLength()
        {
            var scenario = new ActiveScenario();

            scenario.Reveal(ScenarioKind.Ground, 4f);

            Assert.AreEqual(new ScenarioShift(ScenarioKind.Ground, 4f), scenario.Current.Value);
        }

        [Test]
        public void Reveal_ToTheSameScenario_ChangesNothing()
        {
            var scenario = new ActiveScenario();
            scenario.Cut(ScenarioKind.Ground);
            var emitted = 0;
            using (scenario.Current.Subscribe(_ => emitted++))
            {
                scenario.Reveal(ScenarioKind.Ground, 4f);
            }

            Assert.AreEqual(1, emitted, "only the subscription's own replay");
            Assert.IsTrue(scenario.Current.Value.IsCut);
        }

        [Test]
        public void Cut_AfterAReveal_EndsItOnTheSameScenario()
        {
            var scenario = new ActiveScenario();
            scenario.Reveal(ScenarioKind.Ground, 4f);

            scenario.Cut(ScenarioKind.Ground);

            Assert.IsTrue(scenario.Current.Value.IsCut);
            Assert.AreEqual(ScenarioKind.Ground, scenario.Current.Value.Kind);
        }

        [Test]
        public void AShift_NeverRunsBackwardsInTime()
        {
            Assert.IsTrue(new ScenarioShift(ScenarioKind.Ground, -3f).IsCut);
        }

        [Test]
        public void TheAscent_LastsItsCurvePlayedAtItsSpeed()
        {
            var ascend = new LevelAscendSettings(
                AnimationCurve.Linear(0f, 1f, 1.2f, 0f), 0.3f, AnimationCurve.Linear(0f, 0f, 5f, 1f));

            Assert.AreEqual(4f, ascend.AscentSeconds, 1e-4f);
            Assert.AreEqual(5f, ascend.RestartRiseSeconds, 1e-4f);
        }

        [Test]
        public void AnAscentWithNoSpeed_PlaysAtTheCurvesOwnPace()
        {
            var ascend = new LevelAscendSettings(AnimationCurve.Linear(0f, 1f, 1.2f, 0f), 0f, null);

            Assert.AreEqual(1.2f, ascend.AscentSeconds, 1e-4f);
            Assert.AreEqual(0f, ascend.RestartRiseSeconds);
        }

        [Test]
        public void Reveal_WithNoLength_BecomesACut()
        {
            var scenario = new ActiveScenario();

            scenario.Reveal(ScenarioKind.Ground, 0f);

            Assert.AreEqual(ScenarioKind.Ground, scenario.Current.Value.Kind);
            Assert.IsTrue(scenario.Current.Value.IsCut);
        }

        [Test]
        public void Reveal_IntoTheScenarioAnotherRevealIsHeadingFor_ChangesNothing()
        {
            var scenario = new ActiveScenario();
            scenario.Reveal(ScenarioKind.Ground, 4f);
            var emitted = 0;
            using (scenario.Current.Subscribe(_ => emitted++))
            {
                scenario.Reveal(ScenarioKind.Ground, 5f);
            }

            Assert.AreEqual(1, emitted, "only the subscription replay");
            Assert.AreEqual(4f, scenario.Current.Value.Seconds);
        }
    }
}

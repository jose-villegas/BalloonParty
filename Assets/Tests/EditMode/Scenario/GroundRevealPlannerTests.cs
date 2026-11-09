using BalloonParty.Configuration.Level;
using BalloonParty.Scenario;
using BalloonParty.Shared.GameState;
using NUnit.Framework;

namespace BalloonParty.Tests.Scenario
{
    [TestFixture]
    public class GroundRevealPlannerTests
    {
        private static readonly ScenarioShift RevealGround = new(ScenarioKind.Ground, 4f);
        private static readonly ScenarioShift RevealSky = new(ScenarioKind.Sky, 5f);

        [Test]
        public void TheFirstAnswer_Cuts()
        {
            var decision = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Game, null, ScenarioKind.Sky, canReveal: true);

            Assert.AreEqual(GroundRevealAction.Cut, decision.Action);
            Assert.AreEqual(ScenarioKind.Ground, decision.Target);
        }

        [Test]
        public void DuringPlay_AChangeOfScenario_RevealsOverItsLength()
        {
            var decision = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Game, NavigationState.Game, ScenarioKind.Sky, canReveal: true);

            Assert.AreEqual(GroundRevealAction.Reveal, decision.Action);
            Assert.AreEqual(ScenarioKind.Ground, decision.Target);
            Assert.AreEqual(4f, decision.Seconds);
        }

        [Test]
        public void ACutShift_Cuts()
        {
            var decision = GroundRevealPlanner.Decide(
                new ScenarioShift(ScenarioKind.Ground, 0f), NavigationState.Game, NavigationState.Game,
                ScenarioKind.Sky, canReveal: true);

            Assert.AreEqual(GroundRevealAction.Cut, decision.Action);
        }

        [Test]
        public void WithoutAMist_EveryChangeCuts()
        {
            var decision = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Game, NavigationState.Game, ScenarioKind.Sky, canReveal: false);

            Assert.AreEqual(GroundRevealAction.Cut, decision.Action);
        }

        [Test]
        public void TheLaunchScreen_IsAlwaysSky_AndEnteringItCuts()
        {
            var decision = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Launch, NavigationState.Game, ScenarioKind.Ground, canReveal: true);

            Assert.AreEqual(GroundRevealAction.Cut, decision.Action);
            Assert.AreEqual(ScenarioKind.Sky, decision.Target);
        }

        [Test]
        public void LeavingTheLaunchScreen_CutsToTheLevelsScenario()
        {
            var decision = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Game, NavigationState.Launch, ScenarioKind.Sky, canReveal: true);

            Assert.AreEqual(GroundRevealAction.Cut, decision.Action);
            Assert.AreEqual(ScenarioKind.Ground, decision.Target);
        }

        [Test]
        public void ARestartNavigatingBackToGame_LeavesItsRevealRunning()
        {
            // The restart publishes its reset (the reveal, already heading for sky) and then navigates to Game.
            var decision = GroundRevealPlanner.Decide(
                RevealSky, NavigationState.Game, NavigationState.GameOver, ScenarioKind.Sky, canReveal: true);

            Assert.AreEqual(GroundRevealAction.None, decision.Action);
        }

        [Test]
        public void TheScenarioAlreadyShowing_ChangesNothing()
        {
            var decision = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Game, NavigationState.Game, ScenarioKind.Ground, canReveal: true);

            Assert.AreEqual(GroundRevealAction.None, decision.Action);
        }
    }
}

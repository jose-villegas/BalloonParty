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
        public void DuringPlay_TheSkyToWater_RevealsOverItsLength()
        {
            var decision = GroundRevealPlanner.Decide(
                new ScenarioShift(ScenarioKind.Water, 4f), NavigationState.Game, NavigationState.Game, ScenarioKind.Sky,
                canReveal: true);

            Assert.AreEqual(GroundRevealAction.Reveal, decision.Action);
            Assert.AreEqual(ScenarioKind.Water, decision.Target);
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
                new ScenarioShift(ScenarioKind.Ground, 0f), NavigationState.Game, NavigationState.Launch,
                ScenarioKind.Sky, canReveal: true);

            Assert.AreEqual(GroundRevealAction.Cut, decision.Action);
            Assert.AreEqual(ScenarioKind.Ground, decision.Target);
        }

        // A saved run continued on a meadow: its reveal was told on the launch screen and waits for the game.
        [Test]
        public void LeavingTheLaunchScreen_WithARevealWaiting_OpensTheSkyOntoTheLevel()
        {
            var waiting = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Launch, NavigationState.Launch, ScenarioKind.Sky, canReveal: true);
            var decision = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Game, NavigationState.Launch, ScenarioKind.Sky, canReveal: true);

            Assert.AreEqual(GroundRevealAction.None, waiting.Action, "the launch screen stays sky");
            Assert.AreEqual(GroundRevealAction.Reveal, decision.Action);
            Assert.AreEqual(ScenarioKind.Ground, decision.Target);
            Assert.AreEqual(4f, decision.Seconds, 1e-4f);
        }

        [Test]
        public void LeavingTheLaunchScreen_WithARevealWaiting_ButNoMist_Cuts()
        {
            var decision = GroundRevealPlanner.Decide(
                RevealGround, NavigationState.Game, NavigationState.Launch, ScenarioKind.Sky, canReveal: false);

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

        // ScenarioKind is internal, and NUnit's test methods are public, so the cases pass it as an int.
        [TestCase((int)ScenarioKind.Ground, (int)ScenarioKind.Paper)]
        [TestCase((int)ScenarioKind.Water, (int)ScenarioKind.Paper)]
        [TestCase((int)ScenarioKind.Paper, (int)ScenarioKind.Ground)]
        [TestCase((int)ScenarioKind.Paper, (int)ScenarioKind.Water)]
        public void PaperAndASurface_SwapWithNoSkyBetween(int from, int to)
        {
            Assert.IsTrue(GroundRevealPlanner.IsPaperSwap((ScenarioKind)from, (ScenarioKind)to));
        }

        [TestCase((int)ScenarioKind.Sky, (int)ScenarioKind.Paper)]
        [TestCase((int)ScenarioKind.Paper, (int)ScenarioKind.Sky)]
        [TestCase((int)ScenarioKind.Sky, (int)ScenarioKind.Ground)]
        [TestCase((int)ScenarioKind.Water, (int)ScenarioKind.Sky)]
        [TestCase((int)ScenarioKind.Paper, (int)ScenarioKind.Paper)]
        public void TheSkyOrNoPaper_RevealsThroughTheMist(int from, int to)
        {
            Assert.IsFalse(GroundRevealPlanner.IsPaperSwap((ScenarioKind)from, (ScenarioKind)to));
        }

        [Test]
        public void IntoPaper_RevealsLikeAnyOtherSurface()
        {
            var decision = GroundRevealPlanner.Decide(
                new ScenarioShift(ScenarioKind.Paper, 4f), NavigationState.Game, NavigationState.Game,
                ScenarioKind.Ground, canReveal: true);

            Assert.AreEqual(GroundRevealAction.Reveal, decision.Action);
            Assert.AreEqual(ScenarioKind.Paper, decision.Target);
        }
    }
}

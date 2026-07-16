using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Cheats;
using BalloonParty.Game.Level;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Drives a real level-up in the scene (via the score cheat) and lets the ceremony/cinematic
    ///     run — the memory-flagged fragile path (CinematicDirector / level-up trail). The guard: it
    ///     leaves the Playing phase and runs without throwing or logging an error.
    /// </summary>
    public class LevelUpCinematicPlayModeTests : PlayModeGameTest
    {
        // The level lock would suppress the very level-up ceremony this fixture triggers.
        protected override bool ProtectRunFromLoss => false;

        [UnityTest]
        public IEnumerator TriggerLevelUp_RunsCeremonyWithoutError()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var levelProgress = Resolve<ILevelProgress>();
            var cheat = Resolve<IEnumerable<ICheat>>().FirstOrDefault(c => c.Name == "Trigger Level Up");
            if (cheat == null)
            {
                Assert.Inconclusive("Trigger Level Up cheat not registered (needs a dev build).");
                yield break;
            }

            yield return WaitUntil(() => BalloonCount(grid) > 0, message: "Balloons never spawned.");

            cheat.Execute();

            // Reaching the ceremony (leaving Playing) is the trigger; running frames without an exception
            // or error log is the regression guard for the fragile cinematic path.
            yield return WaitUntil(() => levelProgress.Phase.Value != LevelUpPhase.Playing,
                message: "Level-up ceremony never started after the cheat.");

            for (var i = 0; i < 120; i++)
            {
                yield return null;
            }
        }
    }
}

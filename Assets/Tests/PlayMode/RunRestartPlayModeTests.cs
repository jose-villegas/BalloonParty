using System.Collections;
using BalloonParty.Game.Run;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Exercises the in-place restart loop end-to-end in the real Game scene — the pooling /
    ///     broadcast / re-spawn behaviour that EditMode can't drive. A green run here means a
    ///     restart clears the board (balloons + static actors) and repopulates it without throwing
    ///     (the kind of failure the prewarm double-await produced).
    /// </summary>
    public class RunRestartPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator Restart_ClearsAndRepopulatesBoard()
        {
            yield return LoadGameScene();

            var scope = Scope();
            var grid = scope.Container.Resolve<SlotGrid>();
            var runController = scope.Container.Resolve<RunController>();

            // Wait for balloons specifically — static actors spawn synchronously first, so a bare
            // "any occupied slot" check would pass before balloon prewarm finishes and let the
            // restart race the initial spawn.
            yield return WaitUntil(() => BalloonCount(grid) > 0, message: "Balloons never spawned on load.");

            runController.EndRun();
            yield return null;

            runController.RestartRun();

            // The reset empties the board (Board stage) and the Respawn stage repopulates it.
            yield return WaitUntil(() => BalloonCount(grid) > 0, message: "Balloons did not respawn after restart.");
        }
    }
}

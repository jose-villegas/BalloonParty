using System;
using System.Collections;
using BalloonParty.Game;
using BalloonParty.Game.Run;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    public class RunRestartPlayModeTests
    {
        private const float TimeoutSeconds = 8f;

        [UnityTest]
        public IEnumerator Restart_ClearsAndRepopulatesBoard()
        {
            yield return LoadGameScene();

            var scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
            Assert.IsNotNull(scope, "GameLifetimeScope not found in the Game scene.");

            var grid = scope.Container.Resolve<SlotGrid>();
            var runController = scope.Container.Resolve<RunController>();

            // Wait for balloons specifically — static actors spawn synchronously first, so a bare
            // "any occupied slot" check would pass before balloon prewarm finishes and let the
            // restart race the initial spawn.
            yield return WaitUntil(() => BalloonCount(grid) > 0);
            Assert.Greater(BalloonCount(grid), 0, "Balloons never spawned on initial load.");

            runController.EndRun();
            yield return null;

            runController.RestartRun();

            // The reset empties the board (Board stage) and the Respawn stage repopulates it.
            yield return WaitUntil(() => BalloonCount(grid) > 0);
            Assert.Greater(BalloonCount(grid), 0, "Balloons did not respawn after restart.");
        }

        private static IEnumerator LoadGameScene()
        {
            var load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (load != null && !load.isDone)
            {
                yield return null;
            }

            // One extra frame so GameLifetimeScope builds and its entry points run.
            yield return null;
        }

        private static int BalloonCount(SlotGrid grid)
        {
            var count = 0;
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    // Balloons are dynamic actors; static actors (Puff/Bush) are not.
                    if (grid.At(new Vector2Int(col, row)) is IWriteableDynamicSlotActor)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            var elapsed = 0f;
            while (!condition() && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}

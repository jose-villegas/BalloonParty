using System;
using System.Collections;
using BalloonParty.Game;
using BalloonParty.Shared.GameState;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Base for PlayMode tests that drive the real Game scene: loads the scene, resolves services
    ///     from <see cref="GameLifetimeScope" />, and waits on conditions with a timeout. PlayMode
    ///     shares static state (no domain reload between tests), so <see cref="ResetNavigation" />
    ///     returns to Launch — the scene's EditorNavigationBootstrap promotes that to Game on load,
    ///     which the spawn ReadyGate waits for. Without it, a test that ended in GameOver leaves the
    ///     gate shut and the next test never spawns.
    /// </summary>
    public abstract class PlayModeGameTest
    {
        internal const float DefaultTimeout = 15f;

        [SetUp]
        public void ResetNavigation()
        {
            Navigation.TransitionTo(NavigationState.Launch);
        }

        internal static IEnumerator LoadGameScene()
        {
            var load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (load != null && !load.isDone)
            {
                yield return null;
            }

            // One extra frame so GameLifetimeScope builds and its entry points run.
            yield return null;
        }

        internal static GameLifetimeScope Scope()
        {
            var scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
            Assert.IsNotNull(scope, "GameLifetimeScope not found in the Game scene.");
            return scope;
        }

        internal static T Resolve<T>()
        {
            return Scope().Container.Resolve<T>();
        }

        internal static int BalloonCount(SlotGrid grid)
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

        /// <summary>Pumps frames until <paramref name="condition" /> holds; fails the test on timeout
        /// rather than returning silently (so a never-met wait can't hang the runner).</summary>
        internal static IEnumerator WaitUntil(Func<bool> condition, float timeout = DefaultTimeout,
            string message = null)
        {
            var elapsed = 0f;
            while (!condition() && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(condition(), message ?? "Timed out waiting for condition.");
        }
    }
}

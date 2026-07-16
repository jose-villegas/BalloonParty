using System;
using System.Collections;
using BalloonParty.Balloon.Model;
using BalloonParty.Cheats;
using BalloonParty.Game;
using DG.Tweening;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using MessagePipe;
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

        // Board-filling scaffolding can reject enough spawns to drain HP to zero, and a lost run swaps
        // the board for the loss→restart cinematic state mid-test. The dev level lock makes hearts
        // undrainable (and EndRun a no-op), so fixtures observe only the motion they drive. Fixtures
        // that deliberately exercise loss, level-up, or EndRun override this to false.
        protected virtual bool ProtectRunFromLoss => true;

        [SetUp]
        public void ResetNavigation()
        {
            Navigation.TransitionTo(NavigationState.Launch);

            // Per-test rather than static: CheatState.ResetOnPlay clears the flag at play-mode start.
            CheatState.BlockLevelUp = ProtectRunFromLoss;
        }

        [TearDown]
        public void ClearRunLossProtection()
        {
            CheatState.BlockLevelUp = false;
        }

        internal static IEnumerator LoadGameScene()
        {
            // A tween mid-flight when the previous test's scene unloads (loss-cinematic camera move,
            // score trail, thrower entrance) would fire on destroyed targets in THIS test — kill
            // everything before loading so each test starts tween-clean.
            DOTween.KillAll();

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

        // The grid registers a balloon at its slot before its collider finishes flying there (spawn and
        // balance animation isn't tracked by BalancePathHolder), so a physics activation can fire into
        // empty space. Wait until a collider is actually present at the slot centre.
        internal static IEnumerator WaitForColliderAt(SlotGrid grid, Vector2Int slot, float timeout = DefaultTimeout)
        {
            var pos = (Vector2)grid.IndexToWorldPosition(slot);
            yield return WaitUntil(() => Physics2D.OverlapPoint(pos) != null, timeout,
                $"No collider arrived at slot {slot}.");
        }

        // Packs the board with a few lines and waits for it to settle — overflow hold released and no
        // balance move still in transit — so activations aim at balloons whose colliders have arrived.
        internal static IEnumerator FillAndSettle(SlotGrid grid, int lines = 4)
        {
            yield return WaitUntil(() => BalloonCount(grid) > 0);

            var linePublisher = Resolve<IPublisher<SpawnBalloonLineMessage>>();
            for (var i = 0; i < lines; i++)
            {
                linePublisher.Publish(new SpawnBalloonLineMessage(1));
                yield return null;
            }

            yield return WaitUntil(() => BalloonCount(grid) > grid.Columns * 2,
                message: "Board never filled enough.");

            var pause = Resolve<PauseService>();
            var transit = Resolve<BalancePathHolder>();
            yield return WaitUntil(() => !pause.IsAnyPaused.Value, message: "Overflow hold never released.");
            yield return WaitUntil(() => !AnyInTransit(transit, grid), message: "Balance moves never settled.");

            // Transit only tracks balance moves — spawn path tweens fly outside BalancePathHolder, so
            // models occupy slots while their colliders are still en route. A physics activation fired
            // then hits empty space; wait for every actor's IsStable (set by the spawn/balance
            // OnComplete) so "settled" means colliders have actually arrived.
            yield return WaitUntil(() => AllStable(grid), message: "Some balloon never reported IsStable.");
        }

        internal static bool AllStable(SlotGrid grid)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.At(new Vector2Int(col, row)) is IDynamicSlotActor { IsStable: { Value: false } })
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static bool AnyInTransit(BalancePathHolder transit, SlotGrid grid)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (transit.IsInTransit(col, row))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Finds an interior balloon with a populated neighbourhood, so a blast/cross has balloons to
        // hit — a lone interior balloon removes nothing (the blast excludes the popped source).
        internal static bool TryFindInteriorBalloon(SlotGrid grid, out Vector2Int slot, out IBalloonModel model)
        {
            var neighbours = new Vector2Int[6];
            for (var col = 1; col < grid.Columns - 1; col++)
            {
                for (var row = 1; row < grid.Rows - 1; row++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (grid.At(candidate) is not IBalloonModel balloon)
                    {
                        continue;
                    }

                    HexCoordinates.HexNeighborIndices(col, row, neighbours);
                    var occupied = 0;
                    for (var n = 0; n < 6; n++)
                    {
                        if (!grid.IsEmpty(neighbours[n].x, neighbours[n].y))
                        {
                            occupied++;
                        }
                    }

                    if (occupied >= 2)
                    {
                        slot = candidate;
                        model = balloon;
                        return true;
                    }
                }
            }

            slot = default;
            model = null;
            return false;
        }
    }
}

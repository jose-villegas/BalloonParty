using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Drives a bomb activation against real balloon colliders in the Game scene — the
    ///     Physics2D.OverlapCircle blast EditMode can't exercise. Fills the board, then activates the
    ///     bomb handler on an interior balloon and asserts the blast destroys at least one neighbour.
    /// </summary>
    public class BombActivationPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator Activate_PopsNeighbouringBalloons()
        {
            yield return LoadGameScene();

            var scope = Scope();
            var grid = scope.Container.Resolve<SlotGrid>();
            var bomb = scope.Container.Resolve<IEnumerable<IBalloonItem>>()
                .First(handler => handler.Type == ItemType.Bomb);
            var linePublisher = scope.Container.Resolve<IPublisher<SpawnBalloonLineMessage>>();
            var pause = scope.Container.Resolve<PauseService>();
            var transit = scope.Container.Resolve<BalancePathHolder>();

            // Pack the board so an interior balloon is surrounded by poppable neighbours. Few enough
            // lines that the board doesn't saturate — saturation triggers the overflow/heart loss loop
            // (slow-mo cinematic, HP drain to GameOver), which is PressureLossPlayModeTests' subject.
            yield return WaitUntil(() => BalloonCount(grid) > 0);
            for (var i = 0; i < 4; i++)
            {
                linePublisher.Publish(new SpawnBalloonLineMessage(1));
                yield return null;
            }

            yield return WaitUntil(() => BalloonCount(grid) > grid.Columns * 2,
                message: "Board never filled enough to bomb.");

            // Let the board settle before aiming: the overflow hold must release (any rejects drained)
            // and every balance move must land. The grid registers a balloon at its slot while its
            // collider is still flying there — a blast at the slot's world position during transit
            // finds nothing to overlap.
            yield return WaitUntil(() => !pause.IsAnyPaused.Value,
                message: "Overflow hold never released — the board saturated.");
            yield return WaitUntil(() => !AnyInTransit(transit, grid),
                message: "Balance moves never settled.");

            if (!TryFindInteriorBalloon(grid, out var slot, out var model))
            {
                Assert.Inconclusive("No interior balloon with a populated neighbourhood to bomb.");
                yield break;
            }

            var before = BalloonCount(grid);

            // Activate is synchronous (returns CompletedTask) — the blast publishes its hits inline.
            _ = bomb.Activate(new ItemActivationContext(model, grid.IndexToWorldPosition(slot), Vector3.zero));

            yield return WaitUntil(() => BalloonCount(grid) < before, timeout: 5f,
                message: "Bomb blast did not remove any balloon.");
        }

        private static bool AnyInTransit(BalancePathHolder transit, SlotGrid grid)
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

        private static bool TryFindInteriorBalloon(SlotGrid grid, out Vector2Int slot, out IBalloonModel model)
        {
            for (var col = 1; col < grid.Columns - 1; col++)
            {
                for (var row = 1; row < grid.Rows - 1; row++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (grid.At(candidate) is IBalloonModel balloon)
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

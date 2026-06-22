using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

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

            // Pack the board so an interior balloon is surrounded by poppable neighbours.
            yield return WaitUntil(() => BalloonCount(grid) > 0);
            for (var i = 0; i < 8; i++)
            {
                linePublisher.Publish(new SpawnBalloonLineMessage(1));
                yield return null;
            }

            yield return WaitUntil(() => BalloonCount(grid) > grid.Columns * 2,
                message: "Board never filled enough to bomb.");

            if (!TryFindInteriorBalloon(grid, out var slot, out var model))
            {
                Assert.Inconclusive("No interior balloon with a populated neighbourhood to bomb.");
                yield break;
            }

            var before = BalloonCount(grid);

            bomb.Setup(model, grid.IndexToWorldPosition(slot));
            // Activate is synchronous (returns CompletedTask) — the blast publishes its hits inline.
            _ = bomb.Activate();

            yield return WaitUntil(() => BalloonCount(grid) < before, timeout: 5f,
                message: "Bomb blast did not remove any balloon.");
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

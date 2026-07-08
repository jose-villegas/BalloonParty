using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Item;
using BalloonParty.Slots.Grid;
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
    ///     bomb handler on an interior balloon with a populated neighbourhood and asserts the blast
    ///     destroys at least one neighbour.
    /// </summary>
    public class BombActivationPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator Activate_PopsNeighbouringBalloons()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var bomb = Resolve<IEnumerable<IBalloonItem>>().First(handler => handler.Type == ItemType.Bomb);

            yield return FillAndSettle(grid);

            if (!TryFindInteriorBalloon(grid, out var slot, out var model))
            {
                Assert.Inconclusive("No interior balloon with a populated neighbourhood to bomb.");
                yield break;
            }

            // The blast is physics (OverlapCircle) — wait for the target's collider to actually arrive.
            yield return WaitForColliderAt(grid, slot);

            var before = BalloonCount(grid);

            // Activate is synchronous (returns CompletedTask) — the blast publishes its hits inline.
            _ = bomb.Activate(new ItemActivationContext(model, grid.IndexToWorldPosition(slot), Vector3.zero));

            yield return WaitUntil(() => BalloonCount(grid) < before, timeout: 5f,
                message: "Bomb blast did not remove any balloon.");
        }
    }
}

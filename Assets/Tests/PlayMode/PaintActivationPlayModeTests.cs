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
    ///     Runs a paint activation in the real scene — the pooled splash flights + per-blob landing
    ///     callbacks + disturbance stamps that only execute under the player loop. Recolour correctness
    ///     is covered in EditMode (splash coverage vs the grid is too sparse to assert deterministically
    ///     here); this guards that the whole splash path runs across frames without throwing, and that
    ///     paint never destroys a balloon.
    /// </summary>
    public class PaintActivationPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator Activate_RunsSplashWithoutError()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var paint = Resolve<IEnumerable<IBalloonItem>>().First(handler => handler.Type == ItemType.Paint);

            yield return FillAndSettle(grid);

            if (!TryFindInteriorBalloon(grid, out var slot, out var model))
            {
                Assert.Inconclusive("No interior balloon to paint from.");
                yield break;
            }

            var before = BalloonCount(grid);
            var origin = grid.IndexToWorldPosition(slot);

            _ = paint.Activate(new ItemActivationContext(model, origin, Vector3.down));

            // Let the blobs fly and land (recolour is deferred) — any thrown exception or error log fails.
            for (var i = 0; i < 120; i++)
            {
                yield return null;
            }

            Assert.AreEqual(before, BalloonCount(grid), "Paint must recolour, never destroy — count changed.");
        }
    }
}

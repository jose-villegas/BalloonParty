using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Item;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Drives a paint activation in the real scene — the pooled splash flights that recolour covered
    ///     balloons as blobs land (deferred across frames, so EditMode can't drive it). Paints every
    ///     other balloon a different colour first so whatever the splash fan covers is a repaintable
    ///     target, then asserts the source-colour count grows (coverage is patchy by design, so it
    ///     asserts "some balloon repainted", not a specific slot).
    /// </summary>
    public class PaintActivationPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator Activate_RecoloursCoveredBalloons()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var paint = Resolve<IEnumerable<IBalloonItem>>().First(handler => handler.Type == ItemType.Paint);
            var palette = Resolve<IGamePalette>();

            yield return FillAndSettle(grid);

            if (!TryFindInteriorBalloon(grid, out var slot, out var model) || model is not IPaintable source)
            {
                Assert.Inconclusive("No interior paintable balloon to paint from.");
                yield break;
            }

            var paintColor = source.Color.Value;
            var otherColor = palette.ColorNames.FirstOrDefault(n => n != paintColor);
            if (otherColor == null)
            {
                Assert.Inconclusive("Palette has only one colour — nothing to distinguish a paint target.");
                yield break;
            }

            if (!TryGetOccupiedNeighbour(grid, slot, out var neighbourSlot))
            {
                Assert.Inconclusive("Source balloon had no occupied neighbour to aim at.");
                yield break;
            }

            // Every other paintable balloon becomes the contrasting colour, so any slot the (small-radius)
            // splash fan happens to cover is a valid repaint target — independent of blob packing.
            ForceOthersToColor(grid, source, otherColor);

            var origin = grid.IndexToWorldPosition(slot);
            var target = grid.IndexToWorldPosition(neighbourSlot);
            var before = CountWithColor(grid, paintColor);

            _ = paint.Activate(new ItemActivationContext(model, origin, target - origin));

            var elapsed = 0f;
            while (CountWithColor(grid, paintColor) <= before && elapsed < 4f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Greater(CountWithColor(grid, paintColor), before,
                "Paint did not recolour any covered balloon to the source colour.");
        }

        private static void ForceOthersToColor(SlotGrid grid, IPaintable source, string color)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.At(new Vector2Int(col, row)) is IPaintable p && !ReferenceEquals(p, source))
                    {
                        p.Color.Value = color;
                    }
                }
            }
        }

        private static bool TryGetOccupiedNeighbour(SlotGrid grid, Vector2Int slot, out Vector2Int neighbourSlot)
        {
            var neighbours = new Vector2Int[6];
            HexCoordinates.HexNeighborIndices(slot.x, slot.y, neighbours);
            for (var n = 0; n < 6; n++)
            {
                var candidate = neighbours[n];
                if (!grid.IsEmpty(candidate.x, candidate.y) && grid.At(candidate) is IPaintable)
                {
                    neighbourSlot = candidate;
                    return true;
                }
            }

            neighbourSlot = default;
            return false;
        }

        private static int CountWithColor(SlotGrid grid, string color)
        {
            var count = 0;
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.At(new Vector2Int(col, row)) is IHasColor c && c.Color.Value == color)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}

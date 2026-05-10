using UnityEngine;

namespace BalloonParty.Shared
{
    public static class SortingHelper
    {
        public static int SlotBaseSortingOrder(Vector2Int slotIndex, Vector2Int gridSize, int layerMultiplier)
        {
            var maxRow = gridSize.y - 1;
            return (slotIndex.x + ((maxRow - slotIndex.y) * gridSize.x)) * layerMultiplier;
        }

        public static void ApplySortingOrder(Renderer[] renderers, int startOrder)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = startOrder + i + 1;
            }
        }
    }
}


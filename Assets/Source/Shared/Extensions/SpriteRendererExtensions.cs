using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class SpriteRendererExtensions
    {
        // Tints every non-null renderer in the set; a null array is a no-op.
        internal static void SetColor(this SpriteRenderer[] renderers, Color color)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.color = color;
                }
            }
        }
    }
}

using BalloonParty.Configuration.Palette;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class ColorExtensions
    {
        internal static Color WithAlpha(this Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, alpha);
        }

        /// <summary>Index of <paramref name="colorId" /> in the palette's Colors order — the mapping the disturbance field's A channel encodes; -1 when absent (colorless, rainbow).</summary>
        internal static int PaletteIndexOf(this IGamePalette palette, string colorId)
        {
            if (string.IsNullOrEmpty(colorId))
            {
                return -1;
            }

            for (var i = 0; i < palette.Colors.Count; i++)
            {
                if (palette.Colors[i].Name == colorId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

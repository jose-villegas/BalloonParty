using UnityEngine;

namespace BalloonParty.Configuration.Palette
{
    public static class GamePaletteExtensions
    {
        /// <summary>Every concrete palette colour as <see cref="Color" /> values — the set the rainbow items cycle their iridescent effect through.</summary>
        public static Color[] ColorValues(this IGamePalette palette)
        {
            var entries = palette.Colors;
            var result = new Color[entries.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = entries[i].Color;
            }

            return result;
        }
    }
}

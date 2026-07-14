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

        /// <summary>Palette index of a colour by name (its position in <see cref="IGamePalette.Colors" /> /
        /// <see cref="IGamePalette.ColorNames" />), or -1 if not found — the index the field/stamp systems encode.</summary>
        public static int IndexOfColor(this IGamePalette palette, string colorName)
        {
            var names = palette.ColorNames;
            for (var i = 0; i < names.Count; i++)
            {
                if (names[i] == colorName)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

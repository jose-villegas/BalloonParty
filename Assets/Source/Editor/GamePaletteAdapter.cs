using BalloonParty.Configuration.Palette;
using BalloonParty.EditorUI.Palette;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Adapts <see cref="GamePalette"/> to the package's <see cref="IColorPalette"/> interface.</summary>
    internal sealed class GamePaletteAdapter : IColorPalette
    {
        private readonly GamePalette _palette;

        public int Count => _palette != null ? _palette.Colors.Count : 0;

        internal GamePaletteAdapter(GamePalette palette)
        {
            _palette = palette;
        }

        public string GetName(int index)
        {
            return _palette.Colors[index].Name;
        }

        public Color GetColor(int index)
        {
            return _palette.Colors[index].Color;
        }
    }
}

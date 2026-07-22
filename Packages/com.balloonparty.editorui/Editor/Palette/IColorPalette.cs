using UnityEngine;

namespace BalloonParty.EditorUI.Palette
{
    /// <summary>Minimal read-only color palette contract for editor UI components.</summary>
    public interface IColorPalette
    {
        /// <summary>Number of colors in the palette.</summary>
        int Count { get; }

        /// <summary>Display name for the color at <paramref name="index"/>.</summary>
        string GetName(int index);

        /// <summary>Color value at <paramref name="index"/>.</summary>
        Color GetColor(int index);
    }
}

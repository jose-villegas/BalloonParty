using System.Linq;
using BalloonParty.Configuration;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Reusable palette color picker and swatch drawing for editor UI.
    ///     Wraps <see cref="ConfigAssetCache{T}"/> for <see cref="GamePalette"/>.
    /// </summary>
    public sealed class PaletteColorPicker
    {
        private const float SwatchSize = 16f;
        private const float SwatchSpacing = 4f;

        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();

        private GUIContent[] _popupOptions;
        private string[] _names;
        private int _selectedIndex;

        public GamePalette Palette => _paletteCache.Value;
        public int SelectedIndex => _selectedIndex;

        public Color SelectedColor
        {
            get
            {
                var palette = Palette;

                if (palette == null || palette.Colors.Length == 0)
                {
                    return Color.white;
                }

                var index = Mathf.Clamp(_selectedIndex, 0, palette.Colors.Length - 1);
                return palette.Colors[index].Color;
            }
        }

        /// <summary>
        ///     Draws a popup with all palette color names and a color swatch.
        ///     Uses <see cref="EditorGUILayout"/> (auto-layout). Returns true if the palette is available.
        /// </summary>
        public bool DrawLayout(string label = "Tint")
        {
            var palette = Palette;

            if (palette == null || palette.Colors.Length == 0)
            {
                EditorGUILayout.HelpBox("No GamePalette asset found.", MessageType.Warning);
                return false;
            }

            EnsureOptions(palette);

            var rect = EditorGUILayout.GetControlRect();
            var popupRect = new Rect(rect.x,
                rect.y,
                rect.width - SwatchSize - SwatchSpacing,
                rect.height);
            var swatchRect = new Rect(rect.xMax - SwatchSize, rect.y, SwatchSize, SwatchSize);

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _popupOptions.Length - 1);
            _selectedIndex = EditorGUI.Popup(popupRect,
                new GUIContent(label),
                _selectedIndex,
                _popupOptions);

            DrawSwatch(swatchRect, palette.Colors[_selectedIndex].Color);
            return true;
        }

        /// <summary>
        ///     Draws a color swatch with a 1px black border. Rect-based (manual layout).
        /// </summary>
        public static void DrawSwatch(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, color);
            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, 1), Color.black);
            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.yMax, rect.width + 2, 1), Color.black);
            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y, 1, rect.height), Color.black);
            EditorGUI.DrawRect(new Rect(rect.xMax, rect.y, 1, rect.height), Color.black);
        }

        private void EnsureOptions(GamePalette palette)
        {
            if (_names != null && _names.Length == palette.Colors.Length)
            {
                return;
            }

            _names = palette.Colors.Select(c => c.Name).ToArray();
            _popupOptions = _names.Select(n => new GUIContent(n)).ToArray();
        }
    }
}

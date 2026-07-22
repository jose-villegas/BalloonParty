using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Palette
{
    /// <summary>Color picker popup + swatch for any <see cref="IColorPalette"/> implementation.</summary>
    public sealed class PaletteColorPicker
    {
        private const float SwatchSize = 16f;
        private const float SwatchSpacing = 4f;

        private GUIContent[] _popupOptions;
        private int _cachedCount;
        private int _selectedIndex;

        /// <summary>Currently selected color index.</summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => _selectedIndex = value;
        }

        /// <summary>Returns the selected color from the palette, or white if unavailable.</summary>
        public Color GetSelectedColor(IColorPalette palette)
        {
            if (palette == null || palette.Count == 0)
            {
                return Color.white;
            }

            var index = Mathf.Clamp(_selectedIndex, 0, palette.Count - 1);
            return palette.GetColor(index);
        }

        /// <summary>
        /// Draws a popup with all palette color names and a swatch.
        /// Returns true if the palette is available and drawn.
        /// </summary>
        public bool DrawLayout(IColorPalette palette, string label = "Tint")
        {
            if (palette == null || palette.Count == 0)
            {
                EditorGUILayout.HelpBox("No palette available.", MessageType.Warning);
                return false;
            }

            EnsureOptions(palette);

            var rect = EditorGUILayout.GetControlRect();
            var popupRect = new Rect(rect.x, rect.y, rect.width - SwatchSize - SwatchSpacing, rect.height);
            var swatchRect = new Rect(rect.xMax - SwatchSize, rect.y, SwatchSize, SwatchSize);

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _popupOptions.Length - 1);
            _selectedIndex = EditorGUI.Popup(popupRect, new GUIContent(label), _selectedIndex, _popupOptions);

            DrawSwatch(swatchRect, palette.GetColor(_selectedIndex));
            return true;
        }

        /// <summary>Draws a color swatch with a 1px black border.</summary>
        public static void DrawSwatch(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, color);
            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, 1), Color.black);
            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.yMax, rect.width + 2, 1), Color.black);
            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y, 1, rect.height), Color.black);
            EditorGUI.DrawRect(new Rect(rect.xMax, rect.y, 1, rect.height), Color.black);
        }

        private void EnsureOptions(IColorPalette palette)
        {
            if (_popupOptions != null && _cachedCount == palette.Count)
            {
                return;
            }

            _cachedCount = palette.Count;
            _popupOptions = new GUIContent[palette.Count];

            for (var i = 0; i < palette.Count; i++)
            {
                _popupOptions[i] = new GUIContent(palette.GetName(i));
            }
        }
    }
}

using BalloonParty.Configuration.Palette;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    /// <summary>
    ///     Material dropdown that picks a <see cref="GamePalette" /> colour by name and stores its palette
    ///     INDEX in the float property (or -1 for "None") — so a shader knob that means a palette slot
    ///     shows the named swatch list instead of a raw int.
    /// </summary>
    /// <remarks>Usage: <code>[PaletteIndex] _Prop ("Label", Float) = -1</code></remarks>
    public class PaletteIndexDrawer : MaterialPropertyDrawer
    {
        private const float SwatchSize = 16f;
        private const float SwatchSpacing = 4f;

        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            var palette = _paletteCache.Value;
            if (palette == null)
            {
                EditorGUI.HelpBox(position,
                    "No GamePalette asset found. Create one via Create → Configuration → Game Palette.",
                    MessageType.Warning);
                return;
            }

            var colors = palette.Colors;

            // Popup slot 0 = "None" (-1); slots 1..N map to palette indices 0..N-1.
            var options = new GUIContent[colors.Count + 1];
            options[0] = new GUIContent("None");
            for (var i = 0; i < colors.Count; i++)
            {
                options[i + 1] = new GUIContent(colors[i].Name);
            }

            var index = Mathf.RoundToInt(prop.floatValue);
            var current = index >= 0 && index < colors.Count ? index + 1 : 0;

            var popupRect = new Rect(position.x, position.y,
                position.width - SwatchSize - SwatchSpacing, position.height);
            var swatchRect = new Rect(position.xMax - SwatchSize, position.y, SwatchSize, SwatchSize);

            EditorGUI.BeginChangeCheck();
            var updated = EditorGUI.Popup(popupRect, label, current, options);
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = updated <= 0 ? -1f : updated - 1;
            }

            if (updated > 0 && updated - 1 < colors.Count)
            {
                PaletteColorPicker.DrawSwatch(swatchRect, colors[updated - 1].Color);
            }
        }
    }
}

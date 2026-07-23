using System.Linq;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.EditorUI.Palette;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(PaintProfile))]
    internal class PaintProfileDrawer : PropertyDrawer
    {
        private const float SwatchSize = 16f;
        private const float SwatchSpacing = 4f;

        private readonly EditorAssetCache<GamePalette> _paletteCache = new();

        private bool _initialized;
        private string[] _paletteNames;

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            var palette = _paletteCache.Value;
            _paletteNames = palette != null
                ? palette.Colors.Select(c => c.Name).ToArray()
                : System.Array.Empty<string>();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            // Header + Sources + Radius + Opacity + ColorMode = 5 lines
            var lines = 5;

            var colorMode = (PaintColorMode)property.FindPropertyRelative("ColorMode").enumValueIndex;
            if (colorMode == PaintColorMode.Palette || colorMode == PaintColorMode.Custom)
            {
                lines++;
            }

            return lineHeight * lines + spacing * (lines - 1);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureInitialized();

            EditorGUI.BeginProperty(position, label, property);

            var sources = (PaintSource)property.FindPropertyRelative("Sources").intValue;
            var foldoutLabel = sources != 0 ? sources.ToString() : "None";

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, foldoutLabel, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                var y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                var lineHeight = EditorGUIUtility.singleLineHeight;
                var spacing = EditorGUIUtility.standardVerticalSpacing;

                DrawField("Sources", "Sources", ref y);
                DrawField("Radius", "Radius", ref y);
                DrawField("Opacity", "Opacity", ref y);
                DrawField("ColorMode", "Color Mode", ref y);

                var colorMode = (PaintColorMode)property.FindPropertyRelative("ColorMode").enumValueIndex;

                switch (colorMode)
                {
                    case PaintColorMode.Palette:
                        DrawPaletteDropdown(position, property, ref y);
                        break;
                    case PaintColorMode.Custom:
                        DrawField("CustomColor", "Color", ref y);
                        break;
                }

                EditorGUI.indentLevel--;

                void DrawField(string fieldName, string displayName, ref float currentY)
                {
                    var prop = property.FindPropertyRelative(fieldName);
                    if (prop == null)
                    {
                        return;
                    }

                    var rect = new Rect(position.x, currentY, position.width, lineHeight);
                    EditorGUI.PropertyField(rect, prop, new GUIContent(displayName));
                    currentY += lineHeight + spacing;
                }
            }

            EditorGUI.EndProperty();
        }

        private void DrawPaletteDropdown(Rect position, SerializedProperty property, ref float y)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var nameProp = property.FindPropertyRelative("PaletteColorName");

            if (_paletteNames.Length == 0)
            {
                var helpRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.HelpBox(helpRect, "No GamePalette asset found.", MessageType.Warning);
                y += lineHeight + spacing;
                return;
            }

            var popupRect = new Rect(
                position.x, y,
                position.width - SwatchSize - SwatchSpacing, lineHeight);
            var swatchRect = new Rect(
                position.x + position.width - SwatchSize, y, SwatchSize, SwatchSize);

            var currentIndex = System.Array.IndexOf(_paletteNames, nameProp.stringValue);
            var options = _paletteNames.Select(n => new GUIContent(n)).ToArray();
            var newIndex = EditorGUI.Popup(popupRect, new GUIContent("Palette Color"), currentIndex, options);

            if (newIndex != currentIndex && newIndex >= 0)
            {
                nameProp.stringValue = _paletteNames[newIndex];
            }

            var palette = _paletteCache.Value;
            if (palette != null && newIndex >= 0 && newIndex < palette.Colors.Count)
            {
                PaletteColorPicker.DrawSwatch(swatchRect, palette.Colors[newIndex].Color);
            }

            y += lineHeight + spacing;
        }
    }
}

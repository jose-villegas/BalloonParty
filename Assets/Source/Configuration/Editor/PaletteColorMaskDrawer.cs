using System.Collections.Generic;
using System.Linq;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(PaletteColorMaskAttribute))]
    public class PaletteColorMaskDrawer : PropertyDrawer
    {
        private const float BoxPadding = 3f;

        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();

        private bool _initialized;
        private string[] _paletteNames;

        private GamePalette Palette => _paletteCache.Value;

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            var palette = Palette;
            _paletteNames = palette != null
                ? palette.Colors.Select(c => c.Name).ToArray()
                : System.Array.Empty<string>();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            EnsureInitialized();

            if (Palette == null || property.propertyType != SerializedPropertyType.Integer)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            var mask = property.intValue;
            var innerHeight = mask == 0
                ? EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight + 4f + CalculateSwatchHeight(mask,
                    EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth - 20f - (BoxPadding * 2f));

            return (BoxPadding * 2f) + innerHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureInitialized();

            if (Palette == null)
            {
                EditorGUI.HelpBox(position,
                    "No GamePalette asset found. Create one via Create → Configuration → Game Palette.",
                    MessageType.Warning);
                return;
            }

            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            var inner = new Rect(
                position.x + BoxPadding,
                position.y + BoxPadding,
                position.width - (BoxPadding * 2f),
                position.height - (BoxPadding * 2f));

            var maskRect = new Rect(inner.x, inner.y, inner.width, EditorGUIUtility.singleLineHeight);

            var newMask = EditorGUI.MaskField(maskRect, label, property.intValue, _paletteNames);

            if (newMask != property.intValue)
            {
                property.intValue = newMask;
            }

            if (newMask != 0)
            {
                var swatchY = maskRect.yMax + 4f;
                DrawSwatches(inner.x, swatchY, inner.width, newMask);
            }
        }

        private static float AvailableSwatchWidth(float propertyWidth)
        {
            // Use the property rect width minus a small margin so layout and
            // height calculation always agree, even inside nested array drawers.
            return propertyWidth - 8f;
        }

        private float CalculateSwatchHeight(int mask, float propertyWidth)
        {
            var swatchSize = 16f;
            var itemSpacing = 4f;
            var availableWidth = AvailableSwatchWidth(propertyWidth);
            var itemWidth = swatchSize + itemSpacing;

            var totalHeight = swatchSize;
            var x = 0f;

            for (var i = 0; i < _paletteNames.Length; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                if (x + itemWidth > availableWidth && x > 0f)
                {
                    x = 0f;
                    totalHeight += swatchSize + 4f;
                }

                x += itemWidth;
            }

            return totalHeight;
        }

        private void DrawSwatches(float startX, float startY, float width, int mask)
        {
            var swatchSize = 16f;
            var itemSpacing = 4f;
            var availableWidth = AvailableSwatchWidth(width);
            var itemWidth = swatchSize + itemSpacing;

            // Pre-build rows so each row can be right-aligned.
            var rows = new List<List<int>>();
            var currentRow = new List<int>();
            var x = 0f;

            for (var i = 0; i < _paletteNames.Length; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                if (x + itemWidth > availableWidth && currentRow.Count > 0)
                {
                    rows.Add(currentRow);
                    currentRow = new List<int>();
                    x = 0f;
                }

                currentRow.Add(i);
                x += itemWidth;
            }

            if (currentRow.Count > 0)
            {
                rows.Add(currentRow);
            }

            var curY = startY;

            foreach (var row in rows)
            {
                // Right-align: start so the last item ends at the available right edge.
                var rowWidth = (row.Count * itemWidth) - itemSpacing;
                var curX = startX + availableWidth - rowWidth;

                foreach (var i in row)
                {
                    var entry = Palette.Colors[i];
                    var swatchRect = new Rect(curX, curY, swatchSize, swatchSize);
                    PaletteColorPicker.DrawSwatch(swatchRect, entry.Color);
                    GUI.Label(swatchRect, new GUIContent(string.Empty, entry.Name), GUIStyle.none);
                    curX += itemWidth;
                }

                curY += swatchSize + 4f;
            }
        }
    }
}

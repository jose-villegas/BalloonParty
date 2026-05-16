using System.Linq;
using BalloonParty.Editor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(PaletteColorMaskAttribute))]
    public class PaletteColorMaskDrawer : PropertyDrawer
    {
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
            if (mask == 0)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight + 4f + CalculateSwatchHeight(mask);
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

            var maskRect = new Rect(position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);

            var newMask = EditorGUI.MaskField(maskRect, label, property.intValue, _paletteNames);

            if (newMask != property.intValue)
            {
                property.intValue = newMask;
            }

            if (newMask != 0)
            {
                var swatchY = maskRect.yMax + 4f;
                DrawSwatches(position.x, swatchY, position.width, newMask);
            }
        }

        private float CalculateSwatchHeight(int mask)
        {
            var swatchSize = 16f;
            var labelPadding = 4f;
            var itemSpacing = 8f;
            var availableWidth = EditorGUIUtility.currentViewWidth - 40f;
            var style = EditorStyles.miniLabel;

            var totalHeight = swatchSize;
            var x = 0f;

            for (var i = 0; i < _paletteNames.Length; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                var labelWidth = style.CalcSize(new GUIContent(_paletteNames[i])).x;
                var itemWidth = swatchSize + labelPadding + labelWidth + itemSpacing;

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
            var labelPadding = 4f;
            var itemSpacing = 8f;
            var availableWidth = width - 20f;
            var style = EditorStyles.miniLabel;

            var curX = startX;
            var curY = startY;

            for (var i = 0; i < _paletteNames.Length; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                var entry = Palette.Colors[i];
                var labelWidth = style.CalcSize(new GUIContent(entry.Name)).x;
                var itemWidth = swatchSize + labelPadding + labelWidth + itemSpacing;

                if (curX + itemWidth > startX + availableWidth && curX > startX)
                {
                    curX = startX;
                    curY += swatchSize + 4f;
                }

                var swatchRect = new Rect(curX, curY, swatchSize, swatchSize);
                PaletteColorPicker.DrawSwatch(swatchRect, entry.Color);

                var labelRect = new Rect(swatchRect.xMax + labelPadding, curY, labelWidth, swatchSize);
                EditorGUI.LabelField(labelRect, entry.Name, style);

                curX += itemWidth;
            }
        }
    }
}

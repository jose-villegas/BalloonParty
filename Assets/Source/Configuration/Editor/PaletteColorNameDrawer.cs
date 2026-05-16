using System.Linq;
using BalloonParty.Editor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(PaletteColorNameAttribute))]
    public class PaletteColorNameDrawer : PropertyDrawer
    {
        private const float SwatchSize = 16f;
        private const float SwatchSpacing = 4f;

        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();

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
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureInitialized();

            var palette = _paletteCache.Value;

            if (palette == null)
            {
                EditorGUI.HelpBox(position,
                    "No GamePalette asset found. Create one via Create → Configuration → Game Palette.",
                    MessageType.Warning);
                return;
            }

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var swatchRect = new Rect(position.xMax - SwatchSize, position.y, SwatchSize, SwatchSize);
            var popupRect = new Rect(position.x,
                position.y,
                position.width - SwatchSize - SwatchSpacing,
                position.height);

            var currentIndex = System.Array.IndexOf(_paletteNames, property.stringValue);
            var newIndex = EditorGUI.Popup(popupRect, label, currentIndex, BuildPopupOptions());

            if (newIndex != currentIndex && newIndex >= 0)
            {
                property.stringValue = _paletteNames[newIndex];
            }

            if (newIndex >= 0 && newIndex < palette.Colors.Length)
            {
                PaletteColorPicker.DrawSwatch(swatchRect, palette.Colors[newIndex].Color);
            }
        }

        private GUIContent[] BuildPopupOptions()
        {
            var options = new GUIContent[_paletteNames.Length];
            for (var i = 0; i < _paletteNames.Length; i++)
            {
                options[i] = new GUIContent(_paletteNames[i]);
            }

            return options;
        }
    }
}

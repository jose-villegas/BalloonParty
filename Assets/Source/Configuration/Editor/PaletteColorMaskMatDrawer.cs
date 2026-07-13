using System.Linq;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Configuration.Editor
{
    /// <summary>Material-property counterpart of <see cref="PaletteColorMaskDrawer" />: renders a float material
    /// property that holds a palette-colour bitmask as a multi-select of palette colours. Bit <c>i</c> = palette
    /// colour <c>i</c>. Shader usage: <c>[PaletteColorMaskMat] _IgnoreColorMask("Ignore Colours", Float) = 0</c>.</summary>
    public class PaletteColorMaskMatDrawer : MaterialPropertyDrawer
    {
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

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            EnsureInitialized();

            if (_paletteNames.Length == 0)
            {
                EditorGUI.HelpBox(position, "No GamePalette asset found.", MessageType.Warning);
                return;
            }

            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            var mask = EditorGUI.MaskField(position, label, (int)prop.floatValue, _paletteNames);
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = mask;
            }

            EditorGUI.showMixedValue = false;
        }
    }
}

using System.Linq;
using BalloonParty.Configuration.Palette;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    /// <summary>
    ///     Material drawer for a Float shader property: <c>[PaletteMask]</c> renders a multi-select of
    ///     the GamePalette colour names and stores the selection as a BITMASK (bit i = palette index i)
    ///     in the property's float. Shaders read the float and test bits — e.g. SceneLight.cginc's
    ///     masked light helpers, which drop light tagged with any excluded palette index.
    /// </summary>
    public class PaletteMaskDrawer : MaterialPropertyDrawer
    {
        private readonly EditorAssetCache<GamePalette> _paletteCache = new();

        private string[] _names;

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            var palette = _paletteCache.Value;
            if (palette == null || prop.propertyType != UnityEngine.Rendering.ShaderPropertyType.Float)
            {
                EditorGUI.HelpBox(position, "PaletteMask needs a Float property and a GamePalette asset.",
                    MessageType.Warning);
                return;
            }

            _names ??= palette.Colors.Select(c => c.Name).ToArray();

            EditorGUI.BeginChangeCheck();
            var newMask = EditorGUI.MaskField(position, label, (int)prop.floatValue, _names);
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = newMask;
            }
        }
    }
}

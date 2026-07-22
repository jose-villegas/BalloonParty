using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.EditorUI.Palette;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    [CustomEditor(typeof(ColorableBalloonVariant), true)]
    internal sealed class ColorableBalloonVariantEditor : UnityEditor.Editor
    {
        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();
        private readonly PaletteColorPicker _picker = new();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Color Preview", EditorStyles.boldLabel);

            if (!_picker.DrawLayout(new GamePaletteAdapter(_paletteCache.Value), "Preview Color"))
            {
                return;
            }

            if (GUILayout.Button("Apply Preview Color"))
            {
                var variant = (ColorableBalloonVariant)target;
                var renderers = variant.GetComponentsInChildren<ColorableRenderer>();

                foreach (var r in renderers)
                {
                    r.SetColor(_picker.GetSelectedColor(new GamePaletteAdapter(_paletteCache.Value)));
                    EditorUtility.SetDirty(r);
                }

                SceneView.RepaintAll();
            }
        }
    }
}

using BalloonParty.Balloon.Type;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Shared.Rendering;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    [CustomEditor(typeof(ColorableBalloonVariant), true)]
    internal sealed class ColorableBalloonVariantEditor : UnityEditor.Editor
    {
        private readonly PaletteColorPicker _picker = new();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Color Preview", EditorStyles.boldLabel);

            if (!_picker.DrawLayout("Preview Color"))
            {
                return;
            }

            if (GUILayout.Button("Apply Preview Color"))
            {
                var variant = (ColorableBalloonVariant)target;
                var renderers = variant.GetComponentsInChildren<ColorableRenderer>();

                foreach (var r in renderers)
                {
                    r.SetColor(_picker.SelectedColor);
                    EditorUtility.SetDirty(r);
                }

                SceneView.RepaintAll();
            }
        }
    }
}



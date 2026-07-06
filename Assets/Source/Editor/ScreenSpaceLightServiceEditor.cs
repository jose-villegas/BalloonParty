using BalloonParty.Display;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Live play-mode preview of the screen-space light buffer: RGB is bounce color, A is shadow amount.
    /// </summary>
    [CustomEditor(typeof(ScreenSpaceLightService))]
    internal sealed class ScreenSpaceLightServiceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var service = (ScreenSpaceLightService)target;
            var texture = service.LightTexture;

            EditorGUILayout.Space();

            if (texture == null)
            {
                EditorGUILayout.HelpBox(
                    Application.isPlaying
                        ? "No light buffer yet — it builds on the first rendered frame."
                        : "Light buffer preview is available in play mode.",
                    MessageType.Info);
                return;
            }

            var aspect = (float)texture.width / texture.height;

            EditorGUILayout.LabelField(
                $"Bounce color (RGB)  {texture.width} × {texture.height}", EditorStyles.boldLabel);
            EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect(aspect), texture);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Shadow amount (A)", EditorStyles.boldLabel);
            EditorGUI.DrawTextureAlpha(GUILayoutUtility.GetAspectRect(aspect), texture);

            Repaint();
        }
    }
}

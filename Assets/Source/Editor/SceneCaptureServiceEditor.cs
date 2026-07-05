using BalloonParty.Display;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Live play-mode preview of the shared scene-capture RT on the
    ///     <see cref="SceneCaptureService"/> inspector — for judging the downscale by eye.
    /// </summary>
    [CustomEditor(typeof(SceneCaptureService))]
    internal sealed class SceneCaptureServiceEditor : UnityEditor.Editor
    {
        private static Material _opaquePreviewMaterial;

        // The capture's alpha is a coverage mask, 0 almost everywhere — DrawPreviewTexture
        // alpha-blends against the Inspector's background, so the RGB it's masking (the
        // actual sky color) would otherwise never be visible. This material has no blend
        // state, so it just replaces with the texture's RGB, ignoring alpha entirely.
        private static Material OpaquePreviewMaterial =>
            _opaquePreviewMaterial ??= new Material(Shader.Find("Unlit/Texture"));

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var service = (SceneCaptureService)target;
            var texture = service.CaptureTexture;

            EditorGUILayout.Space();

            if (texture == null)
            {
                EditorGUILayout.HelpBox(
                    Application.isPlaying
                        ? "No capture yet — it renders once a consumer acquires it (e.g. spawn an Unbreakable)."
                        : "Capture preview is available in play mode.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Capture  {texture.width} × {texture.height}", EditorStyles.boldLabel);

            var aspect = (float)texture.width / texture.height;
            var rect = GUILayoutUtility.GetAspectRect(aspect);
            EditorGUI.DrawPreviewTexture(rect, texture, OpaquePreviewMaterial);

            // Keep the preview live while the game runs.
            Repaint();
        }
    }
}

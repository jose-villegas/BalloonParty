using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Disturbance
{
    /// <summary>
    ///     Live play-mode preview of the shared disturbance field. Reads the RT that
    ///     <c>DisturbanceFieldResources</c> binds globally as <c>_DisturbanceTex</c> — no
    ///     dependency on the (internal, VContainer-scoped) service itself.
    /// </summary>
    internal sealed class DisturbanceFieldPreviewWindow : EditorWindow
    {
        private static readonly int DisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");

        [MenuItem("Tools/BalloonParty/Disturbance Field Preview")]
        private static void Open()
        {
            GetWindow<DisturbanceFieldPreviewWindow>("Disturbance Field");
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            var texture = Shader.GetGlobalTexture(DisturbanceTexId) as RenderTexture;

            if (!Application.isPlaying || texture == null)
            {
                EditorGUILayout.HelpBox(
                    "No disturbance field bound — enter play mode; the field binds once the game scope starts.",
                    MessageType.Info);
                return;
            }

            // Raw R=density, G/B=displacement (biased ±0.5) — equilibrium reads as a flat
            // pinkish tint; density drops toward black, displacement tints green/blue.
            var aspect = (float)texture.width / texture.height;
            EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect(aspect), texture);

            EditorGUILayout.LabelField($"{texture.width} × {texture.height}");
        }
    }
}

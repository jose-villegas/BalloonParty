using BalloonParty.Editor;
using BalloonParty.Editor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    /// <summary>
    ///     Draws the map limits rectangle in the Scene view whenever a
    ///     <see cref="GameConfiguration"/> asset exists in the project.
    ///     Active in both edit and play mode, regardless of inspector selection.
    /// </summary>
    [InitializeOnLoad]
    internal static class MapLimitsSceneOverlay
    {
        private const string EditorPrefKey = "BalloonParty.ShowMapLimits";

        private static readonly ConfigAssetCache<GameConfiguration> ConfigCache = new();
        private static readonly Color OutlineColor = new(1f, 0.6f, 0.2f, 0.9f);
        private static readonly Color FillColor = new(1f, 0.6f, 0.2f, 0.06f);

        internal static bool ShowInScene
        {
            get => EditorPrefs.GetBool(EditorPrefKey, true);
            set => EditorPrefs.SetBool(EditorPrefKey, value);
        }

        static MapLimitsSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!ShowInScene)
            {
                return;
            }

            var config = ConfigCache.Value;
            if (config == null)
            {
                return;
            }

            var limits = config.LimitsClockwise;
            var top = limits.x;
            var right = limits.y;
            var bottom = limits.z;
            var left = limits.w;

            if (Mathf.Approximately(right - left, 0f) || Mathf.Approximately(top - bottom, 0f))
            {
                return;
            }

            SceneDrawingHelper.DrawWorldRectFromLimits(top,
                right,
                bottom,
                left,
                OutlineColor,
                FillColor);

            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = OutlineColor }
            };

            var centerX = (left + right) / 2f;
            Handles.Label(
                new Vector3(centerX, top + 0.3f, 0f),
                "Map Limits",
                labelStyle);

            var edgeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = OutlineColor }
            };

            Handles.Label(new Vector3(centerX, top + 0.1f, 0f),
                $"T {top:F2}",
                edgeLabelStyle);
            Handles.Label(new Vector3(right + 0.1f, (top + bottom) / 2f, 0f),
                $"R {right:F2}",
                edgeLabelStyle);
            Handles.Label(new Vector3(centerX, bottom - 0.2f, 0f),
                $"B {bottom:F2}",
                edgeLabelStyle);
            Handles.Label(new Vector3(left - 0.5f, (top + bottom) / 2f, 0f),
                $"L {left:F2}",
                edgeLabelStyle);
        }
    }

    [CustomEditor(typeof(GameConfiguration))]
    internal class GameConfigurationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            var show = EditorGUILayout.Toggle("Show Limits In Scene", MapLimitsSceneOverlay.ShowInScene);
            if (show != MapLimitsSceneOverlay.ShowInScene)
            {
                MapLimitsSceneOverlay.ShowInScene = show;
                SceneView.RepaintAll();
            }
        }
    }
}

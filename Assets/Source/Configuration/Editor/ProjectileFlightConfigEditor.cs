using System.Collections.Generic;
using BalloonParty.Editor;
using BalloonParty.EditorUI.Utilities;
using BalloonParty.Shared;
using BalloonParty.Solver;
using BalloonParty.Thrower;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    /// <summary>
    ///     Draws the map limits rectangle in the Scene view whenever a <see cref="ProjectileFlightConfig"/> asset exists.
    /// </summary>
    [InitializeOnLoad]
    internal static class MapLimitsSceneOverlay
    {
        private const string EditorPrefKey = "BalloonParty.ShowMapLimits";

        private static readonly EditorAssetCache<ProjectileFlightConfig> ConfigCache = new();
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

            var labelStyle = StyleCache.Get("MapLimitsSceneOverlay.Label", () => new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = OutlineColor }
            });

            var centerX = (left + right) / 2f;
            Handles.Label(
                new Vector3(centerX, top + 0.3f, 0f),
                "Map Limits",
                labelStyle);

            var edgeLabelStyle = StyleCache.Get("MapLimitsSceneOverlay.EdgeLabel", () => new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = OutlineColor }
            });

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

    /// <summary>
    ///     Draws a ray per reachable aim angle from the thrower's launch position, clipped to the map
    ///     limits, whenever a <see cref="ProjectileFlightConfig"/> asset and a live
    ///     <see cref="ThrowerView"/> both exist in the open scene.
    /// </summary>
    /// <remarks>
    ///     Exists because the board is a portrait rectangle and the thrower fires up its long axis:
    ///     equal ANGULAR steps do not land as equal BOARD spacing. Rays near vertical run the long
    ///     axis and land far apart near the top wall, while rays toward the sides hit the near side
    ///     walls and bunch up. Clipping each ray to where it actually meets a wall — rather than
    ///     drawing equal-length rays — is what makes that visible; an unclipped fan would look evenly
    ///     spaced regardless of the effect. Only draws for a quantized aim
    ///     (<see cref="IProjectileFlightConfig.AimAngleStepDegrees"/> &gt; 0): continuous aim reaches
    ///     infinitely many angles, so there is no finite fan to draw.
    /// </remarks>
    [InitializeOnLoad]
    internal static class AimFanSceneOverlay
    {
        private const string EditorPrefKey = "BalloonParty.ShowAimFan";

        private static readonly EditorAssetCache<ProjectileFlightConfig> ConfigCache = new();
        private static readonly Color RayColor = new(0.25f, 0.75f, 1f, 0.6f);
        private static readonly List<Vector3> RayBuffer = new(2) { Vector3.zero, Vector3.zero };

        private static ThrowerView _cachedThrower;

        internal static bool ShowInScene
        {
            get => EditorPrefs.GetBool(EditorPrefKey, false);
            set => EditorPrefs.SetBool(EditorPrefKey, value);
        }

        static AimFanSceneOverlay()
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
            if (config == null || config.AimAngleStepDegrees <= 0f)
            {
                return;
            }

            // Cached rather than re-found every Scene-view repaint; a destroyed/unloaded reference
            // compares equal to null (Unity's overridden Object equality), so a scene change or exit
            // from Play mode correctly triggers a re-find on the next repaint.
            if (_cachedThrower == null)
            {
                _cachedThrower = UnityEngine.Object.FindFirstObjectByType<ThrowerView>();
            }

            if (_cachedThrower == null)
            {
                return;
            }

            DrawFan(config, _cachedThrower);
        }

        // One clipped ray per angle the quantized aim can actually reach — derived from the exact
        // ResolveSweepSampleCount/ResolveSweepAngle helpers the solver sweeps with, over the same
        // config-sourced range (IProjectileFlightConfig.AimAngleMinDegrees/AimAngleMaxDegrees) the
        // thrower clamps to and FireBestShotCheat sweeps, so the picture can never diverge from what
        // those — and the player's own aim clamp — consider reachable.
        private static void DrawFan(ProjectileFlightConfig config, ThrowerView thrower)
        {
            var stepDegrees = config.AimAngleStepDegrees;
            var sampleCount = ShotBoardGather.ResolveSweepSampleCount(
                config.AimAngleMinDegrees, config.AimAngleMaxDegrees, stepDegrees, 1);

            // The spawn point, not the pivot transform — where a shot actually leaves from (same
            // reasoning as ThrowerOriginProvider). One snapshot origin shared by every ray, not
            // re-rotated per angle the way the solver's OriginForAngle orbits it around the pivot:
            // that orbit is sub-pixel next to the fan itself and would only add per-ray origin jitter
            // to the picture.
            var origin = thrower.SpawnPointPosition;
            var walls = new WallLimits(config.LimitsClockwise);

            for (var i = 0; i < sampleCount; i++)
            {
                var angleDegrees = ShotBoardGather.ResolveSweepAngle(
                    i, config.AimAngleMinDegrees, config.AimAngleMaxDegrees, stepDegrees, 1);
                var direction = (Vector3)ShotBoardGather.DirectionFromDegrees(angleDegrees);
                if (!walls.TryFindCrossing(origin, direction, out var crossing, out _))
                {
                    continue;
                }

                RayBuffer[0] = origin;
                RayBuffer[1] = crossing;
                SceneDrawingHelper.DrawWorldPolyline(RayBuffer, RayColor);
            }
        }
    }

    [CustomEditor(typeof(ProjectileFlightConfig))]
    internal class ProjectileFlightConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesWithAimRangeSlider();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            var show = EditorGUILayout.Toggle("Show Limits In Scene", MapLimitsSceneOverlay.ShowInScene);
            if (show != MapLimitsSceneOverlay.ShowInScene)
            {
                MapLimitsSceneOverlay.ShowInScene = show;
                SceneView.RepaintAll();
            }

            var showFan = EditorGUILayout.Toggle("Show Aim Fan In Scene", AimFanSceneOverlay.ShowInScene);
            if (showFan != AimFanSceneOverlay.ShowInScene)
            {
                AimFanSceneOverlay.ShowInScene = showFan;
                SceneView.RepaintAll();
            }
        }

        // Same walk DrawDefaultInspector does (m_Script disabled, every visible child in order), except
        // the two aim-angle bounds collapse into one DrawMinMaxSliderLayout in the Min slot — the
        // runtime interface still exposes them as two separate float properties (see OnValidate's
        // min-below-max guard on ProjectileFlightConfig), only the inspector presentation changes.
        private void DrawPropertiesWithAimRangeSlider()
        {
            var minProp = serializedObject.FindProperty("_aimAngleMinDegrees");
            var maxProp = serializedObject.FindProperty("_aimAngleMaxDegrees");

            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name == minProp.name)
                {
                    DrawAimAngleRangeSlider(minProp, maxProp);
                    continue;
                }

                if (iterator.name == maxProp.name)
                {
                    continue;
                }

                using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
        }

        private static void DrawAimAngleRangeSlider(SerializedProperty minProp, SerializedProperty maxProp)
        {
            var range = new Vector2(minProp.floatValue, maxProp.floatValue);
            PropertyDrawerHelper.DrawMinMaxSliderLayout("Aim Angle Range (°)", ref range, 0f, 180f);
            minProp.floatValue = range.x;
            maxProp.floatValue = range.y;
        }
    }
}

using BalloonParty.Editor;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomEditor(typeof(GameDisplayConfiguration))]
    public class GameDisplayConfigurationEditor : UnityEditor.Editor
    {
        private const float PreviewBoxHeight = 120f;

        private static readonly (string Label, float Aspect)[] CommonRatios =
        {
            ("9:21  (tall phone)", 9f / 21f),
            ("9:19.5 (iPhone 14)", 9f / 19.5f),
            ("9:18  (Galaxy S)", 9f / 18f),
            ("9:16  (standard)", 9f / 16f),
            ("3:4   (iPad)", 3f / 4f),
            ("1:1   (square)", 1f)
        };

        private static readonly Color ReferenceBoxColor = new(0.4f, 0.7f, 1f, 0.9f);
        private static readonly Color ReferenceBoxFill = new(0.25f, 0.4f, 0.6f, 0.08f);

        private bool _showInScene = true;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var widthProp = serializedObject.FindProperty("_referenceWorldWidth");
            var heightProp = serializedObject.FindProperty("_referenceWorldHeight");

            EditorGUILayout.PropertyField(widthProp, new GUIContent("Reference World Width"));
            EditorGUILayout.PropertyField(heightProp, new GUIContent("Reference World Height"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Capture", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_sceneCaptureDownscale"),
                new GUIContent("Downscale"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_sceneCaptureFrameInterval"),
                new GUIContent("Frame Interval"));

            serializedObject.ApplyModifiedProperties();

            var refWidth = widthProp.floatValue;
            var refHeight = heightProp.floatValue;

            EditorGUILayout.Space();
            _showInScene = EditorGUILayout.Toggle("Show In Scene", _showInScene);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ortho Size Preview", EditorStyles.boldLabel);

            foreach (var (ratioLabel, aspect) in CommonRatios)
            {
                var sizeW = refWidth / (2f * aspect);
                var sizeH = refHeight / 2f;
                var ortho = Mathf.Max(sizeW, sizeH);
                var fitsBy = sizeW > sizeH ? "width" : "height";
                EditorGUILayout.LabelField(
                    $"  {ratioLabel}",
                    $"ortho = {ortho:F2}  (fits {fitsBy})");
            }

            EditorGUILayout.Space();
            var boxRect = GUILayoutUtility.GetRect(GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(PreviewBoxHeight));
            DrawPreviewBox(boxRect, refWidth, refHeight);

            if (_showInScene)
            {
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_showInScene)
            {
                return;
            }

            var widthProp = serializedObject.FindProperty("_referenceWorldWidth");
            var heightProp = serializedObject.FindProperty("_referenceWorldHeight");
            var refWidth = widthProp.floatValue;
            var refHeight = heightProp.floatValue;

            if (refWidth <= 0 || refHeight <= 0)
            {
                return;
            }

            var center = Vector3.zero;

            SceneDrawingHelper.DrawWorldRect(center, refWidth, refHeight, ReferenceBoxColor, ReferenceBoxFill);

            foreach (var (label, aspect) in CommonRatios)
            {
                var ortho = Mathf.Max(refWidth / (2f * aspect), refHeight / 2f);
                var visibleH = ortho * 2f;
                var visibleW = visibleH * aspect;

                var deviceColor = new Color(1f, 1f, 1f, 0.15f);
                SceneDrawingHelper.DrawWorldRect(center, visibleW, visibleH, deviceColor, Color.clear);

                var labelPos = center + new Vector3(visibleW / 2f, visibleH / 2f, 0f);
                Handles.Label(labelPos,
                    label,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(1f, 1f, 1f, 0.5f) }
                    });
            }

            var refLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ReferenceBoxColor }
            };
            Handles.Label(center + new Vector3(0f, (refHeight / 2f) + 0.3f, 0f),
                $"Reference  {refWidth} × {refHeight}",
                refLabelStyle);
        }

        private static void DrawPreviewBox(Rect area, float refWidth, float refHeight)
        {
            EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f));

            if (refWidth <= 0 || refHeight <= 0)
            {
                return;
            }

            var margin = 10f;
            var innerW = area.width - (margin * 2);
            var innerH = area.height - (margin * 2);

            var scale = Mathf.Min(innerW / refWidth, innerH / refHeight);
            var scaledW = refWidth * scale;
            var scaledH = refHeight * scale;

            var refRect = new Rect(
                area.x + ((area.width - scaledW) / 2f),
                area.y + ((area.height - scaledH) / 2f),
                scaledW,
                scaledH);

            EditorGUI.DrawRect(refRect, new Color(0.25f, 0.4f, 0.6f, 0.3f));
            DrawRectOutline(refRect, new Color(0.4f, 0.7f, 1f, 0.8f));

            var centerX = refRect.x + (refRect.width / 2f);
            var centerY = refRect.y + (refRect.height / 2f);

            foreach (var (_, aspect) in CommonRatios)
            {
                var ortho = Mathf.Max(refWidth / (2f * aspect), refHeight / 2f);
                var visibleH = ortho * 2f;
                var visibleW = visibleH * aspect;

                var devW = visibleW * scale;
                var devH = visibleH * scale;

                var devRect = new Rect(
                    centerX - (devW / 2f),
                    centerY - (devH / 2f),
                    devW,
                    devH);

                DrawRectOutline(devRect, new Color(1f, 1f, 1f, 0.25f));
            }

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.85f, 1f) }
            };
            GUI.Label(refRect, $"{refWidth} × {refHeight}", style);
        }

        private static void DrawRectOutline(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), color);
        }
    }
}

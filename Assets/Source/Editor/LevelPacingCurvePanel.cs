using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Draws the scoring curve section in <see cref="LevelPacingWindow"/>. Clicking a bar selects
    /// that level and shows its detail; if the level has no control point a button offers to add one.
    /// A range slider controls the visible levels and an input row allows adding custom CPs.</summary>
    internal static class LevelPacingCurvePanel
    {
        private const float GraphHeight = 180f;
        private const float Padding = 8f;
        private const float AxisLabelWidth = 50f;
        private const string FoldoutPrefKey = "LevelPacingCurvePanel_Foldout";

        private static readonly Color CumulativeColor = new(0.4f, 0.7f, 1f, 1f);
        private static readonly Color PerColorColor = new(0.2f, 0.9f, 0.4f, 1f);
        private static readonly Color GridColor = new(0.3f, 0.3f, 0.3f, 0.5f);
        private static readonly Color SelectedColor = new(1f, 0.8f, 0.2f, 0.8f);
        private static readonly Color ControlPointColor = new(1f, 0.4f, 0.3f, 1f);

        private static int _selectedLevel = 1;
        private static int _rangeFrom = 1;
        private static int _rangeTo = 30;
        private static int _addLevel = 1;
        private static float _addCumulative;
        private static bool _showTotal = true;

        /// <summary>The level currently selected/viewed in the curve panel.</summary>
        internal static int SelectedLevel => _selectedLevel;

        /// <summary>Draws the scoring curve section. Requires a <see cref="SerializedObject"/> for the asset
        /// to support adding control points with undo.</summary>
        internal static void Draw(LevelPacingConfiguration asset, SerializedObject serialized)
        {
            if (asset == null)
            {
                return;
            }

            var foldout = EditorPrefs.GetBool(FoldoutPrefKey, true);
            foldout = EditorGUILayout.Foldout(foldout, "Scoring Curve", true, EditorStyles.foldoutHeader);
            EditorPrefs.SetBool(FoldoutPrefKey, foldout);

            if (!foldout)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawRangeControls(serialized);

            var levelCount = _rangeTo - _rangeFrom + 1;
            if (levelCount < 1)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            // Sample thresholds and capped cumulatives.
            var thresholds = new int[levelCount];
            var cumulatives = new float[levelCount];
            var maxThreshold = 1f;
            var maxCumulative = 1f;

            var runningCumulative = 0f;
            for (var i = 0; i < levelCount; i++)
            {
                var level = _rangeFrom + i;
                var perColor = asset.ThresholdForLevel(level);
                var colors = asset.ColorsForLevel(level);
                thresholds[i] = _showTotal ? perColor * colors : perColor;
                runningCumulative += perColor * colors;
                cumulatives[i] = runningCumulative;

                if (thresholds[i] > maxThreshold)
                {
                    maxThreshold = thresholds[i];
                }

                if (cumulatives[i] > maxCumulative)
                {
                    maxCumulative = cumulatives[i];
                }
            }

            // Graph area.
            var graphRect = GUILayoutUtility.GetRect(0f, GraphHeight, GUILayout.ExpandWidth(true));
            graphRect.x += Padding;
            graphRect.width -= Padding * 2f;

            if (graphRect.width < 100f || Event.current.type == EventType.Layout)
            {
                DrawSelectedLevelInfo(asset, serialized, levelCount);
                DrawTailConfig(serialized);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            var plotRect = new Rect(
                graphRect.x + AxisLabelWidth,
                graphRect.y + Padding,
                graphRect.width - AxisLabelWidth - Padding,
                graphRect.height - Padding * 2f - 16f);

            DrawGrid(plotRect, maxThreshold);
            DrawBars(plotRect, thresholds, maxThreshold, levelCount);
            DrawControlPointMarkers(plotRect, serialized, levelCount);
            DrawCumulativeLine(plotRect, cumulatives, maxCumulative, levelCount);
            DrawAxisLabels(graphRect, plotRect, maxThreshold);
            DrawLevelLabels(plotRect, levelCount);
            HandleClick(plotRect, levelCount);
            DrawLegend(new Rect(graphRect.x, graphRect.yMax - 14f, graphRect.width, 14f));

            DrawSelectedLevelInfo(asset, serialized, levelCount);
            DrawTailConfig(serialized);

            EditorGUILayout.EndVertical();
        }

        private static void DrawRangeControls(SerializedObject serialized)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Range", GUILayout.Width(42f));
            _rangeFrom = EditorGUILayout.IntField(_rangeFrom, GUILayout.Width(40f));
            EditorGUILayout.LabelField("–", GUILayout.Width(12f));
            _rangeTo = EditorGUILayout.IntField(_rangeTo, GUILayout.Width(40f));
            _rangeFrom = Mathf.Max(1, _rangeFrom);
            _rangeTo = Mathf.Max(_rangeFrom, _rangeTo);

            GUILayout.Space(12f);
            _showTotal = GUILayout.Toggle(_showTotal, _showTotal ? "Total" : "Per-color",
                EditorStyles.miniButton, GUILayout.Width(64f));

            GUILayout.Space(12f);
            EditorGUILayout.LabelField("CP:", GUILayout.Width(22f));
            _addLevel = EditorGUILayout.IntField(_addLevel, GUILayout.Width(32f));
            _addLevel = Mathf.Max(1, _addLevel);
            _addCumulative = EditorGUILayout.FloatField(_addCumulative, GUILayout.Width(56f));
            if (GUILayout.Button("+", GUILayout.Width(20f)))
            {
                AddControlPoint(serialized, _addLevel, _addCumulative);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSelectedLevelInfo(
            LevelPacingConfiguration asset, SerializedObject serialized, int levelCount)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Navigation header: ◀ Level [N] ▶
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _selectedLevel > 1;
            if (GUILayout.Button("◀", GUILayout.Width(24f)))
            {
                _selectedLevel--;
                GUIUtility.keyboardControl = 0;
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel, GUILayout.Width(38f));
            var inputLevel = EditorGUILayout.IntField(_selectedLevel, GUILayout.Width(48f));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedLevel = Mathf.Max(1, inputLevel);
                GUIUtility.keyboardControl = 0;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("▶", GUILayout.Width(24f)))
            {
                _selectedLevel++;
                GUIUtility.keyboardControl = 0;
            }

            EditorGUILayout.EndHorizontal();

            if (_selectedLevel < 1)
            {
                EditorGUILayout.LabelField("Select a level or enter one above.", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            var threshold = asset.ThresholdForLevel(_selectedLevel);
            var colors = asset.ColorsForLevel(_selectedLevel);
            var cumulative = asset.ScoringCurve.CumulativeMilestone(_selectedLevel);
            var cumPrev = asset.ScoringCurve.CumulativeMilestone(_selectedLevel - 1);
            var rawPerColor = (cumulative - cumPrev) / colors;
            var cpIndex = FindControlPointIndex(serialized, _selectedLevel);

            // Capped = sum of actual rounded thresholds × colors up to this level.
            var capped = 0;
            for (var l = 1; l <= _selectedLevel; l++)
            {
                capped += asset.ThresholdForLevel(l) * asset.ColorsForLevel(l);
            }

            EditorGUILayout.LabelField($"Cumulative: {capped}  ({cumulative:F1})");
            EditorGUILayout.LabelField($"Per-color threshold: {threshold}  ({rawPerColor:F1})");
            EditorGUILayout.LabelField($"Colors: {colors}  |  Total: {threshold * colors}");

            if (cpIndex >= 0)
            {
                var pointsProp = GetControlPointsProperty(serialized);
                var element = pointsProp.GetArrayElementAtIndex(cpIndex);
                var cumProp = element.FindPropertyRelative("_cumulativeScore");
                var modeProp = element.FindPropertyRelative("_segmentMode");

                EditorGUILayout.Space(2f);
                EditorGUI.BeginChangeCheck();
                var newCum = EditorGUILayout.FloatField("Cumulative Score (CP)", cumProp.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    cumProp.floatValue = newCum;
                    serialized.ApplyModifiedProperties();
                }

                EditorGUI.BeginChangeCheck();
                var newMode = (SegmentMode)EditorGUILayout.EnumPopup("Segment Mode", (SegmentMode)modeProp.enumValueIndex);
                if (EditorGUI.EndChangeCheck())
                {
                    modeProp.enumValueIndex = (int)newMode;
                    serialized.ApplyModifiedProperties();
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove Control Point", GUILayout.Width(160f)))
                {
                    pointsProp.DeleteArrayElementAtIndex(cpIndex);
                    serialized.ApplyModifiedProperties();
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("No control point at this level.", EditorStyles.miniLabel);
                if (GUILayout.Button($"Add Control Point at Level {_selectedLevel}"))
                {
                    AddControlPoint(serialized, _selectedLevel, EstimateCumulative(asset, _selectedLevel));
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawBars(Rect plotRect, int[] values, float maxValue, int levelCount)
        {
            var barWidth = plotRect.width / levelCount;

            for (var i = 0; i < values.Length; i++)
            {
                var level = _rangeFrom + i;
                var isSelected = level == _selectedLevel;
                var barColor = isSelected
                    ? new Color(SelectedColor.r, SelectedColor.g, SelectedColor.b, 0.7f)
                    : new Color(PerColorColor.r, PerColorColor.g, PerColorColor.b, 0.5f);

                var normalized = values[i] / maxValue;
                var barHeight = normalized * plotRect.height;
                var barRect = new Rect(
                    plotRect.x + i * barWidth + 1f,
                    plotRect.y + plotRect.height - barHeight,
                    barWidth - 2f,
                    barHeight);
                EditorGUI.DrawRect(barRect, barColor);
            }
        }

        private static void DrawControlPointMarkers(Rect plotRect, SerializedObject serialized, int levelCount)
        {
            var barWidth = plotRect.width / levelCount;
            var pointsProp = GetControlPointsProperty(serialized);

            if (pointsProp == null)
            {
                return;
            }

            for (var i = 0; i < pointsProp.arraySize; i++)
            {
                var element = pointsProp.GetArrayElementAtIndex(i);
                var cpLevel = element.FindPropertyRelative("_level").intValue;

                if (cpLevel < _rangeFrom || cpLevel > _rangeTo)
                {
                    continue;
                }

                var index = cpLevel - _rangeFrom;
                var x = plotRect.x + (index + 0.5f) * barWidth;

                // Diamond marker at top.
                Handles.color = ControlPointColor;
                var markerY = plotRect.y - 2f;
                var diamond = new Vector3[]
                {
                    new(x, markerY - 5f, 0f),
                    new(x + 4f, markerY, 0f),
                    new(x, markerY + 5f, 0f),
                    new(x - 4f, markerY, 0f),
                    new(x, markerY - 5f, 0f),
                };
                Handles.DrawAAPolyLine(2f, diamond);
            }
        }

        private static void DrawCumulativeLine(Rect plotRect, float[] cumulatives, float maxCumulative, int levelCount)
        {
            if (cumulatives.Length < 2)
            {
                return;
            }

            var points = new Vector3[cumulatives.Length];
            var barWidth = plotRect.width / levelCount;

            for (var i = 0; i < cumulatives.Length; i++)
            {
                var x = plotRect.x + (i + 0.5f) * barWidth;
                var y = plotRect.y + plotRect.height * (1f - cumulatives[i] / maxCumulative);
                points[i] = new Vector3(x, y, 0f);
            }

            Handles.color = CumulativeColor;
            Handles.DrawAAPolyLine(2f, points);
        }

        private static void DrawGrid(Rect plotRect, float maxValue)
        {
            const int gridLines = 4;
            for (var i = 1; i < gridLines; i++)
            {
                var y = plotRect.y + plotRect.height * (1f - (float)i / gridLines);
                Handles.color = GridColor;
                Handles.DrawLine(new Vector3(plotRect.x, y), new Vector3(plotRect.xMax, y));
            }
        }

        private static void DrawAxisLabels(Rect graphRect, Rect plotRect, float maxValue)
        {
            var axisLabelStyle = StyleCache.Get("LevelPacingCurvePanel.AxisLabel", () => new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.gray },
            });

            const int gridLines = 4;
            for (var i = 0; i <= gridLines; i++)
            {
                var y = plotRect.y + plotRect.height * (1f - (float)i / gridLines);
                var value = maxValue * i / gridLines;
                var labelRect = new Rect(graphRect.x, y - 7f, AxisLabelWidth - 4f, 14f);
                GUI.Label(labelRect, Mathf.RoundToInt(value).ToString(), axisLabelStyle);
            }
        }

        private static void DrawLevelLabels(Rect plotRect, int levelCount)
        {
            var levelLabelStyle = StyleCache.Get("LevelPacingCurvePanel.LevelLabel", () => new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.gray },
            });

            var barWidth = plotRect.width / levelCount;
            var step = Mathf.Max(1, levelCount / 10);

            for (var i = 0; i < levelCount; i += step)
            {
                var x = plotRect.x + (i + 0.5f) * barWidth;
                var labelRect = new Rect(x - 15f, plotRect.yMax + 1f, 30f, 14f);
                GUI.Label(labelRect, (_rangeFrom + i).ToString(), levelLabelStyle);
            }
        }

        private static void HandleClick(Rect plotRect, int levelCount)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
            {
                return;
            }

            if (!plotRect.Contains(Event.current.mousePosition))
            {
                return;
            }

            var barWidth = plotRect.width / levelCount;
            var index = Mathf.Clamp(
                (int)((Event.current.mousePosition.x - plotRect.x) / barWidth), 0, levelCount - 1);
            _selectedLevel = _rangeFrom + index;
            GUIUtility.keyboardControl = 0;
            Event.current.Use();
        }

        private static void DrawLegend(Rect rect)
        {
            var legendStyle = StyleCache.Get("LevelPacingCurvePanel.LegendLabel", () => new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.gray },
            });

            var x = rect.x + AxisLabelWidth;
            EditorGUI.DrawRect(new Rect(x, rect.y + 4f, 12f, 6f), PerColorColor);
            GUI.Label(new Rect(x + 14f, rect.y, 80f, 14f), _showTotal ? "Total" : "Per-color", legendStyle);

            x += 90f;
            EditorGUI.DrawRect(new Rect(x, rect.y + 4f, 12f, 3f), CumulativeColor);
            GUI.Label(new Rect(x + 14f, rect.y, 80f, 14f), "Cumulative", legendStyle);

            x += 100f;
            Handles.color = ControlPointColor;
            Handles.DrawAAPolyLine(2f,
                new Vector3(x + 3f, rect.y + 2f, 0f),
                new Vector3(x + 6f, rect.y + 7f, 0f),
                new Vector3(x + 3f, rect.y + 12f, 0f),
                new Vector3(x, rect.y + 7f, 0f),
                new Vector3(x + 3f, rect.y + 2f, 0f));
            GUI.Label(new Rect(x + 10f, rect.y, 90f, 14f), "Control Point", legendStyle);
        }

        private static int FindControlPointIndex(SerializedObject serialized, int level)
        {
            var pointsProp = GetControlPointsProperty(serialized);
            if (pointsProp == null)
            {
                return -1;
            }

            for (var i = 0; i < pointsProp.arraySize; i++)
            {
                var element = pointsProp.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("_level").intValue == level)
                {
                    return i;
                }
            }

            return -1;
        }

        private static SerializedProperty GetControlPointsProperty(SerializedObject serialized)
        {
            return serialized.FindProperty("_scoringCurve._controlPoints");
        }

        private static void AddControlPoint(SerializedObject serialized, int level, float cumulative)
        {
            var pointsProp = GetControlPointsProperty(serialized);
            if (pointsProp == null)
            {
                return;
            }

            // Insert sorted by level.
            var insertAt = pointsProp.arraySize;
            for (var i = 0; i < pointsProp.arraySize; i++)
            {
                var element = pointsProp.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("_level").intValue >= level)
                {
                    insertAt = i;
                    break;
                }
            }

            pointsProp.InsertArrayElementAtIndex(insertAt);
            var newElement = pointsProp.GetArrayElementAtIndex(insertAt);
            newElement.FindPropertyRelative("_level").intValue = level;
            newElement.FindPropertyRelative("_cumulativeScore").floatValue = cumulative;
            newElement.FindPropertyRelative("_segmentMode").enumValueIndex = (int)SegmentMode.Smooth;
            serialized.ApplyModifiedProperties();
        }

        private static void DrawTailConfig(SerializedObject serialized)
        {
            var tailProp = serialized.FindProperty("_scoringCurve._tailGrowth");
            if (tailProp == null)
            {
                return;
            }

            var modeProp = tailProp.FindPropertyRelative("_mode");
            var rateProp = tailProp.FindPropertyRelative("_rate");

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tail:", EditorStyles.boldLabel, GUILayout.Width(34f));

            EditorGUI.BeginChangeCheck();
            var newMode = (TailGrowthMode)EditorGUILayout.EnumPopup((TailGrowthMode)modeProp.enumValueIndex, GUILayout.Width(90f));
            if (EditorGUI.EndChangeCheck())
            {
                modeProp.enumValueIndex = (int)newMode;
                serialized.ApplyModifiedProperties();
            }

            EditorGUILayout.LabelField("Rate", GUILayout.Width(32f));
            EditorGUI.BeginChangeCheck();
            var newRate = EditorGUILayout.FloatField(rateProp.floatValue, GUILayout.Width(60f));
            if (EditorGUI.EndChangeCheck())
            {
                rateProp.floatValue = Mathf.Max(0f, newRate);
                serialized.ApplyModifiedProperties();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static float EstimateCumulative(LevelPacingConfiguration asset, int level)
        {
            // Sum thresholds × colors from 1 to level as a reasonable starting cumulative.
            var cumulative = 0f;
            for (var l = 1; l <= level; l++)
            {
                cumulative += asset.ThresholdForLevel(l) * asset.ColorsForLevel(l);
            }

            return cumulative;
        }
    }
}

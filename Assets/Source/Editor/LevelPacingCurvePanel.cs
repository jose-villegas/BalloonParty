using BalloonParty.Configuration.Level;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Draws the scoring curve graph panel for <see cref="LevelPacingWindow"/>. Display-only V1:
    /// shows cumulative milestones and derived per-color thresholds, with a hover tooltip for exact values.</summary>
    internal static class LevelPacingCurvePanel
    {
        private const float GraphHeight = 180f;
        private const float Padding = 8f;
        private const float AxisLabelWidth = 50f;
        private const int MaxPreviewLevels = 50;

        private static readonly Color CumulativeColor = new(0.4f, 0.7f, 1f, 1f);
        private static readonly Color PerColorColor = new(0.2f, 0.9f, 0.4f, 1f);
        private static readonly Color GridColor = new(0.3f, 0.3f, 0.3f, 0.5f);
        private static readonly Color HoverColor = new(1f, 1f, 1f, 0.6f);

        /// <summary>Draws the scoring curve panel. Call between <c>_serialized.Update()</c> and
        /// <c>ApplyModifiedProperties()</c>.</summary>
        internal static void Draw(LevelPacingConfiguration asset)
        {
            if (asset == null)
            {
                return;
            }

            var foldout = EditorPrefs.GetBool("LevelPacingCurvePanel_Foldout", true);
            foldout = EditorGUILayout.Foldout(foldout, "Scoring Curve Preview", true, EditorStyles.foldoutHeader);
            EditorPrefs.SetBool("LevelPacingCurvePanel_Foldout", foldout);

            if (!foldout)
            {
                return;
            }

            var graphRect = GUILayoutUtility.GetRect(0f, GraphHeight, GUILayout.ExpandWidth(true));
            graphRect.x += Padding;
            graphRect.width -= Padding * 2f;

            if (graphRect.width < 100f || Event.current.type == EventType.Layout)
            {
                return;
            }

            // Sample thresholds and cumulative values.
            var thresholds = new int[MaxPreviewLevels];
            var cumulatives = new float[MaxPreviewLevels];
            var maxThreshold = 1f;
            var maxCumulative = 1f;

            for (var i = 0; i < MaxPreviewLevels; i++)
            {
                var level = i + 1;
                thresholds[i] = asset.ThresholdForLevel(level);
                cumulatives[i] = i > 0 ? cumulatives[i - 1] + thresholds[i] : thresholds[i];

                if (thresholds[i] > maxThreshold)
                {
                    maxThreshold = thresholds[i];
                }

                if (cumulatives[i] > maxCumulative)
                {
                    maxCumulative = cumulatives[i];
                }
            }

            // Background.
            EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            var plotRect = new Rect(
                graphRect.x + AxisLabelWidth,
                graphRect.y + Padding,
                graphRect.width - AxisLabelWidth - Padding,
                graphRect.height - Padding * 2f - 16f);

            // Grid lines.
            DrawGrid(plotRect, maxThreshold);

            // Draw per-color threshold bars.
            DrawBars(plotRect, thresholds, maxThreshold, PerColorColor);

            // Draw cumulative line.
            DrawCumulativeLine(plotRect, cumulatives, maxCumulative);

            // Y-axis labels.
            DrawAxisLabels(graphRect, plotRect, maxThreshold);

            // X-axis level numbers.
            DrawLevelLabels(plotRect);

            // Hover tooltip.
            DrawHoverTooltip(plotRect, thresholds, cumulatives);

            // Legend.
            var legendRect = new Rect(graphRect.x, graphRect.yMax - 14f, graphRect.width, 14f);
            DrawLegend(legendRect);
        }

        private static void DrawGrid(Rect plotRect, float maxValue)
        {
            var gridLines = 4;
            for (var i = 1; i < gridLines; i++)
            {
                var y = plotRect.y + plotRect.height * (1f - (float)i / gridLines);
                Handles.color = GridColor;
                Handles.DrawLine(new Vector3(plotRect.x, y), new Vector3(plotRect.xMax, y));
            }
        }

        private static void DrawBars(Rect plotRect, int[] values, float maxValue, Color color)
        {
            var barWidth = plotRect.width / values.Length;
            var barColor = new Color(color.r, color.g, color.b, 0.5f);

            for (var i = 0; i < values.Length; i++)
            {
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

        private static void DrawCumulativeLine(Rect plotRect, float[] cumulatives, float maxCumulative)
        {
            if (cumulatives.Length < 2)
            {
                return;
            }

            var points = new Vector3[cumulatives.Length];
            var barWidth = plotRect.width / cumulatives.Length;

            for (var i = 0; i < cumulatives.Length; i++)
            {
                var x = plotRect.x + (i + 0.5f) * barWidth;
                var y = plotRect.y + plotRect.height * (1f - cumulatives[i] / maxCumulative);
                points[i] = new Vector3(x, y, 0f);
            }

            Handles.color = CumulativeColor;
            Handles.DrawAAPolyLine(2f, points);
        }

        private static void DrawAxisLabels(Rect graphRect, Rect plotRect, float maxValue)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.gray }
            };

            var gridLines = 4;
            for (var i = 0; i <= gridLines; i++)
            {
                var y = plotRect.y + plotRect.height * (1f - (float)i / gridLines);
                var value = maxValue * i / gridLines;
                var labelRect = new Rect(graphRect.x, y - 7f, AxisLabelWidth - 4f, 14f);
                GUI.Label(labelRect, Mathf.RoundToInt(value).ToString(), style);
            }
        }

        private static void DrawLevelLabels(Rect plotRect)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.gray }
            };

            var barWidth = plotRect.width / MaxPreviewLevels;
            var step = Mathf.Max(1, MaxPreviewLevels / 10);

            for (var i = 0; i < MaxPreviewLevels; i += step)
            {
                var x = plotRect.x + (i + 0.5f) * barWidth;
                var labelRect = new Rect(x - 15f, plotRect.yMax + 1f, 30f, 14f);
                GUI.Label(labelRect, (i + 1).ToString(), style);
            }
        }

        private static void DrawHoverTooltip(Rect plotRect, int[] thresholds, float[] cumulatives)
        {
            var mousePos = Event.current.mousePosition;
            if (!plotRect.Contains(mousePos))
            {
                return;
            }

            var barWidth = plotRect.width / thresholds.Length;
            var index = Mathf.Clamp((int)((mousePos.x - plotRect.x) / barWidth), 0, thresholds.Length - 1);
            var level = index + 1;

            // Vertical hover line.
            var lineX = plotRect.x + (index + 0.5f) * barWidth;
            Handles.color = HoverColor;
            Handles.DrawLine(new Vector3(lineX, plotRect.y), new Vector3(lineX, plotRect.yMax));

            // Tooltip.
            var tooltip = $"Level {level}\nPer-color: {thresholds[index]}\nCumulative: {Mathf.RoundToInt(cumulatives[index])}";
            var content = new GUIContent(tooltip);
            var tooltipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2)
            };
            var size = tooltipStyle.CalcSize(content);
            var tooltipRect = new Rect(
                Mathf.Min(mousePos.x + 10f, plotRect.xMax - size.x),
                Mathf.Min(mousePos.y - size.y - 5f, plotRect.yMax - size.y),
                size.x, size.y);
            GUI.Label(tooltipRect, content, tooltipStyle);

            // Force repaint on mouse move for tooltip tracking.
            if (Event.current.type == EventType.MouseMove)
            {
                HandleUtility.Repaint();
            }
        }

        private static void DrawLegend(Rect rect)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.gray }
            };

            var x = rect.x + AxisLabelWidth;
            EditorGUI.DrawRect(new Rect(x, rect.y + 4f, 12f, 6f), PerColorColor);
            GUI.Label(new Rect(x + 14f, rect.y, 80f, 14f), "Per-color", style);

            x += 90f;
            EditorGUI.DrawRect(new Rect(x, rect.y + 4f, 12f, 3f), CumulativeColor);
            GUI.Label(new Rect(x + 14f, rect.y, 80f, 14f), "Cumulative", style);
        }
    }
}

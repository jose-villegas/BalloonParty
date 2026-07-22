using System;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Charts
{
    /// <summary>Provides grid lines and axis labels for editor plots.</summary>
    public static class PlotGrid
    {
        /// <summary>Describes a horizontal grid line in plot-space.</summary>
        public struct GridLine
        {
            public float Y;
            public string Label;
        }

        /// <summary>Computes horizontal grid lines for the supplied value range.</summary>
        public static GridLine[] ComputeGridLines(Rect area, int divisions, float minVal, float maxVal)
        {
            if (divisions <= 0)
            {
                return Array.Empty<GridLine>();
            }

            if (Mathf.Approximately(minVal, maxVal))
            {
                return new[]
                {
                    new GridLine
                    {
                        Y = area.center.y,
                        Label = FormatLabel(minVal)
                    }
                };
            }

            GridLine[] lines = new GridLine[divisions + 1];
            for (int i = 0; i <= divisions; i++)
            {
                float normalized = i / (float)divisions;
                lines[i] = new GridLine
                {
                    Y = Mathf.Lerp(area.yMax, area.yMin, normalized),
                    Label = FormatLabel(Mathf.Lerp(minVal, maxVal, normalized))
                };
            }

            return lines;
        }

        /// <summary>Draws horizontal grid lines and labels for a plot area.</summary>
        public static void Draw(Rect area, int divisions, float minVal, float maxVal, Color gridColor, GUIStyle labelStyle = null)
        {
            GridLine[] lines = ComputeGridLines(area, divisions, minVal, maxVal);
            if (lines.Length == 0)
            {
                return;
            }

            GUIStyle style = GetYLabelStyle(labelStyle);
            Color previousColor = Handles.color;
            Handles.color = gridColor;
            for (int i = 0; i < lines.Length; i++)
            {
                Handles.DrawLine(new Vector3(area.xMin, lines[i].Y), new Vector3(area.xMax, lines[i].Y));
                Rect labelRect = new Rect(area.xMin - 40f, lines[i].Y - (style.lineHeight * 0.5f), 36f, style.lineHeight);
                GUI.Label(labelRect, lines[i].Label, style);
            }

            Handles.color = previousColor;
        }

        /// <summary>Draws evenly stepped x-axis labels below the plot.</summary>
        public static void DrawXLabels(Rect area, int startLabel, int endLabel, int step, GUIStyle labelStyle = null)
        {
            if (step <= 0 || endLabel < startLabel)
            {
                return;
            }

            GUIStyle style = GetXLabelStyle(labelStyle);
            int range = endLabel - startLabel;
            for (int label = startLabel; label <= endLabel; label += step)
            {
                float normalized = range > 0 ? (label - startLabel) / (float)range : 0f;
                float x = Mathf.Lerp(area.xMin, area.xMax, normalized);
                Rect labelRect = new Rect(x - 20f, area.yMax + 2f, 40f, style.lineHeight);
                GUI.Label(labelRect, label.ToString(), style);
            }
        }

        private static string FormatLabel(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value)) ? value.ToString("F0") : value.ToString("F1");
        }

        private static GUIStyle GetXLabelStyle(GUIStyle labelStyle)
        {
            if (labelStyle != null)
            {
                return new GUIStyle(labelStyle)
                {
                    alignment = TextAnchor.UpperCenter
                };
            }

            return StyleCache.Get("PlotGrid.XLabel", () => new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter
            });
        }

        private static GUIStyle GetYLabelStyle(GUIStyle labelStyle)
        {
            if (labelStyle != null)
            {
                return new GUIStyle(labelStyle)
                {
                    alignment = TextAnchor.MiddleRight
                };
            }

            return StyleCache.Get("PlotGrid.YLabel", () => new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight
            });
        }
    }
}

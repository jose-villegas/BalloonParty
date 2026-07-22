using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Charts
{
    /// <summary>A horizontal threshold line drawn over the chart at a normalized Y position.</summary>
    public struct ThresholdLine
    {
        public float Value;
        public Color Color;
    }

    /// <summary>Configures how chart bars are sized and colored.</summary>
    public struct BarChartOptions
    {
        public Color BarColor;
        public Color SelectedColor;
        public float BarPadding;
        public float MinBarWidth;

        /// <summary>
        /// Per-bar color override (index, value) → color.
        /// When non-null, <see cref="BarColor"/> is ignored for non-selected bars.
        /// </summary>
        public Func<int, float, Color> BarColorResolver;

        /// <summary>Optional horizontal threshold line overlay.</summary>
        public ThresholdLine? Threshold;
    }

    /// <summary>Provides reusable editor bar chart helpers.</summary>
    public static class BarChart
    {
        /// <summary>Computes the bar rectangles for a chart area without drawing them.</summary>
        public static Rect[] ComputeBarRects(Rect area, IReadOnlyList<float> values, float max, in BarChartOptions options)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<Rect>();
            }

            int count = values.Count;
            Rect[] rects = new Rect[count];
            float slotWidth = area.width / count;
            float barPadding = options.BarPadding > 0f ? options.BarPadding : 1f;
            float minBarWidth = options.MinBarWidth > 0f ? options.MinBarWidth : 4f;
            float barWidth = Mathf.Min(slotWidth, Mathf.Max(minBarWidth, slotWidth - barPadding));
            float xOffset = (slotWidth - barWidth) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float normalized = max > 0f ? Mathf.Clamp01(values[i] / max) : 0f;
                float height = normalized * area.height;
                rects[i] = new Rect(area.xMin + (slotWidth * i) + xOffset, area.yMax - height, barWidth, height);
            }

            return rects;
        }

        /// <summary>Finds the bar index that corresponds to the supplied x coordinate.</summary>
        public static int IndexFromX(float x, Rect area, int count)
        {
            if (count <= 0 || area.width <= 0f)
            {
                return -1;
            }

            float clampedX = Mathf.Clamp(x, area.xMin, area.xMax - Mathf.Epsilon);
            float normalized = (clampedX - area.xMin) / area.width;
            return Mathf.Clamp(Mathf.FloorToInt(normalized * count), 0, count - 1);
        }

        /// <summary>Draws the chart bars and reports which one was clicked.</summary>
        public static int? Draw(Rect area, IReadOnlyList<float> values, float max, in BarChartOptions options, int selectedIndex = -1)
        {
            Rect[] rects = ComputeBarRects(area, values, max, options);
            for (int i = 0; i < rects.Length; i++)
            {
                Color color;
                if (i == selectedIndex)
                {
                    color = options.SelectedColor;
                }
                else if (options.BarColorResolver != null)
                {
                    color = options.BarColorResolver(i, values[i]);
                }
                else
                {
                    color = options.BarColor;
                }

                EditorGUI.DrawRect(rects[i], color);
            }

            if (options.Threshold.HasValue && max > 0f)
            {
                var t = options.Threshold.Value;
                float normalizedY = Mathf.Clamp01(t.Value / max);
                float lineY = area.yMax - (area.height * normalizedY);
                float lineHeight = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
                EditorGUI.DrawRect(new Rect(area.x, lineY, area.width, lineHeight), t.Color);
            }

            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.MouseDown || currentEvent.button != 0 || !area.Contains(currentEvent.mousePosition))
            {
                return null;
            }

            int index = IndexFromX(currentEvent.mousePosition.x, area, rects.Length);
            currentEvent.Use();
            return index >= 0 ? index : null;
        }
    }
}

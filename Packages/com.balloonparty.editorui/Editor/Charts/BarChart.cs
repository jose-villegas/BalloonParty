using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Charts
{
    /// <summary>Configures how chart bars are sized and colored.</summary>
    public struct BarChartOptions
    {
        public Color barColor;
        public Color selectedColor;
        public float barPadding;
        public float minBarWidth;
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
            float barPadding = options.barPadding > 0f ? options.barPadding : 1f;
            float minBarWidth = options.minBarWidth > 0f ? options.minBarWidth : 4f;
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
                EditorGUI.DrawRect(rects[i], i == selectedIndex ? options.selectedColor : options.barColor);
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

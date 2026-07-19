#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Shared IMGUI layout helpers that size every grid/row to <see cref="ContentWidth" /> so cheat
    ///     panels can never grow wider than the console's scroll viewport — the root cause of the
    ///     sideways-scrolling bug was <c>GUILayout.SelectionGrid</c>/button rows sizing to their longest
    ///     label regardless of the viewport, which pushed the whole scroll view horizontally.
    /// </summary>
    internal static class CheatLayout
    {
        private const float ButtonSpacing = 4f;
        private const float LabelPadding = 12f;
        private const float StepButtonWidth = 28f;
        private const float FieldWidth = 56f;

        // Gates hold-to-repeat on the +/- stepper buttons — see IntField.
        private const float StepInterval = 0.07f;

        // Saved content widths across nested BeginPanel/EndPanel — a box shrinks the usable width by its
        // border + padding, so grids inside it must size to the reduced width or they overflow the panel.
        private static readonly Stack<float> PanelWidths = new();

        // Per-IntField raw text so mid-edit states (empty, "-") survive across frames instead of
        // snapping back to the last valid value; keyed by the caller's field key.
        private static readonly Dictionary<string, string> EditBuffers = new();

        // Keys of currently-expanded Dropdown foldouts.
        private static readonly HashSet<string> OpenDropdowns = new();

        internal static float ContentWidth;

        // Last time any IntField stepper button applied a step. A single shared timer is enough
        // because only one button can be held at a time (single mouse/touch pointer).
        private static float _lastStep;

        // Called once per OnGUI before drawing: sets the viewport width and drops any panel widths a
        // previous pass left behind (e.g. if a cheat's draw threw), so the stack can't drift.
        internal static void BeginFrame(float contentWidth)
        {
            ContentWidth = contentWidth;
            PanelWidths.Clear();
        }

        // Renders "[-] [value] [+]"; the middle field is directly editable, and the +/- buttons
        // repeat while held (throttled by StepInterval) instead of stepping once per click.
        internal static int IntField(string key, int value, int min = int.MinValue, int max = int.MaxValue)
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.RepeatButton("−", GUILayout.Width(StepButtonWidth)) && TryConsumeStep())
            {
                value = Mathf.Clamp(value - 1, min, max);
                EditBuffers.Remove(key);
            }

            var text = EditBuffers.TryGetValue(key, out var buffered) ? buffered : value.ToString();
            text = GUILayout.TextField(text, GUILayout.Width(FieldWidth));
            EditBuffers[key] = text;

            if (int.TryParse(text, out var parsed))
            {
                value = Mathf.Clamp(parsed, min, max);
            }

            if (GUILayout.RepeatButton("+", GUILayout.Width(StepButtonWidth)) && TryConsumeStep())
            {
                value = Mathf.Clamp(value + 1, min, max);
                EditBuffers.Remove(key);
            }

            // The buffer only needs to survive while it disagrees with the resolved value (mid-edit,
            // or not-yet-parseable); once it matches, drop it so later external changes surface.
            if (EditBuffers.TryGetValue(key, out var current)
                && int.TryParse(current, out var currentParsed)
                && Mathf.Clamp(currentParsed, min, max) == value)
            {
                EditBuffers.Remove(key);
            }

            GUILayout.EndHorizontal();
            return value;
        }

        // A foldout that collapses to the selected option's name and expands into a SelectionGrid to
        // pick another — the console's existing panel/foldout idiom (see Tags, section headers)
        // standing in for IMGUI's lack of a native popup.
        internal static int Dropdown(string key, string[] options, int selected)
        {
            var open = OpenDropdowns.Contains(key);
            var current = selected >= 0 && selected < options.Length ? options[selected] : "?";

            BeginPanel();

            if (GUILayout.Button($"{(open ? "▾" : "▸")}  {current}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            {
                open = !open;
                if (open)
                {
                    OpenDropdowns.Add(key);
                }
                else
                {
                    OpenDropdowns.Remove(key);
                }
            }

            if (open)
            {
                selected = SelectionGrid(selected, options);
            }

            EndPanel();
            return selected;
        }

        internal static int SelectionGrid(int selected, string[] labels)
        {
            var cols = ColumnCount(labels);
            return GUILayout.SelectionGrid(selected, labels, cols, GUILayout.Width(ContentWidth));
        }

        internal static int ButtonGrid(string[] labels)
        {
            var cols = ColumnCount(labels);
            var buttonWidth = (ContentWidth / cols) - ButtonSpacing;
            var clicked = -1;

            for (var row = 0; row < labels.Length; row += cols)
            {
                GUILayout.BeginHorizontal();
                for (var col = 0; col < cols && row + col < labels.Length; col++)
                {
                    if (GUILayout.Button(labels[row + col], GUILayout.Width(buttonWidth)))
                    {
                        clicked = row + col;
                    }
                }

                GUILayout.EndHorizontal();
            }

            return clicked;
        }

        internal static void BeginPanel(string title = null)
        {
            var box = GUI.skin.box;
            GUILayout.BeginVertical(box);

            // Reserve the box's border + padding so anything sizing to ContentWidth stays inside the panel;
            // restored on EndPanel. Nested panels compound the inset via the stack.
            PanelWidths.Push(ContentWidth);
            ContentWidth -= box.margin.horizontal + box.padding.horizontal;

            if (!string.IsNullOrEmpty(title))
            {
                GUILayout.Label(title);
            }
        }

        internal static void EndPanel()
        {
            GUILayout.EndVertical();
            if (PanelWidths.Count > 0)
            {
                ContentWidth = PanelWidths.Pop();
            }
        }

        // Longest label × N columns must fit ContentWidth — the same math SelectionGrid and
        // ButtonGrid both need, so it lives once here.
        private static int ColumnCount(string[] labels)
        {
            var maxLabelWidth = 0f;
            foreach (var label in labels)
            {
                var width = GUI.skin.button.CalcSize(new GUIContent(label)).x;
                if (width > maxLabelWidth)
                {
                    maxLabelWidth = width;
                }
            }

            var cellWidth = maxLabelWidth + LabelPadding;
            var cols = Mathf.FloorToInt(ContentWidth / cellWidth);
            return Mathf.Clamp(cols, 1, labels.Length);
        }

        // Time.unscaledTime is constant within a frame, so gating on it — rather than counting
        // OnGUI events — naturally collapses IntField's Layout + Repaint passes into one step.
        private static bool TryConsumeStep()
        {
            var now = Time.unscaledTime;
            if (now - _lastStep < StepInterval)
            {
                return false;
            }

            _lastStep = now;
            return true;
        }
    }
}
#endif

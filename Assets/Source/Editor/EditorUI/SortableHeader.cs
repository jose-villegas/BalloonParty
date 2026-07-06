using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Tracks which column is sorted and in which direction; share the instance across <see cref="SortableHeader"/> calls.
    /// </summary>
    internal sealed class SortState
    {
        internal int Column = -1;
        internal bool Ascending = true;
    }

    /// <summary>
    ///     Draws a toolbar button that toggles sort direction on click
    ///     and sorts a list via a caller-supplied comparison delegate.
    /// </summary>
    internal static class SortableHeader
    {
        /// <summary>
        ///     Draws a single sortable column header; clicking toggles <paramref name="state"/> ascending/descending.
        /// </summary>
        internal static void Draw(string label, int column, float width, SortState state)
        {
            var display = state.Column == column
                ? $"{label} {(state.Ascending ? "▲" : "▼")}"
                : label;

            if (GUILayout.Button(display, EditorStyles.toolbarButton, GUILayout.Width(width)))
            {
                if (state.Column == column)
                {
                    state.Ascending = !state.Ascending;
                }
                else
                {
                    state.Column = column;
                    state.Ascending = true;
                }
            }
        }

        /// <summary>
        ///     Sorts <paramref name="items"/> in place using <paramref name="state"/> and a column-to-comparison mapper.
        /// </summary>
        internal static void ApplySort<T>(
            System.Collections.Generic.List<T> items,
            SortState state,
            System.Func<int, T, T, int> compareByColumn)
        {
            if (state.Column < 0)
            {
                return;
            }

            items.Sort((a, b) =>
            {
                var cmp = compareByColumn(state.Column, a, b);
                return state.Ascending ? cmp : -cmp;
            });
        }
    }
}

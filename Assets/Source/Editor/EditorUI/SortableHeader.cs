using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Tracks which column is sorted and in which direction.
    ///     Pass the same instance to <see cref="SortableHeader.Draw"/> and
    ///     <see cref="SortableHeader.ApplySort{T}"/> to keep them in sync.
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
        ///     Draws a single sortable column header as a toolbar button.
        ///     Clicking toggles <paramref name="state"/> between ascending/descending.
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
        ///     Sorts <paramref name="items"/> in place using the current
        ///     <paramref name="state"/> and a column-to-comparison mapper.
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


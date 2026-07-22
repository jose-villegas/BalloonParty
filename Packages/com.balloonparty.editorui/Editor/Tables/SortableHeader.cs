using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Tables
{
    /// <summary>Tracks which column is sorted and in which direction.</summary>
    public sealed class SortState
    {
        public int Column = -1;
        public bool Ascending = true;
    }

    /// <summary>Draws a toolbar button that toggles sort direction and sorts a list via a comparison delegate.</summary>
    public static class SortableHeader
    {
        /// <summary>Draws a single sortable column header; clicking toggles ascending/descending.</summary>
        public static void Draw(string label, int column, float width, SortState state)
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

        /// <summary>Sorts items in place using the sort state and a column-comparison mapper.</summary>
        public static void ApplySort<T>(List<T> items, SortState state, Func<int, T, T, int> compareByColumn)
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

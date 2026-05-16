using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Interface for items that can be selected in a table row.
    /// </summary>
    internal interface ISelectable
    {
        bool Selected { get; set; }
    }

    /// <summary>
    ///     Helpers for managing checkbox-based row selection in editor tables.
    /// </summary>
    internal static class SelectionTracker
    {
        /// <summary>
        ///     Draws a toggle-all checkbox and syncs it with the visible items.
        ///     Returns the new toggle-all state.
        /// </summary>
        internal static bool DrawSelectAllToggle<T>(bool selectAll, IReadOnlyList<T> visibleItems)
            where T : class, ISelectable
        {
            var newValue = EditorGUILayout.Toggle(selectAll, GUILayout.Width(18));

            if (newValue != selectAll)
            {
                foreach (var item in visibleItems)
                {
                    item.Selected = newValue;
                }
            }

            return newValue;
        }

        /// <summary>
        ///     Draws a single row selection toggle for an item.
        /// </summary>
        internal static void DrawRowToggle(ISelectable item)
        {
            item.Selected = EditorGUILayout.Toggle(item.Selected, GUILayout.Width(18));
        }

        /// <summary>
        ///     Draws a status label showing selected count vs total.
        /// </summary>
        internal static void DrawSelectionCount<T>(IReadOnlyList<T> allItems, float width = 140f)
            where T : class, ISelectable
        {
            var selectedCount = 0;

            foreach (var item in allItems)
            {
                if (item.Selected)
                {
                    selectedCount++;
                }
            }

            EditorGUILayout.LabelField(
                $"{selectedCount} selected / {allItems.Count} total",
                GUILayout.Width(width));
        }

        /// <summary>
        ///     Returns all items where <see cref="ISelectable.Selected"/> is true.
        /// </summary>
        internal static List<T> GetSelected<T>(IReadOnlyList<T> items)
            where T : class, ISelectable
        {
            return items.Where(i => i.Selected).ToList();
        }
    }
}


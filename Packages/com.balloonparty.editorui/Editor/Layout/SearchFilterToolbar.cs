using System;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Layout
{
    public static class SearchFilterToolbar
    {
        public static TEnum Draw<TEnum>(
            ref string searchText,
            TEnum currentFilter,
            string[] filterLabels,
            Action onRefresh = null)
            where TEnum : Enum
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            searchText = EditorGUILayout.TextField(
                searchText,
                EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(120));

            GUILayout.Space(8);

            EditorGUILayout.LabelField("Filter:", GUILayout.Width(38));
            var newFilter = (TEnum)(object)EditorGUILayout.Popup(
                Convert.ToInt32(currentFilter),
                filterLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            if (onRefresh != null &&
                GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                onRefresh();
            }

            EditorGUILayout.EndHorizontal();

            return newFilter;
        }
    }
}

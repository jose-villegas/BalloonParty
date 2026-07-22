using System;
using UnityEditor;

namespace BalloonParty.EditorUI.Layout
{
    public static class FoldoutSection
    {
        public static bool Draw(string prefKey, string label, bool defaultOpen = true)
        {
            var isOpen = EditorPrefs.GetBool(prefKey, defaultOpen);
            var newState = EditorGUILayout.Foldout(isOpen, label, true);

            if (newState != isOpen)
            {
                EditorPrefs.SetBool(prefKey, newState);
            }

            return newState;
        }

        /// <summary>Draws a foldout with pluggable persistence via getter/setter delegates.</summary>
        public static bool Draw(Func<bool> getExpanded, Action<bool> setExpanded, string label)
        {
            var isOpen = getExpanded();
            var newState = EditorGUILayout.Foldout(isOpen, label, true);

            if (newState != isOpen)
            {
                setExpanded(newState);
            }

            return newState;
        }

        public static void Begin()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        }

        public static void End()
        {
            EditorGUILayout.EndVertical();
        }
    }
}

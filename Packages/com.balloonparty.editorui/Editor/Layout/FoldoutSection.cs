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

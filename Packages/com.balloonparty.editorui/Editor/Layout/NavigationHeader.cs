using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Layout
{
    public static class NavigationHeader
    {
        public struct NavigationResult
        {
            public int Value;
            public bool Changed;
        }

        public static NavigationResult Draw(string label, int value, int min = 1, float fieldWidth = 50f)
        {
            var clampedValue = Mathf.Max(min, value);
            var newValue = clampedValue;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("◀", GUILayout.Width(24f)))
            {
                newValue = Mathf.Max(min, newValue - 1);
            }

            EditorGUILayout.LabelField(label);
            newValue = Mathf.Max(min, EditorGUILayout.IntField(newValue, GUILayout.Width(fieldWidth)));

            if (GUILayout.Button("▶", GUILayout.Width(24f)))
            {
                newValue += 1;
            }

            EditorGUILayout.EndHorizontal();

            newValue = Mathf.Max(min, newValue);

            return new NavigationResult
            {
                Value = newValue,
                Changed = newValue != clampedValue
            };
        }
    }
}

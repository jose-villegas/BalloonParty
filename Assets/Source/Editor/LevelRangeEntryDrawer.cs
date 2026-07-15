using BalloonParty.Configuration.Level;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Shows <c>LevelRangeEntry</c> array elements as "Level N", "Level N–M", or
    /// "Fallback" instead of the default "Element 0, 1, …".</summary>
    [CustomPropertyDrawer(typeof(LevelRangeEntry))]
    internal class LevelRangeEntryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var from = property.FindPropertyRelative("_fromLevel").intValue;
            var to = property.FindPropertyRelative("_toLevel").intValue;

            if (from < 0 || to < 0)
            {
                label.text = "Fallback";
            }
            else if (from == to)
            {
                label.text = $"Level {from}";
            }
            else
            {
                label.text = $"Level {from}–{to}";
            }

            EditorGUI.PropertyField(position, property, label, true);
        }
    }
}

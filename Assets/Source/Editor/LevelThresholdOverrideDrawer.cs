using BalloonParty.Configuration.Level;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Shows <c>LevelThresholdOverride</c> array elements as "Level N" or "Level N–M"
    /// instead of the default "Element 0, 1, …".</summary>
    [CustomPropertyDrawer(typeof(LevelThresholdOverride))]
    internal class LevelThresholdOverrideDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var from = property.FindPropertyRelative("_fromLevel").intValue;
            var to = property.FindPropertyRelative("_toLevel").intValue;

            label.text = from == to ? $"Level {from}" : $"Level {from}–{to}";

            EditorGUI.PropertyField(position, property, label, true);
        }
    }
}

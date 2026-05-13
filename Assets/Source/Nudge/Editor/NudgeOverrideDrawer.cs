using BalloonParty.Nudge;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Source.Nudge.Editor
{
    [CustomPropertyDrawer(typeof(NudgeOverride))]
    public class NudgeOverrideDrawer : PropertyDrawer
    {
        private const float LineHeight = 20f;
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return LineHeight + Spacing;
            }

            var lines = 4; // header, appliesTo, distance, duration
            var appliesToProp = property.FindPropertyRelative("_appliesTo");
            if (appliesToProp != null && ((NudgeType)appliesToProp.intValue).HasFlag(NudgeType.Shockwave))
            {
                lines += 1; // falloff
            }

            return lines * (LineHeight + Spacing);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var headerRect = new Rect(position.x, position.y, position.width, LineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = indent + 1;

                var y = position.y + LineHeight + Spacing;

                var appliesToProp = property.FindPropertyRelative("_appliesTo");
                if (appliesToProp != null)
                {
                    var rect = new Rect(position.x, y, position.width, LineHeight);
                    appliesToProp.intValue = (int)(NudgeType)EditorGUI.EnumFlagsField(
                        rect,
                        new GUIContent("Applies To"),
                        (NudgeType)appliesToProp.intValue);
                    y += LineHeight + Spacing;
                }

                y = DrawField(position, y, property, "_distance", "Distance");
                y = DrawField(position, y, property, "_duration", "Duration");

                if (appliesToProp != null && ((NudgeType)appliesToProp.intValue).HasFlag(NudgeType.Shockwave))
                {
                    DrawField(position, y, property, "_falloff", "Falloff");
                }

                EditorGUI.indentLevel = indent;
            }

            EditorGUI.EndProperty();
        }

        private static float DrawField(
            Rect position,
            float y,
            SerializedProperty parent,
            string fieldName,
            string displayName)
        {
            var prop = parent.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return y;
            }

            var rect = new Rect(position.x, y, position.width, LineHeight);
            EditorGUI.PropertyField(rect, prop, new GUIContent(displayName));
            return y + LineHeight + Spacing;
        }
    }
}

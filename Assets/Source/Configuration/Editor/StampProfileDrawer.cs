using UnityEditor;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(StampProfile))]
    internal class StampProfileDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight * 9f + EditorGUIUtility.standardVerticalSpacing * 8f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var sources = (StampSource)property.FindPropertyRelative("Sources").intValue;
            var foldoutLabel = sources != 0 ? sources.ToString() : "None";

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, foldoutLabel, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                var y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                var lineHeight = EditorGUIUtility.singleLineHeight;
                var spacing = EditorGUIUtility.standardVerticalSpacing;

                DrawField("Sources", "Sources", ref y);
                DrawField("Radius", "Radius", ref y);
                DrawField("Strength", "Strength", ref y);
                DrawField("Duration", "Duration", ref y);
                DrawField("Interval", "Interval", ref y);
                DrawField("Spacing", "Spacing", ref y);
                DrawField("RadiusGrowth", "Radius Growth", ref y);
                DrawField("StrengthFalloff", "Strength Falloff", ref y);

                EditorGUI.indentLevel--;

                void DrawField(string fieldName, string displayName, ref float currentY)
                {
                    var prop = property.FindPropertyRelative(fieldName);
                    if (prop == null)
                    {
                        return;
                    }

                    var rect = new Rect(position.x, currentY, position.width, lineHeight);
                    EditorGUI.PropertyField(rect, prop, new GUIContent(displayName));
                    currentY += lineHeight + spacing;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}

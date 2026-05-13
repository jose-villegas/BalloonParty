using BalloonParty.Configuration;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(BalloonPrefabEntry))]
    public class BalloonPrefabEntryDrawer : PropertyDrawer
    {
        private const float LineHeight = 20f;
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return LineHeight + Spacing;
            }

            var lines = 4; // prefab, weight, maxCount, canHoldItem

            var nudgeOverrides = property.FindPropertyRelative("_nudgeOverrides");
            if (nudgeOverrides != null)
            {
                lines += 1; // array header
                lines += (int)EditorGUI.GetPropertyHeight(nudgeOverrides, true) / (int)(LineHeight + Spacing);
            }

            lines += 1; // overridePopVfx toggle
            var overridePopVfx = property.FindPropertyRelative("_overridePopVfx");
            if (overridePopVfx != null && overridePopVfx.boolValue)
            {
                lines += 1; // popVfxPrefab only
            }

            return (lines + 1) * (LineHeight + Spacing);
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

                y = DrawField(position, y, property, "_prefab", "Prefab");
                y = DrawField(position, y, property, "_weight", "Weight");
                y = DrawField(position, y, property, "_maxCount", "Max Count");
                y = DrawField(position, y, property, "_canHoldItem", "Can Hold Item");

                var nudgeOverrides = property.FindPropertyRelative("_nudgeOverrides");
                if (nudgeOverrides != null)
                {
                    var height = EditorGUI.GetPropertyHeight(nudgeOverrides, true);
                    var rect = new Rect(position.x, y, position.width, height);
                    EditorGUI.PropertyField(rect, nudgeOverrides, new GUIContent("Nudge Overrides"), true);
                    y += height + Spacing;
                }

                y = DrawField(position, y, property, "_overridePopVfx", "Override Pop VFX");

                var overridePopVfx = property.FindPropertyRelative("_overridePopVfx");
                if (overridePopVfx != null && overridePopVfx.boolValue)
                {
                    EditorGUI.indentLevel = indent + 2;
                    y = DrawField(position, y, property, "_popVfxPrefab", "VFX Prefab");
                    EditorGUI.indentLevel = indent + 1;
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

using BalloonParty.Configuration;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Source.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(BalloonPrefabEntry))]
    public class BalloonPrefabEntryDrawer : PropertyDrawer
    {
        private const float LineHeight = 20f;
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lines = 4; // prefab, weight, maxCount, overrideNudge
            var overrideNudge = property.FindPropertyRelative("_overrideNudge");
            if (overrideNudge != null && overrideNudge.boolValue)
            {
                lines += 2; // nudgeDistanceOverride, nudgeDurationOverride
            }

            lines += 1; // overridePopVfx toggle
            var overridePopVfx = property.FindPropertyRelative("_overridePopVfx");
            if (overridePopVfx != null && overridePopVfx.boolValue)
            {
                lines += 1; // popVfxPrefab only
            }

            return property.isExpanded
                ? (lines + 1) * (LineHeight + Spacing)
                : LineHeight + Spacing;
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

                y = DrawField(position, y, property, "_prefab",        "Prefab");
                y = DrawField(position, y, property, "_weight",        "Weight");
                y = DrawField(position, y, property, "_maxCount",      "Max Count");
                y = DrawField(position, y, property, "_overrideNudge", "Override Nudge");

                var overrideNudge = property.FindPropertyRelative("_overrideNudge");
                if (overrideNudge != null && overrideNudge.boolValue)
                {
                    EditorGUI.indentLevel = indent + 2;
                    y = DrawField(position, y, property, "_nudgeDistanceOverride", "Distance");
                    y = DrawField(position, y, property, "_nudgeDurationOverride", "Duration");
                    EditorGUI.indentLevel = indent + 1;
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

        private static float DrawField(Rect position, float y, SerializedProperty parent, string fieldName, string displayName)
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


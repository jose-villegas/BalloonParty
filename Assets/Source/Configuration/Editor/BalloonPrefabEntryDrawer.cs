using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Editor;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(BalloonPrefabEntry))]
    public class BalloonPrefabEntryDrawer : AutoFieldPropertyDrawer
    {
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "_balloonType",
            "_nudgeOverrides",
            "_overridePopVfx",
            "_popVfxPrefab"
        };

        protected override GUIContent BuildFoldoutLabel(GUIContent label, SerializedProperty property)
        {
            var typeProp = property.FindPropertyRelative("_balloonType");
            if (typeProp == null)
            {
                return label;
            }

            var balloonType = (BalloonType)typeProp.intValue;
            return new GUIContent($"{label.text}  [{balloonType}]");
        }

        protected override float DrawPinnedFields(Rect position, float y, SerializedProperty property)
        {
            return PropertyDrawerHelper.DrawNamedField(position, y, property, "_balloonType", "Balloon Type");
        }

        protected override float GetPinnedFieldsHeight(SerializedProperty property)
        {
            return PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
        }

        protected override float DrawSpecialFields(Rect position, float y, SerializedProperty property)
        {
            var nudgeOverrides = property.FindPropertyRelative("_nudgeOverrides");
            if (nudgeOverrides != null)
            {
                var h = EditorGUI.GetPropertyHeight(nudgeOverrides, true);
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, h),
                    nudgeOverrides,
                    new GUIContent("Nudge Overrides"),
                    true);
                y += h + PropertyDrawerHelper.Spacing;
            }

            y = PropertyDrawerHelper.DrawNamedField(position, y, property, "_overridePopVfx", "Override Pop VFX");

            var overridePopVfx = property.FindPropertyRelative("_overridePopVfx");
            if (overridePopVfx != null && overridePopVfx.boolValue)
            {
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = indent + 1;
                y = PropertyDrawerHelper.DrawNamedField(position, y, property, "_popVfxPrefab", "VFX Prefab");
                EditorGUI.indentLevel = indent;
            }

            return y;
        }

        protected override float GetSpecialFieldsHeight(SerializedProperty property)
        {
            var row = PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;

            var nudgeOverrides = property.FindPropertyRelative("_nudgeOverrides");
            var nudgeHeight = nudgeOverrides != null
                ? EditorGUI.GetPropertyHeight(nudgeOverrides, true) + PropertyDrawerHelper.Spacing
                : 0f;

            var height = nudgeHeight + row;

            var overridePopVfx = property.FindPropertyRelative("_overridePopVfx");
            if (overridePopVfx != null && overridePopVfx.boolValue)
            {
                height += row;
            }

            return height;
        }
    }
}

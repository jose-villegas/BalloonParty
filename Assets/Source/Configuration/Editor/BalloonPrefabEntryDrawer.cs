using System.Collections.Generic;
using BalloonParty.Editor;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(BalloonPrefabEntry))]
    public class BalloonPrefabEntryDrawer : AutoFieldPropertyDrawer
    {
        /// <summary>
        ///     Fields handled manually because they have variable height, conditional
        ///     visibility, or special layout. Everything else is drawn automatically.
        /// </summary>
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "_nudgeOverrides",
            "_overridePopVfx",
            "_popVfxPrefab"
        };

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

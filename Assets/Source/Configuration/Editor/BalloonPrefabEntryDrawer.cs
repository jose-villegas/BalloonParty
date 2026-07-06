using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Editor;
using UnityEditor;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(BalloonPrefabEntry))]
    public class BalloonPrefabEntryDrawer : AutoFieldPropertyDrawer
    {
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "_balloonType",
            "_nudgeOverrides",
            "_hitVfxOverrides"
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

            var hitVfxOverrides = property.FindPropertyRelative("_hitVfxOverrides");
            if (hitVfxOverrides != null)
            {
                var h = EditorGUI.GetPropertyHeight(hitVfxOverrides, true);
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, h),
                    hitVfxOverrides,
                    new GUIContent("Hit VFX Overrides"),
                    true);
                y += h + PropertyDrawerHelper.Spacing;
            }

            return y;
        }

        protected override float GetSpecialFieldsHeight(SerializedProperty property)
        {
            var nudgeOverrides = property.FindPropertyRelative("_nudgeOverrides");
            var nudgeHeight = nudgeOverrides != null
                ? EditorGUI.GetPropertyHeight(nudgeOverrides, true) + PropertyDrawerHelper.Spacing
                : 0f;

            var hitVfxOverrides = property.FindPropertyRelative("_hitVfxOverrides");
            var hitVfxHeight = hitVfxOverrides != null
                ? EditorGUI.GetPropertyHeight(hitVfxOverrides, true) + PropertyDrawerHelper.Spacing
                : 0f;

            return nudgeHeight + hitVfxHeight;
        }
    }
}

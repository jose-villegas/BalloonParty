using System.Collections.Generic;
using BalloonParty.Editor;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Nudge.Editor
{
    [CustomPropertyDrawer(typeof(NudgeOverride))]
    public class NudgeOverrideDrawer : AutoFieldPropertyDrawer
    {
        /// <summary>
        ///     <c>_appliesTo</c> needs a custom <c>EditorGUI.EnumFlagsField</c> instead of
        ///     the default PropertyField. <c>_falloff</c> is conditional on the Shockwave flag.
        ///     Everything else (<c>_distance</c>, <c>_duration</c>) is drawn automatically.
        /// </summary>
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "_appliesTo",
            "_falloff"
        };

        protected override GUIContent BuildFoldoutLabel(GUIContent label, SerializedProperty property)
        {
            var appliesToProp = property.FindPropertyRelative("_appliesTo");
            if (appliesToProp == null)
            {
                return label;
            }

            var nudgeType = (NudgeType)appliesToProp.intValue;
            return new GUIContent($"{label.text}  [{nudgeType}]");
        }

        protected override float DrawPinnedFields(Rect position, float y, SerializedProperty property)
        {
            var appliesToProp = property.FindPropertyRelative("_appliesTo");
            if (appliesToProp == null)
            {
                return y;
            }

            appliesToProp.intValue = (int)(NudgeType)EditorGUI.EnumFlagsField(
                new Rect(position.x, y, position.width, PropertyDrawerHelper.LineHeight),
                new GUIContent("Applies To"),
                (NudgeType)appliesToProp.intValue);
            return y + PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
        }

        protected override float DrawSpecialFields(Rect position, float y, SerializedProperty property)
        {
            var appliesToProp = property.FindPropertyRelative("_appliesTo");
            if (appliesToProp != null && ((NudgeType)appliesToProp.intValue).HasFlag(NudgeType.Shockwave))
            {
                y = PropertyDrawerHelper.DrawNamedField(position, y, property, "_falloff", "Falloff");
            }

            return y;
        }

        protected override float GetPinnedFieldsHeight(SerializedProperty property)
        {
            return PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
        }

        protected override float GetSpecialFieldsHeight(SerializedProperty property)
        {
            var appliesToProp = property.FindPropertyRelative("_appliesTo");
            if (appliesToProp != null && ((NudgeType)appliesToProp.intValue).HasFlag(NudgeType.Shockwave))
            {
                return PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
            }

            return 0f;
        }
    }
}

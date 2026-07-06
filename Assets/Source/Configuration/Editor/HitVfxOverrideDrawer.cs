using System.Collections.Generic;
using BalloonParty.Editor;
using BalloonParty.Slots.Capabilities;
using UnityEditor;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(HitVfxOverride))]
    public class HitVfxOverrideDrawer : AutoFieldPropertyDrawer
    {
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "_appliesTo"
        };

        protected override GUIContent BuildFoldoutLabel(GUIContent label, SerializedProperty property)
        {
            var appliesToProp = property.FindPropertyRelative("_appliesTo");
            if (appliesToProp == null)
            {
                return label;
            }

            var outcome = (HitOutcome)appliesToProp.intValue;
            return new GUIContent($"{label.text}  [{outcome}]");
        }

        protected override float DrawPinnedFields(Rect position, float y, SerializedProperty property)
        {
            var appliesToProp = property.FindPropertyRelative("_appliesTo");
            if (appliesToProp != null)
            {
                appliesToProp.intValue = (int)(HitOutcome)EditorGUI.EnumFlagsField(
                    new Rect(position.x, y, position.width, PropertyDrawerHelper.LineHeight),
                    new GUIContent("Applies To"),
                    (HitOutcome)appliesToProp.intValue);
                y += PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
            }

            return y;
        }

        protected override float GetPinnedFieldsHeight(SerializedProperty property)
        {
            return PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
        }

        protected override float DrawSpecialFields(Rect position, float y, SerializedProperty property)
        {
            return y;
        }

        protected override float GetSpecialFieldsHeight(SerializedProperty property)
        {
            return 0f;
        }
    }
}

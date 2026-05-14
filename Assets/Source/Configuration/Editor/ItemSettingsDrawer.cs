using System.Collections.Generic;
using BalloonParty.Editor;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(ItemSettings))]
    public class ItemSettingsDrawer : AutoFieldPropertyDrawer
    {
        /// <summary>
        ///     Fields that belong to a specific item type and must NOT be drawn
        ///     in the shared common section. Add new type-specific field names here
        ///     when they are introduced; everything else is drawn automatically.
        /// </summary>
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "_bombRadius",
            "_nudgeOverrides",
            "_laserRaycastDistance",
            "_laserCircleCastRadius",
            "_lightningSegmentsMultiplier",
            "_lightningRandomness",
            "_lightningJumpTime"
        };

        protected override GUIContent BuildFoldoutLabel(GUIContent label, SerializedProperty property)
        {
            var itemType = (ItemType)property.FindPropertyRelative("_type").intValue;
            return new GUIContent($"{label.text}  [{itemType}]");
        }

        protected override float DrawSpecialFields(Rect position, float y, SerializedProperty property)
        {
            var itemType = (ItemType)property.FindPropertyRelative("_type").intValue;

            switch (itemType)
            {
                case ItemType.Bomb:
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Bomb");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, property, "_bombRadius", "Bomb Radius");
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

                    break;

                case ItemType.Laser:
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Laser");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        property,
                        "_laserRaycastDistance",
                        "Raycast Distance");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        property,
                        "_laserCircleCastRadius",
                        "Circle Cast Radius");
                    break;

                case ItemType.Lightning:
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Lightning");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        property,
                        "_lightningSegmentsMultiplier",
                        "Segments Multiplier");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        property,
                        "_lightningRandomness",
                        "Randomness");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, property, "_lightningJumpTime", "Jump Time");
                    break;
            }

            return y;
        }

        protected override float GetSpecialFieldsHeight(SerializedProperty property)
        {
            var itemType = (ItemType)property.FindPropertyRelative("_type").intValue;
            var row = PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;

            switch (itemType)
            {
                case ItemType.Bomb:
                    var nudgeOverrides = property.FindPropertyRelative("_nudgeOverrides");
                    var nudgeHeight = nudgeOverrides != null
                        ? EditorGUI.GetPropertyHeight(nudgeOverrides, true) + PropertyDrawerHelper.Spacing
                        : 0f;
                    return (row * 2) + nudgeHeight;

                case ItemType.Laser:
                    return row * 3;

                case ItemType.Lightning:
                    return row * 4;

                default:
                    return 0f;
            }
        }
    }
}

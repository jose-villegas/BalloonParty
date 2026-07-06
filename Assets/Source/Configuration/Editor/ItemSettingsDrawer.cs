using System.Collections.Generic;
using BalloonParty.Editor;
using UnityEditor;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(ItemSettings))]
    public class ItemSettingsDrawer : AutoFieldPropertyDrawer
    {
        /// <summary>
        ///     Skipped by the auto common-field pass: the per-type sub-settings containers and the
        ///     shared <c>_damage</c>, both drawn per active type in <see cref="DrawSpecialFields"/>.
        /// </summary>
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "_damage",
            "_bomb",
            "_laser",
            "_lightning",
            "_paint"
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
                    var bomb = property.FindPropertyRelative("_bomb");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Bomb");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, property, "_damage", "Damage");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, bomb, "_bombRadius", "Bomb Radius");
                    var nudgeOverrides = bomb?.FindPropertyRelative("_nudgeOverrides");
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
                    var laser = property.FindPropertyRelative("_laser");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Laser");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, property, "_damage", "Damage");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        laser,
                        "_laserRaycastDistance",
                        "Raycast Distance");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        laser,
                        "_laserCircleCastRadius",
                        "Circle Cast Radius");
                    break;

                case ItemType.Lightning:
                    var lightning = property.FindPropertyRelative("_lightning");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Lightning");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, property, "_damage", "Damage");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        lightning,
                        "_lightningSegmentsMultiplier",
                        "Segments Multiplier");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        lightning,
                        "_lightningRandomness",
                        "Randomness");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, lightning, "_lightningJumpTime", "Jump Time");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        lightning,
                        "_lightningGlowSubdivisions",
                        "Glow Subdivisions");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        lightning,
                        "_lightningFractalDecay",
                        "Fractal Decay");
                    break;

                case ItemType.Paint:
                    var paint = property.FindPropertyRelative("_paint");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Paint");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        paint,
                        "_paintBlobFlightDuration",
                        "Blob Flight Duration");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        paint,
                        "_paintBlobArcCurve",
                        "Blob Arc Curve");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        paint,
                        "_paintBlobScaleCurve",
                        "Blob Scale Curve");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        paint,
                        "_paintBlobShadowScaleCurve",
                        "Blob Shadow Scale Curve");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        paint,
                        "_paintBlobSpriteScaleCurve",
                        "Blob Sprite Scale Curve");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        paint,
                        "_paintBlobSpinSpeed",
                        "Blob Spin Speed");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Spread");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, paint, "_spreadOffset", "Offset");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, paint, "_spreadLength", "Length");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, paint, "_spreadBaseWidth", "Base Width");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, paint, "_spreadBlobRadius", "Blob Radius");
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
                    var bomb = property.FindPropertyRelative("_bomb");
                    var nudgeOverrides = bomb?.FindPropertyRelative("_nudgeOverrides");
                    var nudgeHeight = nudgeOverrides != null
                        ? EditorGUI.GetPropertyHeight(nudgeOverrides, true) + PropertyDrawerHelper.Spacing
                        : 0f;
                    return (row * 3) + nudgeHeight;

                case ItemType.Laser:
                    return row * 4;

                case ItemType.Lightning:
                    return row * 7;

                case ItemType.Paint:
                    // 1 "Paint" header + 6 blob fields + 1 "Spread" header + 4 spread fields.
                    return row * 12;

                default:
                    return 0f;
            }
        }
    }
}

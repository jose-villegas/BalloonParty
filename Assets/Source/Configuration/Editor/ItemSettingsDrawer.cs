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
        /// <summary>Skipped by the auto common-field pass; drawn per active type in <see cref="DrawSpecialFields"/> instead.</summary>
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "_damage",
            "_bomb",
            "_laser",
            "_lightning",
            "_paint",
            "_snipe"
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
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        bomb,
                        "_bombRainbowEffectScale",
                        "Rainbow Effect Scale");
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        bomb,
                        "_bombRainbowConversionRange",
                        "Rainbow Conversion Range");
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

                    y = PropertyDrawerHelper.DrawNamedField(position, y, bomb, "_blastLightRadiusScale", "Flash Light Radius Scale");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, bomb, "_blastLightIntensity", "Flash Light Intensity");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, bomb, "_blastLightFallbackSeconds", "Flash Light Fallback (s)");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Implosion");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, bomb, "_implosionRadiusScale", "Radius Scale");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, bomb, "_implosionStrengthScale", "Strength Scale");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, bomb, "_implosionDuration", "Duration (s)");
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
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        laser,
                        "_laserColorCycles",
                        "Color Cycles");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, laser, "_beamLightHalfWidth", "Beam Light Half Width");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, laser, "_beamLightIntensity", "Beam Light Intensity");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, laser, "_beamLightFalloff", "Beam Light Falloff");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, laser, "_beamLightFallbackSeconds", "Beam Light Fallback (s)");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Telegraph");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, laser, "_telegraphEnabled", "Enabled");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, laser, "_telegraphHalfLength", "Half Length");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, laser, "_telegraphHalfWidth", "Half Width");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, laser, "_telegraphIntensity", "Intensity");
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
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        lightning,
                        "_lightningGlowColorCycles",
                        "Glow Color Cycles");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, lightning, "_popLightRadius", "Pop Light Radius");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, lightning, "_popLightIntensity", "Pop Light Intensity");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, lightning, "_popLightSeconds", "Pop Light Seconds");
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
                    y = PropertyDrawerHelper.DrawNamedField(position,
                        y,
                        paint,
                        "_paintBlobColorCycles",
                        "Blob Color Cycles");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Spread");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, paint, "_spreadOffset", "Offset");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, paint, "_spreadLength", "Length");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, paint, "_spreadBaseWidth", "Base Width");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, paint, "_spreadBlobRadius", "Blob Radius");
                    break;

                case ItemType.Snipe:
                    var snipe = property.FindPropertyRelative("_snipe");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Snipe");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_snipeSpeedBuffMultiplier", "Speed Buff Multiplier");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_snipeToughHitSpeedFalloff", "Tough Hit Speed Falloff");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_snipeLineClearHalfWidth", "Line Clear Half Width");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Rainbow");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_snipeChargePerToughHit", "Charge Per Tough Hit");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_bloomBaseRadius", "Bloom Base Radius");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_bloomRadiusPerCharge", "Bloom Radius Per Charge");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_bloomRadiusCap", "Bloom Radius Cap");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_snipeColorCycles", "Color Cycles");
                    y = PropertyDrawerHelper.DrawSectionHeader(position, y, "Lights");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_tracerLightHalfWidth", "Tracer Light Half Width");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_tracerLightIntensity", "Tracer Light Intensity");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_dischargeLightIntensity", "Discharge Light Intensity");
                    y = PropertyDrawerHelper.DrawNamedField(position, y, snipe, "_lightFallbackSeconds", "Light Fallback (s)");
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
                    // Bomb header + damage + radius + 2 rainbow + 3 flash-light + Implosion header + 3 implosion.
                    return (row * 12) + nudgeHeight;

                case ItemType.Laser:
                    // Header + damage + 6 beam fields + telegraph header + 4 telegraph fields.
                    return row * 14;

                case ItemType.Lightning:
                    return row * 11;

                case ItemType.Paint:
                    // 2 headers + 7 blob fields + 4 spread fields.
                    return row * 13;

                case ItemType.Snipe:
                    // 3 headers (Snipe/Rainbow/Lights) + 3 + 5 + 4 fields.
                    return row * 15;

                default:
                    return 0f;
            }
        }
    }
}

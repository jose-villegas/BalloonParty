using System.Collections.Generic;
using BalloonParty.Configuration.Effects;
using BalloonParty.Editor;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(PaintProfile))]
    internal class PaintProfileDrawer : AutoFieldPropertyDrawer
    {
        protected override HashSet<string> ExcludedFields { get; } = new()
        {
            "PaletteColorName",
            "CustomColor"
        };

        protected override GUIContent BuildFoldoutLabel(GUIContent label, SerializedProperty property)
        {
            var sourcesProp = property.FindPropertyRelative("Sources");
            if (sourcesProp == null)
            {
                return label;
            }

            var sources = (PaintSource)sourcesProp.intValue;
            var name = sources != 0 ? sources.ToString() : "None";
            return new GUIContent(name);
        }

        protected override float DrawSpecialFields(Rect position, float y, SerializedProperty property)
        {
            var colorMode = (PaintColorMode)property.FindPropertyRelative("ColorMode").enumValueIndex;

            switch (colorMode)
            {
                case PaintColorMode.Palette:
                    y = PropertyDrawerHelper.DrawNamedField(
                        position, y, property, "PaletteColorName", "Palette Color");
                    break;
                case PaintColorMode.Custom:
                    y = PropertyDrawerHelper.DrawNamedField(
                        position, y, property, "CustomColor", "Color");
                    break;
            }

            return y;
        }

        protected override float GetSpecialFieldsHeight(SerializedProperty property)
        {
            var colorMode = (PaintColorMode)property.FindPropertyRelative("ColorMode").enumValueIndex;

            if (colorMode == PaintColorMode.Palette || colorMode == PaintColorMode.Custom)
            {
                return PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
            }

            return 0f;
        }
    }
}

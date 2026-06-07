using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Shared drawing utilities for custom <see cref="PropertyDrawer"/>s across the project.
    ///     All methods operate on Unity's <see cref="SerializedProperty"/> API so they work across
    ///     any serialized type without needing type-specific knowledge.
    /// </summary>
    public static class PropertyDrawerHelper
    {
        public const float LineHeight = 20f;
        public const float Spacing = 2f;

        /// <summary>
        ///     Returns the total pixel height required to draw all direct serialized children
        ///     of <paramref name="property"/> whose names are NOT in <paramref name="excluded"/>,
        ///     using each field's actual <see cref="EditorGUI.GetPropertyHeight"/> so that
        ///     variable-height drawers (e.g. <c>[PaletteColorMask]</c>) are measured correctly.
        /// </summary>
        public static float GetCommonFieldsHeight(SerializedProperty property, HashSet<string> excluded)
        {
            var total = 0f;
            ForEachCommonChild(property,
                excluded,
                child =>
                    total += EditorGUI.GetPropertyHeight(child, true) + Spacing);
            return total;
        }

        /// <summary>
        ///     Returns the number of direct serialized children of <paramref name="property"/>
        ///     whose names are NOT in <paramref name="excluded"/>.
        ///     Use <see cref="GetCommonFieldsHeight"/> for height calculation when any field
        ///     may have a variable-height drawer.
        /// </summary>
        public static int CountCommonFields(SerializedProperty property, HashSet<string> excluded)
        {
            var count = 0;
            ForEachCommonChild(property, excluded, _ => count++);
            return count;
        }

        /// <summary>
        ///     Draws every direct serialized child of <paramref name="property"/> whose name is
        ///     NOT in <paramref name="excluded"/>, in declaration order, using
        ///     <see cref="ObjectNames.NicifyVariableName"/> for the display label.
        ///     Returns the updated Y position after the last drawn field.
        /// </summary>
        public static float DrawCommonFields(
            Rect position,
            float y,
            SerializedProperty property,
            HashSet<string> excluded)
        {
            ForEachCommonChild(property, excluded, DrawField);
            return y;

            void DrawField(SerializedProperty child)
            {
                var displayName = ObjectNames.NicifyVariableName(child.name);
                var h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, h),
                    child,
                    new GUIContent(displayName));
                y += h + Spacing;
            }
        }

        /// <summary>
        ///     Draws a single field located by <paramref name="fieldName"/> on <paramref name="parent"/>
        ///     using an explicit <paramref name="displayName"/>.
        ///     Suitable for fields where the nicified name is not descriptive enough, or where
        ///     declaration order differs from the desired display order.
        ///     Returns the updated Y position, or the unchanged Y if the field is not found.
        /// </summary>
        public static float DrawNamedField(
            Rect position,
            float y,
            SerializedProperty parent,
            string fieldName,
            string displayName)
        {
            var prop = parent.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return y;
            }

            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, LineHeight),
                prop,
                new GUIContent(displayName));
            return y + LineHeight + Spacing;
        }

        /// <summary>
        ///     Draws a bold section-header label and advances Y by one line.
        /// </summary>
        public static float DrawSectionHeader(Rect position, float y, string title)
        {
            EditorGUI.LabelField(
                new Rect(position.x, y, position.width, LineHeight),
                title,
                EditorStyles.boldLabel);
            return y + LineHeight + Spacing;
        }

        /// <summary>
        ///     Draws a min/max slider with float fields on each side, matching Unity's
        ///     standard control alignment. Rect-based version for <see cref="PropertyDrawer"/>.
        /// </summary>
        public static void DrawMinMaxSlider(
            Rect rect, string label, ref float lo, ref float hi, float min, float max)
        {
            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            EditorGUI.LabelField(labelRect, label);

            var fieldW = EditorGUIUtility.fieldWidth;
            var controlX = rect.x + EditorGUIUtility.labelWidth + 2f;

            var loRect = new Rect(controlX, rect.y, fieldW, rect.height);
            var hiRect = new Rect(rect.xMax - fieldW, rect.y, fieldW, rect.height);
            var sliderRect = new Rect(
                loRect.xMax + 4f, rect.y + 2f,
                hiRect.x - loRect.xMax - 8f, rect.height - 4f);

            var prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            lo = RoundCentesimal(EditorGUI.FloatField(loRect, lo));
            EditorGUI.MinMaxSlider(sliderRect, ref lo, ref hi, min, max);
            hi = RoundCentesimal(EditorGUI.FloatField(hiRect, hi));

            EditorGUI.indentLevel = prevIndent;

            lo = Mathf.Clamp(lo, min, hi);
            hi = Mathf.Clamp(hi, lo, max);
        }

        /// <summary>
        ///     Draws a min/max slider with float fields on each side, matching Unity's
        ///     standard control alignment. Layout version for <see cref="EditorWindow"/>.
        /// </summary>
        public static void DrawMinMaxSliderLayout(
            string label, ref Vector2 range, float min, float max)
        {
            var rect = EditorGUILayout.GetControlRect();
            var lo = range.x;
            var hi = range.y;
            DrawMinMaxSlider(rect, label, ref lo, ref hi, min, max);
            range = new Vector2(lo, hi);
        }

        private static float RoundCentesimal(float value)
        {
            return Mathf.Round(value * 100f) / 100f;
        }

        /// <summary>
        ///     Visits every direct serialized child of <paramref name="root"/> whose
        ///     <c>name</c> is NOT present in <paramref name="excluded"/>,
        ///     passing a safe <c>Copy()</c> to <paramref name="visit"/>.
        /// </summary>
        public static void ForEachCommonChild(
            SerializedProperty root,
            HashSet<string> excluded,
            System.Action<SerializedProperty> visit)
        {
            var iter = root.Copy();
            var end = root.GetEndProperty();

            if (!iter.NextVisible(true))
            {
                return;
            }

            do
            {
                if (!excluded.Contains(iter.name))
                {
                    visit(iter.Copy());
                }
            } while (iter.NextVisible(false) && !SerializedProperty.EqualContents(iter, end));
        }
    }
}

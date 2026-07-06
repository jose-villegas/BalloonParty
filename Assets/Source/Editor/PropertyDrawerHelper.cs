using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Shared drawing utilities for custom <see cref="PropertyDrawer"/>s.
    /// </summary>
    public static class PropertyDrawerHelper
    {
        public const float LineHeight = 20f;
        public const float Spacing = 2f;

        /// <summary>
        ///     Total pixel height of <paramref name="property"/>'s children not in <paramref name="excluded"/>, accounting for variable-height drawers.
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
        ///     Number of direct serialized children of <paramref name="property"/> not in <paramref name="excluded"/>.
        /// </summary>
        public static int CountCommonFields(SerializedProperty property, HashSet<string> excluded)
        {
            var count = 0;
            ForEachCommonChild(property, excluded, _ => count++);
            return count;
        }

        /// <summary>
        ///     Draws every child of <paramref name="property"/> not in <paramref name="excluded"/>; returns the updated Y position.
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
        ///     Draws <paramref name="fieldName"/> on <paramref name="parent"/> with an explicit display name; returns Y unchanged if not found.
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
        ///     Min/max slider with float fields on each side. Rect-based version for <see cref="PropertyDrawer"/>.
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
        ///     Min/max slider with float fields on each side. Layout version for <see cref="EditorWindow"/>.
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
        ///     Visits every direct child of <paramref name="root"/> not in <paramref name="excluded"/>, passing a safe <c>Copy()</c> to <paramref name="visit"/>.
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

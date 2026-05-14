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
    internal static class PropertyDrawerHelper
    {
        internal const float LineHeight = 20f;
        internal const float Spacing = 2f;

        /// <summary>
        ///     Returns the number of direct serialized children of <paramref name="property"/>
        ///     whose names are NOT in <paramref name="excluded"/>.
        ///     Use this result to calculate <c>GetPropertyHeight</c> for the auto section.
        /// </summary>
        internal static int CountCommonFields(SerializedProperty property, HashSet<string> excluded)
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
        internal static float DrawCommonFields(
            Rect position,
            float y,
            SerializedProperty property,
            HashSet<string> excluded)
        {
            ForEachCommonChild(property,
                excluded,
                child =>
                {
                    var displayName = ObjectNames.NicifyVariableName(child.name);
                    EditorGUI.PropertyField(
                        new Rect(position.x, y, position.width, LineHeight),
                        child,
                        new GUIContent(displayName));
                    y += LineHeight + Spacing;
                });
            return y;
        }


        /// <summary>
        ///     Draws a single field located by <paramref name="fieldName"/> on <paramref name="parent"/>
        ///     using an explicit <paramref name="displayName"/>.
        ///     Suitable for fields where the nicified name is not descriptive enough, or where
        ///     declaration order differs from the desired display order.
        ///     Returns the updated Y position, or the unchanged Y if the field is not found.
        /// </summary>
        internal static float DrawNamedField(
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
        internal static float DrawSectionHeader(Rect position, float y, string title)
        {
            EditorGUI.LabelField(
                new Rect(position.x, y, position.width, LineHeight),
                title,
                EditorStyles.boldLabel);
            return y + LineHeight + Spacing;
        }


        /// <summary>
        ///     Visits every direct serialized child of <paramref name="root"/> whose
        ///     <c>name</c> is NOT present in <paramref name="excluded"/>,
        ///     passing a safe <c>Copy()</c> to <paramref name="visit"/>.
        /// </summary>
        internal static void ForEachCommonChild(
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

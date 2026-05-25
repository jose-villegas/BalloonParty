using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Base class for property drawers that want common fields drawn automatically
    ///     via serialized-property reflection, with a manually-handled section for
    ///     fields that need special layout (variable height, conditional visibility, etc.).
    ///
    ///     Subclasses must override <see cref="ExcludedFields"/>, <see cref="GetSpecialFieldsHeight"/>,
    ///     and <see cref="DrawSpecialFields"/>. Optionally override <see cref="BuildFoldoutLabel"/>,
    ///     <see cref="DrawPinnedFields"/>, and <see cref="GetPinnedFieldsHeight"/> for fields
    ///     that must appear above the auto-drawn common section.
    /// </summary>
    public abstract class AutoFieldPropertyDrawer : PropertyDrawer
    {
        /// <summary>
        ///     Serialized field names skipped by the automatic common-field pass.
        ///     Must be handled explicitly in <see cref="DrawPinnedFields"/> or <see cref="DrawSpecialFields"/>.
        /// </summary>
        protected abstract HashSet<string> ExcludedFields { get; }

        public sealed override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
            }

            var row = PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;
            var height = row;
            height += GetPinnedFieldsHeight(property);
            height += PropertyDrawerHelper.GetCommonFieldsHeight(property, ExcludedFields);
            height += GetSpecialFieldsHeight(property);

            return height;
        }

        public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var headerRect = new Rect(position.x, position.y, position.width, PropertyDrawerHelper.LineHeight);
            property.isExpanded = EditorGUI.Foldout(
                headerRect,
                property.isExpanded,
                BuildFoldoutLabel(label, property),
                true);

            if (property.isExpanded)
            {
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = indent + 1;

                var y = position.y + PropertyDrawerHelper.LineHeight + PropertyDrawerHelper.Spacing;

                y = DrawPinnedFields(position, y, property);
                y = PropertyDrawerHelper.DrawCommonFields(position, y, property, ExcludedFields);
                DrawSpecialFields(position, y, property);

                EditorGUI.indentLevel = indent;
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        ///     Builds the label shown on the foldout header row.
        ///     Override to append type tags or other contextual text.
        ///     Default returns <paramref name="label"/> unchanged.
        /// </summary>
        protected virtual GUIContent BuildFoldoutLabel(GUIContent label, SerializedProperty property)
        {
            return label;
        }

        /// <summary>
        ///     Draws fields that must appear above the auto section, such as an enum
        ///     discriminator whose value drives <see cref="BuildFoldoutLabel"/>.
        ///     Returns the updated Y position.
        ///     Override together with <see cref="GetPinnedFieldsHeight"/>. Default does nothing.
        /// </summary>
        protected virtual float DrawPinnedFields(Rect position, float y, SerializedProperty property)
        {
            return y;
        }

        /// <summary>
        ///     Draws the excluded fields below the auto section and returns the updated Y position.
        /// </summary>
        protected abstract float DrawSpecialFields(Rect position, float y, SerializedProperty property);

        /// <summary>
        ///     Returns the pixel height of any fields pinned above the auto section.
        ///     Override together with <see cref="DrawPinnedFields"/>. Default returns 0.
        /// </summary>
        protected virtual float GetPinnedFieldsHeight(SerializedProperty property)
        {
            return 0f;
        }

        /// <summary>
        ///     Returns the total pixel height required by the special (excluded) section.
        /// </summary>
        protected abstract float GetSpecialFieldsHeight(SerializedProperty property);
    }
}

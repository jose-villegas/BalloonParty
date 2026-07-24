using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Hides a <c>[ShowIfEnum]</c> field unless its named sibling enum matches one of the
    /// attribute's values, collapsing the row (no leftover gap) when hidden.</summary>
    [CustomPropertyDrawer(typeof(ShowIfEnumAttribute))]
    internal class ShowIfEnumDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return IsShown(property)
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : -EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IsShown(property))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        private bool IsShown(SerializedProperty property)
        {
            var showIf = (ShowIfEnumAttribute)attribute;
            var enumProperty = FindSibling(property, showIf.EnumFieldName);
            if (enumProperty == null || enumProperty.propertyType != SerializedPropertyType.Enum)
            {
                // Can't resolve the condition — fail open rather than hide a field the author needs.
                return true;
            }

            foreach (var value in showIf.Values)
            {
                if (enumProperty.enumValueIndex == value)
                {
                    return true;
                }
            }

            return false;
        }

        // Resolves a field on the same object as property, swapping the last path element so it also
        // works inside nested structs, not just top-level ScriptableObject fields.
        private static SerializedProperty FindSibling(SerializedProperty property, string siblingName)
        {
            var path = property.propertyPath;
            var dot = path.LastIndexOf('.');
            return dot < 0
                ? property.serializedObject.FindProperty(siblingName)
                : property.serializedObject.FindProperty(path.Substring(0, dot + 1) + siblingName);
        }
    }
}

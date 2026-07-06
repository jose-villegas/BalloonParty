using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Draws each element of an <see cref="EnumIndexedAttribute"/> array using the enum value's name
    ///     as its label. The attribute sits on the array field; Unity invokes this drawer per element, and
    ///     the element index is read from the property path (e.g. <c>_motions.Array.data[1]</c>).
    /// </summary>
    [CustomPropertyDrawer(typeof(EnumIndexedAttribute))]
    public class EnumIndexedDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var names = ((EnumIndexedAttribute)attribute).Names;
            var index = ElementIndex(property.propertyPath);
            if (index >= 0 && index < names.Length)
            {
                label = new GUIContent(names[index]);
            }

            EditorGUI.PropertyField(position, property, label, true);
        }

        // Element paths look like "_motions.Array.data[3]" — pull the 3 out of the trailing [ ].
        private static int ElementIndex(string propertyPath)
        {
            var open = propertyPath.LastIndexOf('[');
            if (open < 0 || !propertyPath.EndsWith("]", System.StringComparison.Ordinal))
            {
                return -1;
            }

            var inner = propertyPath.Substring(open + 1, propertyPath.Length - open - 2);
            return int.TryParse(inner, out var index) ? index : -1;
        }
    }
}

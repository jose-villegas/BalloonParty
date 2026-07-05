using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Renders a <see cref="SortingLayerNameAttribute"/> string field as a popup of the project's
    ///     sorting layers (the same list Unity shows on a renderer's Sorting Layer field).
    /// </summary>
    [CustomPropertyDrawer(typeof(SortingLayerNameAttribute))]
    public class SortingLayerNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var layers = SortingLayer.layers;
            var names = new string[layers.Length];
            var current = 0;
            for (var i = 0; i < layers.Length; i++)
            {
                names[i] = layers[i].name;
                if (layers[i].name == property.stringValue)
                {
                    current = i;
                }
            }

            EditorGUI.BeginProperty(position, label, property);
            var selected = EditorGUI.Popup(position, label.text, current, names);
            if (selected >= 0 && selected < names.Length)
            {
                property.stringValue = names[selected];
            }

            EditorGUI.EndProperty();
        }
    }
}

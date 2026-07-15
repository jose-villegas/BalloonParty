using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Draws common <see cref="SerializedProperty"/> field types inside absolute-positioned
    ///     table cells. Each method resolves the sub-property and renders it inset within the cell.
    /// </summary>
    internal static class PropertyCellDrawer
    {
        /// <summary>Draws a single int field for a sub-property of <paramref name="parent"/>.</summary>
        internal static void IntCell(Rect cell, SerializedProperty parent, string fieldName, float padding = 2f)
        {
            var prop = parent.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            prop.intValue = EditorGUI.IntField(fieldRect, prop.intValue);
        }

        /// <summary>Draws an AnimationCurve field for a sub-property of <paramref name="parent"/>.</summary>
        internal static void CurveCell(Rect cell, SerializedProperty parent, string fieldName, float padding = 2f)
        {
            var prop = parent.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            EditorGUI.PropertyField(fieldRect, prop, GUIContent.none);
        }

        /// <summary>Draws a generic property field (no label) for a sub-property of <paramref name="parent"/>.</summary>
        internal static void PropertyCell(Rect cell, SerializedProperty parent, string fieldName, float padding = 2f)
        {
            var prop = parent.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            EditorGUI.PropertyField(fieldRect, prop, GUIContent.none);
        }

        /// <summary>Draws an int field directly on the provided property.</summary>
        internal static void IntCell(Rect cell, SerializedProperty prop, float padding = 2f)
        {
            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            prop.intValue = EditorGUI.IntField(fieldRect, prop.intValue);
        }

        /// <summary>Draws a float field directly on the provided property.</summary>
        internal static void FloatCell(Rect cell, SerializedProperty prop, float padding = 2f)
        {
            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            prop.floatValue = EditorGUI.FloatField(fieldRect, prop.floatValue);
        }
    }
}

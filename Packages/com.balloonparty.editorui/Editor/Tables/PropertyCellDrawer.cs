using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Tables
{
    /// <summary>Draws common SerializedProperty field types inside absolute-positioned table cells.</summary>
    public static class PropertyCellDrawer
    {
        /// <summary>Draws a single int field for a sub-property.</summary>
        public static void IntCell(Rect cell, SerializedProperty parent, string fieldName, float padding = 2f)
        {
            var prop = parent.FindPropertyRelative(fieldName);

            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            prop.intValue = EditorGUI.IntField(fieldRect, prop.intValue);
        }

        /// <summary>Draws an AnimationCurve field for a sub-property.</summary>
        public static void CurveCell(Rect cell, SerializedProperty parent, string fieldName, float padding = 2f)
        {
            var prop = parent.FindPropertyRelative(fieldName);

            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            EditorGUI.PropertyField(fieldRect, prop, GUIContent.none);
        }

        /// <summary>Draws a generic property field (no label) for a sub-property.</summary>
        public static void PropertyCell(Rect cell, SerializedProperty parent, string fieldName, float padding = 2f)
        {
            var prop = parent.FindPropertyRelative(fieldName);

            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            EditorGUI.PropertyField(fieldRect, prop, GUIContent.none);
        }

        /// <summary>Draws a single float field for a sub-property.</summary>
        public static void FloatCell(Rect cell, SerializedProperty parent, string fieldName, float padding = 2f)
        {
            var prop = parent.FindPropertyRelative(fieldName);

            if (prop == null)
            {
                return;
            }

            FloatCell(cell, prop, padding);
        }

        /// <summary>Draws an int field directly on the provided property.</summary>
        public static void IntCell(Rect cell, SerializedProperty prop, float padding = 2f)
        {
            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            prop.intValue = EditorGUI.IntField(fieldRect, prop.intValue);
        }

        /// <summary>Draws a float field directly on the provided property.</summary>
        public static void FloatCell(Rect cell, SerializedProperty prop, float padding = 2f)
        {
            if (prop == null)
            {
                return;
            }

            var fieldRect = TableDrawHelper.InsetCell(cell, padding);
            prop.floatValue = EditorGUI.FloatField(fieldRect, prop.floatValue);
        }

        /// <summary>Draws a RangedInt cell with min/max int fields and an enum popup for range mode.</summary>
        public static void RangedIntCell(Rect cell, SerializedProperty parent, string fieldName, float padding = 2f)
        {
            var prop = parent.FindPropertyRelative(fieldName);

            if (prop == null)
            {
                return;
            }

            var minProp = prop.FindPropertyRelative("_min");
            var maxProp = prop.FindPropertyRelative("_max");
            var modeProp = prop.FindPropertyRelative("_mode");

            var modeW = 50f;
            var fieldW = (cell.width - modeW - 14f) / 2f;
            var y = cell.y + padding;
            var h = cell.height - padding * 2f;

            var minRect = new Rect(cell.x + padding, y, fieldW, h);
            var slashRect = new Rect(minRect.xMax, y, 10f, h);
            var maxRect = new Rect(slashRect.xMax, y, fieldW, h);
            var modeRect = new Rect(maxRect.xMax + 2f, y, modeW, h);

            minProp.intValue = EditorGUI.IntField(minRect, minProp.intValue);
            EditorGUI.LabelField(slashRect, "/");
            maxProp.intValue = EditorGUI.IntField(maxRect, maxProp.intValue);
            modeProp.enumValueIndex = EditorGUI.Popup(modeRect, modeProp.enumValueIndex, modeProp.enumDisplayNames);
        }

        /// <summary>Draws two int fields side-by-side separated by a dash character.</summary>
        public static void IntRangeCell(
            Rect cell, SerializedProperty fromProp, SerializedProperty toProp,
            string separator = "–", float padding = 2f)
        {
            var w = (cell.width - 14f) / 2f;
            var y = cell.y + padding;
            var h = cell.height - padding * 2f;

            var fromRect = new Rect(cell.x + padding, y, w, h);
            var dashRect = new Rect(fromRect.xMax, y, 10f, h);
            var toRect = new Rect(dashRect.xMax, y, w, h);

            fromProp.intValue = EditorGUI.IntField(fromRect, fromProp.intValue);
            EditorGUI.LabelField(dashRect, separator);
            toProp.intValue = EditorGUI.IntField(toRect, toProp.intValue);
        }
    }
}

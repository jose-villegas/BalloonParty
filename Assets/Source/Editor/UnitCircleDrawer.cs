using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Unit-circle direction picker for <c>[UnitCircle]</c> Vector2 fields: click or drag
    /// inside the disc to aim the vector (written back normalized; GUI y-down is flipped to the
    /// world's y-up). The numeric row above and the angle field beside the disc stay editable for
    /// exact values — the angle is degrees CCW from +x (0° = right, 90° = up), matching the
    /// full-circle gradient mapping (<see cref="VectorMathExtensions.Angle01"/>).</summary>
    [CustomPropertyDrawer(typeof(UnitCircleAttribute))]
    internal class UnitCircleDrawer : PropertyDrawer
    {
        private const float DiscSize = 84f;
        private const float DiscPadding = 4f;
        private const float NeedleDotRadius = 3.5f;

        private static readonly GUIContent AngleLabel = new(
            "Angle°", "Direction in degrees, counter-clockwise from +x (0° = right, 90° = up). " +
                      "Matches the full-circle gradient mapping (t = angle / 360).");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight
                   + EditorGUIUtility.standardVerticalSpacing
                   + DiscSize
                   + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            var typed = EditorGUI.Vector2Field(line, label, property.vector2Value);
            if (EditorGUI.EndChangeCheck())
            {
                property.vector2Value = typed;
            }

            var discRect = new Rect(
                position.x + EditorGUIUtility.labelWidth,
                line.yMax + EditorGUIUtility.standardVerticalSpacing,
                DiscSize,
                DiscSize);
            var center = discRect.center;
            var radius = DiscSize * 0.5f - DiscPadding;

            HandleDiscInput(discRect, center, radius, property);
            DrawAngleField(position, discRect, property);

            if (Event.current.type == EventType.Repaint)
            {
                DrawDisc(center, radius, property.vector2Value);
            }

            EditorGUI.EndProperty();
        }

        // hotControl-based so a press inside the disc keeps steering while the mouse is held, even
        // once the cursor leaves the rect — the "hold" half of click-or-hold aiming.
        private static void HandleDiscInput(Rect discRect, Vector2 center, float radius, SerializedProperty property)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive, discRect);

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && (e.mousePosition - center).magnitude <= radius)
                    {
                        GUIUtility.hotControl = id;
                        ApplyDirection(center, e.mousePosition, property);
                        e.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        ApplyDirection(center, e.mousePosition, property);
                        e.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }

                    break;
            }
        }

        // A degrees field in the empty gutter left of the disc — the exact-value counterpart to dragging
        // the needle. Degrees CCW from +x so it reads as the gradient's t × 360; the vector is written back
        // from the angle (already unit-length), keeping every entry point normalized.
        private static void DrawAngleField(Rect position, Rect discRect, SerializedProperty property)
        {
            const float labelWidth = 44f;

            var gutter = discRect.x - position.x - EditorGUIUtility.standardVerticalSpacing;
            if (gutter <= labelWidth + EditorGUIUtility.fieldWidth)
            {
                return;
            }

            var angleRect = new Rect(
                position.x,
                discRect.y + (DiscSize - EditorGUIUtility.singleLineHeight) * 0.5f,
                gutter,
                EditorGUIUtility.singleLineHeight);

            // FloatField aligns its edit box to the global label column, which sits under the disc — shrink
            // the label width so the label and box both stay inside the gutter, then restore it.
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;

            var degrees = property.vector2Value.Angle01() * 360f;
            EditorGUI.BeginChangeCheck();
            var typed = EditorGUI.FloatField(angleRect, AngleLabel, degrees);
            if (EditorGUI.EndChangeCheck())
            {
                property.vector2Value = VectorMathExtensions.DirectionFromAngle(typed * Mathf.Deg2Rad);
                GUI.changed = true;
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private static void ApplyDirection(Vector2 center, Vector2 mouse, SerializedProperty property)
        {
            // GUI space runs y-down; the stored vector is world-space y-up.
            var dir = new Vector2(mouse.x - center.x, center.y - mouse.y);
            if (dir.sqrMagnitude < 1e-6f)
            {
                return;
            }

            property.vector2Value = dir.normalized;
            GUI.changed = true;
        }

        private static void DrawDisc(Vector2 center, float radius, Vector2 value)
        {
            var center3 = new Vector3(center.x, center.y, 0f);

            Handles.color = new Color(1f, 1f, 1f, 0.04f);
            Handles.DrawSolidDisc(center3, Vector3.forward, radius);

            // Faint axes cross so the cardinal directions read at a glance.
            Handles.color = new Color(1f, 1f, 1f, 0.12f);
            Handles.DrawLine(center3 + Vector3.left * radius, center3 + Vector3.right * radius);
            Handles.DrawLine(center3 + Vector3.down * radius, center3 + Vector3.up * radius);

            Handles.color = new Color(1f, 1f, 1f, 0.45f);
            Handles.DrawWireDisc(center3, Vector3.forward, radius);

            if (value.sqrMagnitude > 1e-6f)
            {
                var dir = value.normalized;
                var end = center3 + new Vector3(dir.x, -dir.y, 0f) * radius;
                Handles.color = new Color(1f, 0.85f, 0.3f, 0.95f);
                Handles.DrawLine(center3, end);
                Handles.DrawSolidDisc(end, Vector3.forward, NeedleDotRadius);
            }
        }
    }
}

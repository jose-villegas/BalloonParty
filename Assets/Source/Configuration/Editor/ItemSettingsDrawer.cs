using UnityEditor;
using UnityEngine;

namespace BalloonParty.Configuration.Editor
{
    [CustomPropertyDrawer(typeof(ItemSettings))]
    public class ItemSettingsDrawer : PropertyDrawer
    {
        private const float LineHeight = 20f;
        private const float Spacing = 2f;

        // Common fields count (excluding the foldout header row itself):
        //   _type, _turnCheckEvery, _weight, _maximumAllowed, _visualPrefab, _activationEffectPrefab
        private const int CommonFieldCount = 6;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return LineHeight + Spacing;
            }

            var itemType = (ItemType)property.FindPropertyRelative("_type").intValue;

            // Foldout header + common fields
            var height = (1 + CommonFieldCount) * (LineHeight + Spacing);

            switch (itemType)
            {
                case ItemType.Bomb:
                    height += LineHeight + Spacing; // section label
                    height += LineHeight + Spacing; // _bombRadius
                    var nudgeOverrides = property.FindPropertyRelative("_nudgeOverrides");
                    if (nudgeOverrides != null)
                    {
                        height += EditorGUI.GetPropertyHeight(nudgeOverrides, true) + Spacing;
                    }
                    break;

                case ItemType.Laser:
                    height += LineHeight + Spacing; // section label
                    height += (LineHeight + Spacing) * 2; // _laserRaycastDistance, _laserCircleCastRadius
                    break;

                case ItemType.Lightning:
                    height += LineHeight + Spacing; // section label
                    height += (LineHeight + Spacing) * 3; // _lightningSegmentsMultiplier, _lightningRandomness, _lightningJumpTime
                    break;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("_type");
            var itemType = (ItemType)typeProp.intValue;

            var foldoutLabel = new GUIContent($"{label.text}  [{itemType}]");
            var headerRect = new Rect(position.x, position.y, position.width, LineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, foldoutLabel, true);

            if (property.isExpanded)
            {
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = indent + 1;

                var y = position.y + LineHeight + Spacing;

                y = DrawField(position, y, property, "_type", "Type");
                y = DrawField(position, y, property, "_turnCheckEvery", "Turn Check Every");
                y = DrawField(position, y, property, "_weight", "Weight");
                y = DrawField(position, y, property, "_maximumAllowed", "Maximum Allowed");
                y = DrawField(position, y, property, "_visualPrefab", "Visual Prefab");
                y = DrawField(position, y, property, "_activationEffectPrefab", "Activation Effect Prefab");

                switch (itemType)
                {
                    case ItemType.Bomb:
                        y = DrawSectionHeader(position, y, "Bomb");
                        y = DrawField(position, y, property, "_bombRadius", "Bomb Radius");
                        var nudgeOverrides = property.FindPropertyRelative("_nudgeOverrides");
                        if (nudgeOverrides != null)
                        {
                            var h = EditorGUI.GetPropertyHeight(nudgeOverrides, true);
                            EditorGUI.PropertyField(
                                new Rect(position.x, y, position.width, h),
                                nudgeOverrides,
                                new GUIContent("Nudge Overrides"),
                                true);
                        }
                        break;

                    case ItemType.Laser:
                        y = DrawSectionHeader(position, y, "Laser");
                        y = DrawField(position, y, property, "_laserRaycastDistance", "Raycast Distance");
                        DrawField(position, y, property, "_laserCircleCastRadius", "Circle Cast Radius");
                        break;

                    case ItemType.Lightning:
                        y = DrawSectionHeader(position, y, "Lightning");
                        y = DrawField(position, y, property, "_lightningSegmentsMultiplier", "Segments Multiplier");
                        y = DrawField(position, y, property, "_lightningRandomness", "Randomness");
                        DrawField(position, y, property, "_lightningJumpTime", "Jump Time");
                        break;
                }

                EditorGUI.indentLevel = indent;
            }

            EditorGUI.EndProperty();
        }

        private static float DrawSectionHeader(Rect position, float y, string title)
        {
            var rect = new Rect(position.x, y, position.width, LineHeight);
            EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);
            return y + LineHeight + Spacing;
        }

        private static float DrawField(
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
    }
}

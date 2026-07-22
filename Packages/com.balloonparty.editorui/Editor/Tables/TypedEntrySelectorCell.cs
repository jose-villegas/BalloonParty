using System;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Tables
{
    public sealed class TypedEntrySelectorCell<TEnum> where TEnum : struct, Enum
    {
        private readonly CellConfig<TEnum> _config;
        private readonly TEnum[] _enumValues;
        private readonly string[] _enumNames;
        private readonly int[] _enumIntValues;

        private int[] _selectedPerRow = Array.Empty<int>();

        private static GUIStyle CenteredMiniLabelStyle => StyleCache.Get(
            "TypedEntrySelectorCell.CenteredMiniLabel",
            () => new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            });

        public TypedEntrySelectorCell(CellConfig<TEnum> config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _enumValues = (TEnum[])Enum.GetValues(typeof(TEnum));
            _enumNames = Enum.GetNames(typeof(TEnum));
            _enumIntValues = new int[_enumValues.Length];

            for (var i = 0; i < _enumValues.Length; i++)
            {
                _enumIntValues[i] = Convert.ToInt32(_enumValues[i]);
            }
        }

        public void DrawCollapsed(Rect cell, SerializedProperty arrayProp)
        {
            if (arrayProp == null || !arrayProp.isArray)
            {
                return;
            }

            var activeCount = 0;
            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                var entryProp = arrayProp.GetArrayElementAtIndex(i);
                if (IsEntryActive(entryProp))
                {
                    activeCount++;
                }
            }

            var labelRect = new Rect(cell.x + 2f, cell.y, 30f, cell.height);
            EditorGUI.LabelField(labelRect, activeCount.ToString(), EditorStyles.miniLabel);

            var x = labelRect.xMax;
            var thumbSize = cell.height - 4f;

            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                var entryProp = arrayProp.GetArrayElementAtIndex(i);
                if (!IsEntryActive(entryProp))
                {
                    continue;
                }

                var typeProp = entryProp.FindPropertyRelative(_config.TypeFieldName);
                if (typeProp == null || !TryGetEnumIndex(typeProp.intValue, out var enumIndex))
                {
                    continue;
                }

                var thumbRect = new Rect(x, cell.y + (cell.height - thumbSize) / 2f, thumbSize, thumbSize);
                var thumbnail = _config.GetThumbnail?.Invoke(_enumIntValues[enumIndex]);
                if (thumbnail != null)
                {
                    GUI.DrawTexture(thumbRect, thumbnail, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(thumbRect, new Color(0.4f, 0.4f, 0.4f, 0.6f));
                    EditorGUI.LabelField(thumbRect, _enumNames[enumIndex][0].ToString(), CenteredMiniLabelStyle);
                }

                x += thumbSize + 2f;
            }
        }

        public void DrawExpanded(Rect cell, SerializedProperty arrayProp, int rowIndex)
        {
            if (arrayProp == null || !arrayProp.isArray)
            {
                return;
            }

            EnsureSelectionArraySize(rowIndex);

            var presentInArray = new bool[_enumValues.Length];
            for (var i = 0; i < _enumValues.Length; i++)
            {
                presentInArray[i] = FindEntryIndex(arrayProp, _enumIntValues[i]) >= 0;
            }

            var selectedIndex = Mathf.Clamp(_selectedPerRow[rowIndex], 0, _enumValues.Length - 1);
            var dropdownNames = BuildDropdownNames(_enumNames, presentInArray, selectedIndex);
            var dropdownRect = new Rect(cell.x + 2f, cell.y, _config.DropdownWidth, cell.height);
            var newSelectedIndex = EditorGUI.Popup(dropdownRect, selectedIndex, dropdownNames);
            if (newSelectedIndex != selectedIndex)
            {
                _selectedPerRow[rowIndex] = newSelectedIndex;
                selectedIndex = newSelectedIndex;

                var enumIntValue = _enumIntValues[selectedIndex];
                if (FindEntryIndex(arrayProp, enumIntValue) < 0)
                {
                    var newIndex = arrayProp.arraySize;
                    arrayProp.InsertArrayElementAtIndex(newIndex);
                    var newEntry = arrayProp.GetArrayElementAtIndex(newIndex);
                    var typeProp = newEntry.FindPropertyRelative(_config.TypeFieldName);
                    if (typeProp != null)
                    {
                        typeProp.intValue = enumIntValue;
                    }

                    _config.InitializeNewEntry?.Invoke(newEntry);
                }
            }

            var x = dropdownRect.xMax + 4f;
            var enumInt = _enumIntValues[selectedIndex];
            var entryIndex = FindEntryIndex(arrayProp, enumInt);
            if (entryIndex < 0)
            {
                EditorGUI.LabelField(new Rect(x, cell.y, 120f, cell.height), "Select a type", EditorStyles.miniLabel);
                return;
            }

            var entryProp = arrayProp.GetArrayElementAtIndex(entryIndex);
            for (var i = 0; i < _config.Fields.Count; i++)
            {
                var field = _config.Fields[i];
                var fieldRect = new Rect(x, cell.y, field.Width, cell.height);
                DrawField(fieldRect, entryProp, field);
                x += field.Width + 4f;
            }

            if (GUI.Button(new Rect(x, cell.y, 18f, cell.height), "−", EditorStyles.miniButton))
            {
                arrayProp.DeleteArrayElementAtIndex(entryIndex);
            }
        }

        public void DrawSubHeaders(Rect cell)
        {
            var x = cell.x + 2f + _config.DropdownWidth + 4f;

            for (var i = 0; i < _config.Fields.Count; i++)
            {
                var field = _config.Fields[i];
                EditorGUI.LabelField(new Rect(x, cell.y, field.Width, cell.height), field.SubHeader, CenteredMiniLabelStyle);
                x += field.Width + 4f;
            }
        }

        public int FindEntryIndex(SerializedProperty arrayProp, int enumIntValue)
        {
            if (arrayProp == null || !arrayProp.isArray)
            {
                return -1;
            }

            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                var typeProp = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative(_config.TypeFieldName);
                if (typeProp != null && typeProp.intValue == enumIntValue)
                {
                    return i;
                }
            }

            return -1;
        }

        public void SelectType(int rowIndex, TEnum selectedType)
        {
            EnsureSelectionArraySize(rowIndex);

            var enumIntValue = Convert.ToInt32(selectedType);
            if (TryGetEnumIndex(enumIntValue, out var enumIndex))
            {
                _selectedPerRow[rowIndex] = enumIndex;
            }
        }

        internal static string[] BuildDropdownNames(string[] enumNames, bool[] presentInArray, int selectedIndex)
        {
            var names = new string[enumNames.Length];

            for (var i = 0; i < enumNames.Length; i++)
            {
                if (i == selectedIndex)
                {
                    names[i] = $"► {enumNames[i]}";
                }
                else
                {
                    names[i] = presentInArray[i] ? $"✓ {enumNames[i]}" : $"  {enumNames[i]}";
                }
            }

            return names;
        }

        private void DrawField(Rect rect, SerializedProperty entryProp, FieldSpec field)
        {
            switch (field.DrawMode)
            {
                case FieldDrawMode.Float:
                    PropertyCellDrawer.FloatCell(rect, entryProp, field.PropertyPath);
                    break;

                case FieldDrawMode.Int:
                    PropertyCellDrawer.IntCell(rect, entryProp, field.PropertyPath);
                    break;

                case FieldDrawMode.Curve:
                    PropertyCellDrawer.CurveCell(rect, entryProp, field.PropertyPath);
                    break;

                case FieldDrawMode.Property:
                {
                    var property = entryProp.FindPropertyRelative(field.PropertyPath);
                    if (property != null)
                    {
                        EditorGUI.PropertyField(rect, property, GUIContent.none);
                    }

                    break;
                }

                case FieldDrawMode.RangedInt:
                    PropertyCellDrawer.RangedIntCell(rect, entryProp, field.PropertyPath);
                    break;
            }
        }

        private void EnsureSelectionArraySize(int rowIndex)
        {
            if (_selectedPerRow.Length > rowIndex)
            {
                return;
            }

            var resized = new int[rowIndex + 16];
            Array.Copy(_selectedPerRow, resized, _selectedPerRow.Length);
            _selectedPerRow = resized;
        }

        private bool IsEntryActive(SerializedProperty entryProp)
        {
            return _config.IsEntryActive?.Invoke(entryProp) ?? true;
        }

        private bool TryGetEnumIndex(int enumIntValue, out int enumIndex)
        {
            for (var i = 0; i < _enumIntValues.Length; i++)
            {
                if (_enumIntValues[i] == enumIntValue)
                {
                    enumIndex = i;
                    return true;
                }
            }

            enumIndex = -1;
            return false;
        }
    }
}

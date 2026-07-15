using System.Linq;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Spreadsheet-style view of <see cref="LevelPacingConfiguration"/>: each range is a row,
    /// parameters are columns. Easier to compare across levels than the default vertical Inspector.</summary>
    internal sealed class LevelPacingWindow : EditorWindow
    {
        private const float RowHeight = 22f;
        private const float SeparatorWidth = 1f;
        private const float SwatchSize = 10f;
        private const float CurveFieldWidth = 80f;
        private const float GroupGap = 20f;
        private const int BalloonColIndex = 5;
        private const int ItemColIndex = 6;
        private const float BalloonExpandedWidth = 310f;
        private const float ItemExpandedWidth = 170f;

        // Columns that have a visual gap before them (group separators)
        private static readonly int[] GapBeforeCols = { 4, 6 };

        private static readonly float[] ColWidths =
        {
            90f,   // 0: Range
            40f,   // 1: Spawn (single int)
            40f,   // 2: Board (single int)
            40f,   // 3: 1st Turn (single int)
            130f,  // 4: Colors (dropdown + swatches)
            100f,  // 5: Balloons (collapsed) — dynamic when expanded
            100f,  // 6: Items — dynamic when expanded
            130f,  // 7: Cadence (min/max + mode)
            80f,   // 8: Initial Count curve
            80f,   // 9: Wave Count curve
            24f,   // 10: ►
            20f,   // 11: −
        };

        private static readonly string[] ColHeaders =
        {
            "Range", "Spawn", "Board", "1st Turn", "Colors", "Balloons", "Items", "Cadence", "Init Count", "Wave Count", "", ""
        };

        private readonly ConfigAssetCache<LevelPacingConfiguration> _assetCache = new();
        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();
        private readonly ConfigAssetCache<BalloonsConfiguration> _balloonsConfigCache = new();

        private string[] _paletteNames;
        private Color[] _paletteColors;
        private int[] _selectedBalloonPerRow = System.Array.Empty<int>();
        private int[] _selectedItemPerRow = System.Array.Empty<int>();

        private LevelPacingConfiguration _asset;
        private SerializedObject _serialized;
        private SerializedProperty _rangesProp;
        private Vector2 _scroll;
        private int _expandedRow = -1;
        private bool _balloonsExpanded;
        private bool _itemsExpanded;
        private float _collapsedBalloonColWidth = ColWidths[BalloonColIndex];
        private float _collapsedItemColWidth = ColWidths[ItemColIndex];

        private float EffectiveBalloonColWidth => _balloonsExpanded ? BalloonExpandedWidth : _collapsedBalloonColWidth;
        private float EffectiveItemColWidth => _itemsExpanded ? ItemExpandedWidth : _collapsedItemColWidth;

        [MenuItem("Tools/BalloonParty/Level Pacing")]
        private static void Open()
        {
            GetWindow<LevelPacingWindow>("Level Pacing");
        }

        private void OnEnable()
        {
            TryLoadAsset();
            var palette = _paletteCache.Value;
            if (palette != null)
            {
                _paletteNames = palette.Colors.Select(c => c.Name).ToArray();
                _paletteColors = palette.Colors.Select(c => c.Color).ToArray();
            }
            else
            {
                _paletteNames = new[] { "0", "1", "2", "3", "4", "5", "6", "7" };
                _paletteColors = System.Array.Empty<Color>();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_asset == null || _serialized == null)
            {
                EditorGUILayout.HelpBox("Assign a LevelPacingConfiguration asset above.", MessageType.Info);
                return;
            }

            _serialized.Update();
            _rangesProp = _serialized.FindProperty("_ranges");

            if (_rangesProp == null || !_rangesProp.isArray)
            {
                EditorGUILayout.HelpBox("Cannot read _ranges property.", MessageType.Error);
                return;
            }

            DrawTable();
            _serialized.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            var newAsset = (LevelPacingConfiguration)EditorGUILayout.ObjectField(
                _asset, typeof(LevelPacingConfiguration), false, GUILayout.Width(250));
            if (EditorGUI.EndChangeCheck() && newAsset != _asset)
            {
                _asset = newAsset;
                _serialized = _asset != null ? new SerializedObject(_asset) : null;
            }

            GUILayout.FlexibleSpace();

            if (_asset != null && GUILayout.Button("Select Asset", EditorStyles.toolbarButton))
            {
                Selection.activeObject = _asset;
                EditorGUIUtility.PingObject(_asset);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTable()
        {
            var count = _rangesProp.arraySize;
            if (count == 0)
            {
                EditorGUILayout.HelpBox("No ranges defined.", MessageType.Warning);
            }

            // Compute collapsed balloon column width from the widest row
            if (!_balloonsExpanded)
            {
                var thumbSize = RowHeight - 4f;
                var maxActive = 0;
                for (var i = 0; i < count; i++)
                {
                    var paramsProp = _rangesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_parameters");
                    var bProp = paramsProp?.FindPropertyRelative("_balloonWeights");
                    if (bProp == null || !bProp.isArray)
                    {
                        continue;
                    }

                    var active = 0;
                    for (var j = 0; j < bProp.arraySize; j++)
                    {
                        var w = bProp.GetArrayElementAtIndex(j).FindPropertyRelative("_weight");
                        if (w != null && w.floatValue > 0f)
                        {
                            active++;
                        }
                    }

                    if (active > maxActive)
                    {
                        maxActive = active;
                    }
                }

                _collapsedBalloonColWidth = Mathf.Max(
                    ColWidths[BalloonColIndex],
                    32f + maxActive * (thumbSize + 2f));
            }

            // Compute collapsed item column width from the widest row
            if (!_itemsExpanded)
            {
                var maxActiveItems = 0;
                for (var i = 0; i < count; i++)
                {
                    var paramsProp = _rangesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_parameters");
                    var iProp = paramsProp?.FindPropertyRelative("_itemWeights");
                    if (iProp == null || !iProp.isArray)
                    {
                        continue;
                    }

                    var active = 0;
                    for (var j = 0; j < iProp.arraySize; j++)
                    {
                        var w = iProp.GetArrayElementAtIndex(j).FindPropertyRelative("_weight");
                        if (w != null && w.floatValue > 0f)
                        {
                            active++;
                        }
                    }

                    if (active > maxActiveItems)
                    {
                        maxActiveItems = active;
                    }
                }

                _collapsedItemColWidth = Mathf.Max(
                    ColWidths[ItemColIndex],
                    32f + maxActiveItems * (RowHeight - 4f + 2f));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Header
            var headerRect = GUILayoutUtility.GetRect(TotalWidth(), RowHeight);
            var headerBg = new Color(0.18f, 0.18f, 0.18f, 1f);
            DrawGroupBackground(headerRect, 0, 3, headerBg);
            DrawGroupBackground(headerRect, 4, 5, headerBg);
            DrawGroupBackground(headerRect, 6, 9, headerBg);
            DrawHeaderCells(headerRect);

            // Rows
            for (var i = 0; i < count; i++)
            {
                DrawRow(i);
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("+ Add Range", GUILayout.Width(120f)))
            {
                _rangesProp.InsertArrayElementAtIndex(count);
            }

            EditorGUILayout.EndScrollView();
        }

        private float TotalWidth()
        {
            var total = 0f;
            for (var i = 0; i < ColWidths.Length; i++)
            {
                if (HasGapBefore(i))
                {
                    total += GroupGap;
                }

                total += EffectiveColWidth(i) + SeparatorWidth;
            }

            return total;
        }

        private float ColX(int col)
        {
            var x = 0f;
            for (var i = 0; i < col; i++)
            {
                if (HasGapBefore(i))
                {
                    x += GroupGap;
                }

                x += EffectiveColWidth(i) + SeparatorWidth;
            }

            if (HasGapBefore(col))
            {
                x += GroupGap;
            }

            return x;
        }

        private Rect CellRect(Rect rowRect, int col)
        {
            return new Rect(rowRect.x + ColX(col), rowRect.y, EffectiveColWidth(col), rowRect.height);
        }

        private static bool HasGapBefore(int col)
        {
            for (var i = 0; i < GapBeforeCols.Length; i++)
            {
                if (GapBeforeCols[i] == col)
                {
                    return true;
                }
            }

            return false;
        }

        private float EffectiveColWidth(int col)
        {
            if (col == BalloonColIndex)
            {
                return EffectiveBalloonColWidth;
            }

            if (col == ItemColIndex)
            {
                return EffectiveItemColWidth;
            }

            return ColWidths[col];
        }

        private void DrawGroupBackground(Rect rowRect, int fromCol, int toCol, Color bg)
        {
            var x = rowRect.x + ColX(fromCol);
            var xEnd = rowRect.x + ColX(toCol) + EffectiveColWidth(toCol);
            var groupRect = new Rect(x, rowRect.y, xEnd - x, rowRect.height);
            EditorGUI.DrawRect(groupRect, bg);
        }

        private void DrawHeaderCells(Rect rowRect)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };

            for (var i = 0; i < ColHeaders.Length; i++)
            {
                if (string.IsNullOrEmpty(ColHeaders[i]))
                {
                    continue;
                }

                var cell = CellRect(rowRect, i);

                if (i == BalloonColIndex)
                {
                    var toggle = _balloonsExpanded ? "▼" : "►";
                    var label = $"{toggle} Balloons";
                    if (GUI.Button(cell, label, EditorStyles.boldLabel))
                    {
                        _balloonsExpanded = !_balloonsExpanded;
                    }

                    if (_balloonsExpanded)
                    {
                        DrawBalloonSubHeaders(cell);
                    }
                }
                else if (i == ItemColIndex)
                {
                    var toggle = _itemsExpanded ? "▼" : "►";
                    var label = $"{toggle} Items";
                    if (GUI.Button(cell, label, EditorStyles.boldLabel))
                    {
                        _itemsExpanded = !_itemsExpanded;
                    }

                    if (_itemsExpanded)
                    {
                        DrawItemSubHeaders(cell);
                    }
                }
                else
                {
                    EditorGUI.LabelField(cell, ColHeaders[i], style);
                }

                // Separator
                var sep = new Rect(cell.xMax, rowRect.y, SeparatorWidth, rowRect.height);
                EditorGUI.DrawRect(sep, new Color(0.35f, 0.35f, 0.35f, 0.8f));
            }
        }

        private static void DrawBalloonSubHeaders(Rect cell)
        {
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            var x = cell.x + 74f;
            var y = cell.y;
            var h = cell.height;

            EditorGUI.LabelField(new Rect(x, y, 36f, h), "Wt", subStyle);
            x += 40f;
            EditorGUI.LabelField(new Rect(x, y, CurveFieldWidth, h), "Initial", subStyle);
            x += CurveFieldWidth + 2f;
            EditorGUI.LabelField(new Rect(x, y, CurveFieldWidth, h), "Wave", subStyle);
        }

        private void DrawRow(int index)
        {
            var entryProp = _rangesProp.GetArrayElementAtIndex(index);
            var fromProp = entryProp.FindPropertyRelative("_fromLevel");
            var toProp = entryProp.FindPropertyRelative("_toLevel");
            var paramsProp = entryProp.FindPropertyRelative("_parameters");

            var from = fromProp.intValue;
            var to = toProp.intValue;
            var isFallback = from < 0 || to < 0;

            var balloonsProp = paramsProp?.FindPropertyRelative("_balloonWeights");
            var rowRect = GUILayoutUtility.GetRect(TotalWidth(), RowHeight);

            // Background — draw per group for floating panel effect
            Color rowBg;
            if (isFallback)
            {
                rowBg = new Color(0.25f, 0.32f, 0.38f, 1f);
            }
            else if (index % 2 == 1)
            {
                rowBg = new Color(0.24f, 0.24f, 0.24f, 1f);
            }
            else
            {
                rowBg = new Color(0.21f, 0.21f, 0.21f, 1f);
            }

            DrawGroupBackground(rowRect, 0, 3, rowBg);
            DrawGroupBackground(rowRect, 4, 5, rowBg);
            DrawGroupBackground(rowRect, 6, 9, rowBg);

            // Separators
            for (var i = 0; i < ColWidths.Length; i++)
            {
                var colW = EffectiveColWidth(i);
                var sep = new Rect(rowRect.x + ColX(i) + colW, rowRect.y, SeparatorWidth, rowRect.height);
                EditorGUI.DrawRect(sep, new Color(0.35f, 0.35f, 0.35f, 0.5f));
            }

            // Range (col 0)
            DrawRangeCell(CellRect(rowRect, 0), fromProp, toProp, isFallback);

            if (paramsProp != null)
            {
                DrawIntCell(CellRect(rowRect, 1), paramsProp, "_spawnLines");
                DrawIntCell(CellRect(rowRect, 2), paramsProp, "_boardLines");
                DrawIntCell(CellRect(rowRect, 3), paramsProp, "_firstSpawnTurn");
                DrawMaskCell(CellRect(rowRect, 4), paramsProp);

                if (_balloonsExpanded)
                {
                    DrawBalloonCellExpanded(CellRect(rowRect, BalloonColIndex), balloonsProp, index);
                }
                else
                {
                    DrawBalloonCellCollapsed(CellRect(rowRect, BalloonColIndex), balloonsProp);
                }

                var itemsProp = paramsProp.FindPropertyRelative("_itemWeights");
                if (_itemsExpanded)
                {
                    DrawItemCellExpanded(CellRect(rowRect, ItemColIndex), itemsProp, index);
                }
                else
                {
                    DrawItemCellCollapsed(CellRect(rowRect, ItemColIndex), itemsProp);
                }

                DrawRangedIntCell(CellRect(rowRect, 7), paramsProp, "_itemCadence");
                DrawCurveCell(CellRect(rowRect, 8), paramsProp, "_initialItemCountWeights");
                DrawCurveCell(CellRect(rowRect, 9), paramsProp, "_itemCountWeights");
            }

            // ► button (col 10)
            var expandRect = CellRect(rowRect, 10);
            if (GUI.Button(expandRect, _expandedRow == index ? "▼" : "►"))
            {
                _expandedRow = _expandedRow == index ? -1 : index;
            }

            // − button — right-aligned to scroll area
            var delW = 20f;
            var delX = Mathf.Max(CellRect(rowRect, 11).x, position.width - delW - 4f + _scroll.x);
            var delRect = new Rect(delX, rowRect.y, delW, rowRect.height);
            if (GUI.Button(delRect, "−"))
            {
                _rangesProp.DeleteArrayElementAtIndex(index);
                if (_expandedRow == index)
                {
                    _expandedRow = -1;
                }

                return;
            }

            if (_expandedRow == index && paramsProp != null)
            {
                DrawExpandedDetails(paramsProp);
            }
        }

        private static void DrawRangeCell(Rect cell, SerializedProperty fromProp, SerializedProperty toProp, bool isFallback)
        {
            if (isFallback)
            {
                EditorGUI.LabelField(cell, "Fallback", EditorStyles.miniLabel);
                return;
            }

            var w = (cell.width - 14f) / 2f;
            var fromRect = new Rect(cell.x + 2f, cell.y + 2f, w, cell.height - 4f);
            var dashRect = new Rect(fromRect.xMax, cell.y + 2f, 10f, cell.height - 4f);
            var toRect = new Rect(dashRect.xMax, cell.y + 2f, w, cell.height - 4f);

            fromProp.intValue = EditorGUI.IntField(fromRect, fromProp.intValue);
            EditorGUI.LabelField(dashRect, "–");
            toProp.intValue = EditorGUI.IntField(toRect, toProp.intValue);
        }

        private static void DrawIntCell(Rect cell, SerializedProperty paramsProp, string fieldName)
        {
            var prop = paramsProp.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }

            var fieldRect = new Rect(cell.x + 2f, cell.y + 2f, cell.width - 4f, cell.height - 4f);
            prop.intValue = EditorGUI.IntField(fieldRect, prop.intValue);
        }

        private static void DrawRangedIntCell(Rect cell, SerializedProperty paramsProp, string fieldName)
        {
            var prop = paramsProp.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }

            var minProp = prop.FindPropertyRelative("_min");
            var maxProp = prop.FindPropertyRelative("_max");
            var modeProp = prop.FindPropertyRelative("_mode");

            var modeW = 50f;
            var fieldW = (cell.width - modeW - 14f) / 2f;
            var y = cell.y + 2f;
            var h = cell.height - 4f;

            var minRect = new Rect(cell.x + 2f, y, fieldW, h);
            var slashRect = new Rect(minRect.xMax, y, 10f, h);
            var maxRect = new Rect(slashRect.xMax, y, fieldW, h);
            var modeRect = new Rect(maxRect.xMax + 2f, y, modeW, h);

            minProp.intValue = EditorGUI.IntField(minRect, minProp.intValue);
            EditorGUI.LabelField(slashRect, "/");
            maxProp.intValue = EditorGUI.IntField(maxRect, maxProp.intValue);
            modeProp.enumValueIndex = (int)(RangeMode)EditorGUI.EnumPopup(modeRect, (RangeMode)modeProp.enumValueIndex);
        }

        private static void DrawCurveCell(Rect cell, SerializedProperty paramsProp, string fieldName)
        {
            var prop = paramsProp.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }

            var fieldRect = new Rect(cell.x + 2f, cell.y + 2f, cell.width - 4f, cell.height - 4f);
            EditorGUI.PropertyField(fieldRect, prop, GUIContent.none);
        }

        private void DrawMaskCell(Rect cell, SerializedProperty paramsProp)
        {
            var maskProp = paramsProp.FindPropertyRelative("_allowedColorsMask");
            if (maskProp == null)
            {
                return;
            }

            var dropdownRect = new Rect(cell.x + 2f, cell.y + 2f, 70f, cell.height - 4f);
            var newMask = EditorGUI.MaskField(dropdownRect, maskProp.intValue, _paletteNames);
            if (newMask != maskProp.intValue)
            {
                maskProp.intValue = newMask;
            }

            // Color swatches
            var x = dropdownRect.xMax + 4f;
            var mask = maskProp.intValue;
            for (var i = 0; i < _paletteColors.Length; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                var swatch = new Rect(x, cell.y + (cell.height - SwatchSize) / 2f, SwatchSize, SwatchSize);
                EditorGUI.DrawRect(swatch, _paletteColors[i]);
                x += SwatchSize + 2f;
            }
        }

        private void DrawBalloonCellCollapsed(Rect cell, SerializedProperty balloonsProp)
        {
            if (balloonsProp == null || !balloonsProp.isArray)
            {
                return;
            }

            var count = balloonsProp.arraySize;
            var activeCount = 0;
            for (var i = 0; i < count; i++)
            {
                var weightProp = balloonsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_weight");
                if (weightProp != null && weightProp.floatValue > 0f)
                {
                    activeCount++;
                }
            }

            // Count label
            var labelRect = new Rect(cell.x + 2f, cell.y, 30f, cell.height);
            EditorGUI.LabelField(labelRect, activeCount.ToString(), EditorStyles.miniLabel);

            // Prefab thumbnails
            var x = labelRect.xMax;
            var thumbSize = cell.height - 4f;
            var balloonsConfig = _balloonsConfigCache.Value;

            for (var i = 0; i < count; i++)
            {
                var entryProp = balloonsProp.GetArrayElementAtIndex(i);
                var weightProp = entryProp.FindPropertyRelative("_weight");
                if (weightProp == null || weightProp.floatValue <= 0f)
                {
                    continue;
                }

                var typeProp = entryProp.FindPropertyRelative("_type");
                if (typeProp == null)
                {
                    continue;
                }

                var balloonType = (BalloonType)typeProp.intValue;
                var preview = FindBalloonPreview(balloonsConfig, balloonType);
                var thumbRect = new Rect(x, cell.y + (cell.height - thumbSize) / 2f, thumbSize, thumbSize);

                if (preview != null)
                {
                    GUI.DrawTexture(thumbRect, preview, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(thumbRect, new Color(0.4f, 0.4f, 0.4f, 0.6f));
                    var initial = balloonType.ToString()[0].ToString();
                    EditorGUI.LabelField(thumbRect, initial, new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    });
                }

                x += thumbSize + 2f;
            }
        }

        private void DrawBalloonCellExpanded(Rect cell, SerializedProperty balloonsProp, int rowIndex)
        {
            if (balloonsProp == null || !balloonsProp.isArray)
            {
                return;
            }

            // Ensure selection array is large enough
            if (_selectedBalloonPerRow.Length <= rowIndex)
            {
                var newArr = new int[rowIndex + 16];
                System.Array.Copy(_selectedBalloonPerRow, newArr, _selectedBalloonPerRow.Length);
                _selectedBalloonPerRow = newArr;
            }

            var count = balloonsProp.arraySize;
            var allTypes = (BalloonType[])System.Enum.GetValues(typeof(BalloonType));
            var typeNames = new string[allTypes.Length];
            var selectedTypeIndex = _selectedBalloonPerRow[rowIndex];
            selectedTypeIndex = Mathf.Clamp(selectedTypeIndex, 0, allTypes.Length - 1);

            for (var i = 0; i < allTypes.Length; i++)
            {
                if (i == selectedTypeIndex)
                {
                    typeNames[i] = allTypes[i].ToString();
                }
                else
                {
                    var present = FindBalloonEntryIndex(balloonsProp, allTypes[i]) >= 0;
                    typeNames[i] = present ? $"✓ {allTypes[i]}" : $"+ {allTypes[i]}";
                }
            }

            var x = cell.x + 2f;
            var y = cell.y + 2f;
            var h = cell.height - 4f;
            var dropdownW = 70f;

            var newTypeIndex = EditorGUI.Popup(new Rect(x, y, dropdownW, h), selectedTypeIndex, typeNames);
            if (newTypeIndex != selectedTypeIndex)
            {
                _selectedBalloonPerRow[rowIndex] = newTypeIndex;
                selectedTypeIndex = newTypeIndex;

                // If this type isn't in the list yet, add it
                var selectedType = allTypes[selectedTypeIndex];
                if (FindBalloonEntryIndex(balloonsProp, selectedType) < 0)
                {
                    balloonsProp.InsertArrayElementAtIndex(count);
                    var newEntry = balloonsProp.GetArrayElementAtIndex(count);
                    newEntry.FindPropertyRelative("_type").intValue = (int)selectedType;
                    newEntry.FindPropertyRelative("_weight").floatValue = 1f;
                    count++;
                }
            }

            x += dropdownW + 4f;

            // Find the entry for the currently selected type
            var selectedBalloonType = allTypes[selectedTypeIndex];
            var entryIndex = FindBalloonEntryIndex(balloonsProp, selectedBalloonType);

            if (entryIndex >= 0)
            {
                var entryProp = balloonsProp.GetArrayElementAtIndex(entryIndex);
                var weightProp = entryProp.FindPropertyRelative("_weight");
                var initialCurveProp = entryProp.FindPropertyRelative("_initialCountWeights");
                var waveCurveProp = entryProp.FindPropertyRelative("_waveCountWeights");

                // Weight
                var weightW = 36f;
                if (weightProp != null)
                {
                    weightProp.floatValue = EditorGUI.FloatField(new Rect(x, y, weightW, h), weightProp.floatValue);
                }

                x += weightW + 4f;

                // Initial count curve
                if (initialCurveProp != null)
                {
                    EditorGUI.PropertyField(new Rect(x, y, CurveFieldWidth, h), initialCurveProp, GUIContent.none);
                }

                x += CurveFieldWidth + 2f;

                // Wave count curve
                if (waveCurveProp != null)
                {
                    EditorGUI.PropertyField(new Rect(x, y, CurveFieldWidth, h), waveCurveProp, GUIContent.none);
                }

                x += CurveFieldWidth + 2f;

                // Remove button
                if (GUI.Button(new Rect(x, y, 18f, h), "−", EditorStyles.miniButton))
                {
                    balloonsProp.DeleteArrayElementAtIndex(entryIndex);
                }
            }
            else
            {
                EditorGUI.LabelField(new Rect(x, y, 120f, h), "Select a type", EditorStyles.miniLabel);
            }
        }

        private static int FindBalloonEntryIndex(SerializedProperty balloonsProp, BalloonType type)
        {
            for (var i = 0; i < balloonsProp.arraySize; i++)
            {
                var typeProp = balloonsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_type");
                if (typeProp != null && typeProp.intValue == (int)type)
                {
                    return i;
                }
            }

            return -1;
        }

        private static Texture2D FindBalloonPreview(BalloonsConfiguration config, BalloonType type)
        {
            if (config == null)
            {
                return null;
            }

            foreach (var entry in config.Entries)
            {
                if (entry.BalloonType == type && entry.Prefab != null)
                {
                    return AssetPreview.GetAssetPreview(entry.Prefab.gameObject);
                }
            }

            return null;
        }

        private static void DrawItemSubHeaders(Rect cell)
        {
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            var x = cell.x + 74f;
            var y = cell.y;
            var h = cell.height;

            EditorGUI.LabelField(new Rect(x, y, 36f, h), "Wt", subStyle);
            x += 40f;
            EditorGUI.LabelField(new Rect(x, y, 36f, h), "Max", subStyle);
        }

        private static void DrawItemCellCollapsed(Rect cell, SerializedProperty itemsProp)
        {
            if (itemsProp == null || !itemsProp.isArray)
            {
                return;
            }

            var count = itemsProp.arraySize;
            var activeCount = 0;
            for (var i = 0; i < count; i++)
            {
                var weightProp = itemsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_weight");
                if (weightProp != null && weightProp.floatValue > 0f)
                {
                    activeCount++;
                }
            }

            var labelRect = new Rect(cell.x + 2f, cell.y, 30f, cell.height);
            EditorGUI.LabelField(labelRect, activeCount.ToString(), EditorStyles.miniLabel);

            // Item type initials as small labels
            var x = labelRect.xMax;
            var thumbSize = cell.height - 4f;

            for (var i = 0; i < count; i++)
            {
                var entryProp = itemsProp.GetArrayElementAtIndex(i);
                var weightProp = entryProp.FindPropertyRelative("_weight");
                if (weightProp == null || weightProp.floatValue <= 0f)
                {
                    continue;
                }

                var typeProp = entryProp.FindPropertyRelative("_type");
                if (typeProp == null)
                {
                    continue;
                }

                var itemType = (ItemType)typeProp.intValue;
                var thumbRect = new Rect(x, cell.y + (cell.height - thumbSize) / 2f, thumbSize, thumbSize);
                EditorGUI.DrawRect(thumbRect, new Color(0.4f, 0.4f, 0.4f, 0.6f));
                EditorGUI.LabelField(thumbRect, itemType.ToString()[0].ToString(),
                    new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
                x += thumbSize + 2f;
            }
        }

        private void DrawItemCellExpanded(Rect cell, SerializedProperty itemsProp, int rowIndex)
        {
            if (itemsProp == null || !itemsProp.isArray)
            {
                return;
            }

            // Ensure selection array is large enough
            if (_selectedItemPerRow.Length <= rowIndex)
            {
                var newArr = new int[rowIndex + 16];
                System.Array.Copy(_selectedItemPerRow, newArr, _selectedItemPerRow.Length);
                _selectedItemPerRow = newArr;
            }

            var count = itemsProp.arraySize;
            var allTypes = (ItemType[])System.Enum.GetValues(typeof(ItemType));
            var typeNames = new string[allTypes.Length];
            var selectedTypeIndex = _selectedItemPerRow[rowIndex];
            selectedTypeIndex = Mathf.Clamp(selectedTypeIndex, 0, allTypes.Length - 1);

            for (var i = 0; i < allTypes.Length; i++)
            {
                if (i == selectedTypeIndex)
                {
                    typeNames[i] = allTypes[i].ToString();
                }
                else
                {
                    var present = FindItemEntryIndex(itemsProp, allTypes[i]) >= 0;
                    typeNames[i] = present ? $"✓ {allTypes[i]}" : $"+ {allTypes[i]}";
                }
            }

            var x = cell.x + 2f;
            var y = cell.y + 2f;
            var h = cell.height - 4f;
            var dropdownW = 70f;

            var newTypeIndex = EditorGUI.Popup(new Rect(x, y, dropdownW, h), selectedTypeIndex, typeNames);
            if (newTypeIndex != selectedTypeIndex)
            {
                _selectedItemPerRow[rowIndex] = newTypeIndex;
                selectedTypeIndex = newTypeIndex;

                var selectedType = allTypes[selectedTypeIndex];
                if (FindItemEntryIndex(itemsProp, selectedType) < 0)
                {
                    itemsProp.InsertArrayElementAtIndex(count);
                    var newEntry = itemsProp.GetArrayElementAtIndex(count);
                    newEntry.FindPropertyRelative("_type").intValue = (int)selectedType;
                    newEntry.FindPropertyRelative("_weight").floatValue = 1f;
                    newEntry.FindPropertyRelative("_maximumAllowedOverride").intValue = 0;
                    count++;
                }
            }

            x += dropdownW + 4f;

            var selectedItemType = allTypes[selectedTypeIndex];
            var entryIndex = FindItemEntryIndex(itemsProp, selectedItemType);

            if (entryIndex >= 0)
            {
                var entryProp = itemsProp.GetArrayElementAtIndex(entryIndex);
                var weightProp = entryProp.FindPropertyRelative("_weight");
                var maxProp = entryProp.FindPropertyRelative("_maximumAllowedOverride");

                // Weight
                var weightW = 36f;
                if (weightProp != null)
                {
                    weightProp.floatValue = EditorGUI.FloatField(new Rect(x, y, weightW, h), weightProp.floatValue);
                }

                x += weightW + 4f;

                // Max
                if (maxProp != null)
                {
                    maxProp.intValue = EditorGUI.IntField(new Rect(x, y, 30f, h), maxProp.intValue);
                }

                x += 34f;

                // Remove button
                if (GUI.Button(new Rect(x, y, 18f, h), "−", EditorStyles.miniButton))
                {
                    itemsProp.DeleteArrayElementAtIndex(entryIndex);
                }
            }
            else
            {
                EditorGUI.LabelField(new Rect(x, y, 120f, h), "Select a type", EditorStyles.miniLabel);
            }
        }

        private static int FindItemEntryIndex(SerializedProperty itemsProp, ItemType type)
        {
            for (var i = 0; i < itemsProp.arraySize; i++)
            {
                var typeProp = itemsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_type");
                if (typeProp != null && typeProp.intValue == (int)type)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void DrawExpandedDetails(SerializedProperty paramsProp)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");

            var balloons = paramsProp.FindPropertyRelative("_balloonWeights");
            if (balloons != null)
            {
                EditorGUILayout.PropertyField(balloons, new GUIContent("Balloon Weights"), true);
            }

            var items = paramsProp.FindPropertyRelative("_itemWeights");
            if (items != null)
            {
                EditorGUILayout.PropertyField(items, new GUIContent("Item Weights"), true);
            }

            var actors = paramsProp.FindPropertyRelative("_gridActorGates");
            if (actors != null)
            {
                EditorGUILayout.PropertyField(actors, new GUIContent("Grid Actor Gates"), true);
            }

            var initItems = paramsProp.FindPropertyRelative("_initialItemCountWeights");
            if (initItems != null)
            {
                EditorGUILayout.PropertyField(initItems, new GUIContent("Initial Item Weights"));
            }

            var itemCounts = paramsProp.FindPropertyRelative("_itemCountWeights");
            if (itemCounts != null)
            {
                EditorGUILayout.PropertyField(itemCounts, new GUIContent("Item Count Weights"));
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void TryLoadAsset()
        {
            if (_asset != null)
            {
                _serialized = new SerializedObject(_asset);
                return;
            }

            _asset = _assetCache.Value;
            _serialized = _asset != null ? new SerializedObject(_asset) : null;
        }
    }
}

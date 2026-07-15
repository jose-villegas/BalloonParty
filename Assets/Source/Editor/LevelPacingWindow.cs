using System.Linq;
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

        private static readonly float[] ColWidths =
        {
            90f,   // Range
            40f,   // Spawn (single int)
            40f,   // Board (single int)
            130f,  // Cadence (min/max + mode)
            40f,   // 1st Turn (single int)
            130f,  // Colors (dropdown + swatches)
            100f,  // Balloons
            100f,  // Items
            20f,   // −
            24f,   // ►
        };

        private static readonly string[] ColHeaders =
        {
            "Range", "Spawn", "Board", "Cadence", "1st Turn", "Colors", "Balloons", "Items", "", ""
        };

        private readonly ConfigAssetCache<LevelPacingConfiguration> _assetCache = new();
        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();

        private string[] _paletteNames;
        private Color[] _paletteColors;

        private LevelPacingConfiguration _asset;
        private SerializedObject _serialized;
        private SerializedProperty _rangesProp;
        private Vector2 _scroll;
        private int _expandedRow = -1;

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

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Header
            var headerRect = GUILayoutUtility.GetRect(TotalWidth(), RowHeight);
            EditorGUI.DrawRect(headerRect, new Color(0.18f, 0.18f, 0.18f, 1f));
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

        private static float TotalWidth()
        {
            var total = 0f;
            for (var i = 0; i < ColWidths.Length; i++)
            {
                total += ColWidths[i] + SeparatorWidth;
            }

            return total;
        }

        private static float ColX(int col)
        {
            var x = 0f;
            for (var i = 0; i < col; i++)
            {
                x += ColWidths[i] + SeparatorWidth;
            }

            return x;
        }

        private static Rect CellRect(Rect rowRect, int col)
        {
            return new Rect(rowRect.x + ColX(col), rowRect.y, ColWidths[col], rowRect.height);
        }

        private static void DrawHeaderCells(Rect rowRect)
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
                EditorGUI.LabelField(cell, ColHeaders[i], style);

                // Separator
                var sep = new Rect(cell.xMax, rowRect.y, SeparatorWidth, rowRect.height);
                EditorGUI.DrawRect(sep, new Color(0.35f, 0.35f, 0.35f, 0.8f));
            }
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

            var rowRect = GUILayoutUtility.GetRect(TotalWidth(), RowHeight);

            // Background
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

            EditorGUI.DrawRect(rowRect, rowBg);

            // Separators
            for (var i = 0; i < ColWidths.Length - 1; i++)
            {
                var sep = new Rect(rowRect.x + ColX(i) + ColWidths[i], rowRect.y, SeparatorWidth, rowRect.height);
                EditorGUI.DrawRect(sep, new Color(0.35f, 0.35f, 0.35f, 0.5f));
            }

            // Range (col 0)
            DrawRangeCell(CellRect(rowRect, 0), fromProp, toProp, isFallback);

            if (paramsProp != null)
            {
                DrawIntCell(CellRect(rowRect, 1), paramsProp, "_spawnLines");
                DrawIntCell(CellRect(rowRect, 2), paramsProp, "_boardLines");
                DrawRangedIntCell(CellRect(rowRect, 3), paramsProp, "_itemCadence");
                DrawIntCell(CellRect(rowRect, 4), paramsProp, "_firstSpawnTurn");
                DrawMaskCell(CellRect(rowRect, 5), paramsProp);
                DrawWeightsCell(CellRect(rowRect, 6), paramsProp, "_balloonWeights", "Balloon");
                DrawWeightsCell(CellRect(rowRect, 7), paramsProp, "_itemWeights", "Item");
            }

            // − button (col 8)
            var delRect = CellRect(rowRect, 8);
            if (GUI.Button(delRect, "−"))
            {
                _rangesProp.DeleteArrayElementAtIndex(index);
                if (_expandedRow == index)
                {
                    _expandedRow = -1;
                }

                return;
            }

            // ► button (col 9)
            var expandRect = CellRect(rowRect, 9);
            if (GUI.Button(expandRect, _expandedRow == index ? "▼" : "►"))
            {
                _expandedRow = _expandedRow == index ? -1 : index;
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

        private static void DrawWeightsCell(Rect cell, SerializedProperty paramsProp, string fieldName, string kind)
        {
            var prop = paramsProp.FindPropertyRelative(fieldName);
            if (prop == null || !prop.isArray)
            {
                return;
            }

            var count = prop.arraySize;
            var activeCount = 0;
            for (var i = 0; i < count; i++)
            {
                var weightProp = prop.GetArrayElementAtIndex(i).FindPropertyRelative("_weight");
                if (weightProp != null && weightProp.floatValue > 0f)
                {
                    activeCount++;
                }
            }

            var labelRect = new Rect(cell.x + 4f, cell.y, cell.width - 4f, cell.height);
            EditorGUI.LabelField(labelRect, $"{activeCount} {kind}(s)", EditorStyles.miniLabel);
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

using System.Linq;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Spreadsheet-style view of <see cref="LevelPacingConfiguration"/>: each range is a row,
    /// parameters are columns. Easier to compare across levels than the default vertical Inspector.</summary>
    internal sealed class LevelPacingWindow : EditorWindow
    {
        private const float RowHeight = 20f;
        private const float RangeColWidth = 80f;
        private const float RangedIntColWidth = 80f;
        private const float MaskColWidth = 100f;
        private const float WeightsColWidth = 140f;
        private const float HeaderHeight = 22f;
        private const float SeparatorWidth = 1f;

        private readonly ConfigAssetCache<LevelPacingConfiguration> _assetCache = new();
        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();

        private string[] _paletteNames;

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
            _paletteNames = palette != null
                ? palette.Colors.Select(c => c.Name).ToArray()
                : new[] { "0", "1", "2", "3", "4", "5", "6", "7" };
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
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHeader();

            for (var i = 0; i < count; i++)
            {
                DrawRow(i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Range", headerStyle, GUILayout.Width(RangeColWidth));
            DrawColumnSeparator();
            GUILayout.Label("Spawn", headerStyle, GUILayout.Width(RangedIntColWidth));
            DrawColumnSeparator();
            GUILayout.Label("Board", headerStyle, GUILayout.Width(RangedIntColWidth));
            DrawColumnSeparator();
            GUILayout.Label("Cadence", headerStyle, GUILayout.Width(RangedIntColWidth));
            DrawColumnSeparator();
            GUILayout.Label("1st Turn", headerStyle, GUILayout.Width(RangedIntColWidth));
            DrawColumnSeparator();
            GUILayout.Label("Colors", headerStyle, GUILayout.Width(MaskColWidth));
            DrawColumnSeparator();
            GUILayout.Label("Balloons", headerStyle, GUILayout.Width(WeightsColWidth));
            DrawColumnSeparator();
            GUILayout.Label("Items", headerStyle, GUILayout.Width(WeightsColWidth));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
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

            var bgColor = GUI.backgroundColor;
            if (isFallback)
            {
                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f, 0.3f);
            }
            else if (index % 2 == 1)
            {
                GUI.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(EditorGUIUtility.singleLineHeight + 4f));
            GUI.backgroundColor = bgColor;

            // Range column
            DrawRangeCell(fromProp, toProp, isFallback);
            DrawColumnSeparator();

            if (paramsProp != null)
            {
                DrawRangedIntCell(paramsProp, "_spawnLines");
                DrawColumnSeparator();
                DrawRangedIntCell(paramsProp, "_boardLines");
                DrawColumnSeparator();
                DrawRangedIntCell(paramsProp, "_itemCadence");
                DrawColumnSeparator();
                DrawRangedIntCell(paramsProp, "_firstSpawnTurn");
                DrawColumnSeparator();
                DrawMaskCell(paramsProp);
                DrawColumnSeparator();
                DrawWeightsCell(paramsProp, "_balloonWeights", "Balloon");
                DrawColumnSeparator();
                DrawWeightsCell(paramsProp, "_itemWeights", "Item");
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(_expandedRow == index ? "▼" : "►", GUILayout.Width(24f)))
            {
                _expandedRow = _expandedRow == index ? -1 : index;
            }

            EditorGUILayout.EndHorizontal();

            if (_expandedRow == index && paramsProp != null)
            {
                DrawExpandedDetails(paramsProp);
            }
        }

        private static void DrawRangeCell(SerializedProperty fromProp, SerializedProperty toProp, bool isFallback)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(RangeColWidth));
            if (isFallback)
            {
                EditorGUILayout.LabelField("Fallback", EditorStyles.miniLabel, GUILayout.Width(RangeColWidth));
            }
            else
            {
                EditorGUIUtility.labelWidth = 1f;
                fromProp.intValue = EditorGUILayout.IntField(" ", fromProp.intValue, GUILayout.Width(34f));
                EditorGUILayout.LabelField("–", GUILayout.Width(10f));
                toProp.intValue = EditorGUILayout.IntField(" ", toProp.intValue, GUILayout.Width(34f));
                EditorGUIUtility.labelWidth = 0f;
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawRangedIntCell(SerializedProperty paramsProp, string fieldName)
        {
            var prop = paramsProp.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                GUILayout.Space(RangedIntColWidth);
                return;
            }

            var minProp = prop.FindPropertyRelative("_min");
            var maxProp = prop.FindPropertyRelative("_max");

            EditorGUILayout.BeginHorizontal(GUILayout.Width(RangedIntColWidth));
            EditorGUIUtility.labelWidth = 1f;
            minProp.intValue = EditorGUILayout.IntField(" ", minProp.intValue, GUILayout.Width(34f));
            EditorGUILayout.LabelField("/", GUILayout.Width(8f));
            maxProp.intValue = EditorGUILayout.IntField(" ", maxProp.intValue, GUILayout.Width(34f));
            EditorGUIUtility.labelWidth = 0f;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaskCell(SerializedProperty paramsProp)
        {
            var maskProp = paramsProp.FindPropertyRelative("_allowedColorsMask");
            if (maskProp == null)
            {
                GUILayout.Space(MaskColWidth);
                return;
            }

            EditorGUIUtility.labelWidth = 1f;
            var newMask = EditorGUILayout.MaskField(" ", maskProp.intValue, _paletteNames,
                GUILayout.Width(MaskColWidth));
            if (newMask != maskProp.intValue)
            {
                maskProp.intValue = newMask;
            }

            EditorGUIUtility.labelWidth = 0f;
        }

        private static void DrawWeightsCell(SerializedProperty paramsProp, string fieldName, string kind)
        {
            var prop = paramsProp.FindPropertyRelative(fieldName);
            if (prop == null || !prop.isArray)
            {
                GUILayout.Space(WeightsColWidth);
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

            EditorGUILayout.LabelField($"{activeCount} {kind}(s)", EditorStyles.miniLabel,
                GUILayout.Width(WeightsColWidth));
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

        private static void DrawColumnSeparator()
        {
            var rect = GUILayoutUtility.GetRect(SeparatorWidth, RowHeight,
                GUILayout.Width(SeparatorWidth), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 0.8f));
        }
    }
}

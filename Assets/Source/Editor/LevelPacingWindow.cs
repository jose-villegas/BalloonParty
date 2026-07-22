using System.Linq;
using BalloonParty.Balloon.Type;
using BalloonParty.Cheats;
using BalloonParty.Game;
using BalloonParty.Game.Run;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor.Archetype;
using UnityEditor;
using UnityEngine;
using VContainer;

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
        private const int ItemColIndex = 9;
        private const int ActorColIndex = 10;
        private const float BalloonExpandedWidth = 310f;
        private const float ItemExpandedWidth = 170f;
        private const float ActorExpandedWidth = 260f;

        // Columns that have a visual gap before them (group separators)
        private static readonly int[] GapBeforeCols = { 1, 4, 6, 10 };

        private static readonly float[] ColWidths =
        {
            90f,   // 0: Range
            40f,   // 1: Spawn (single int)
            40f,   // 2: Board (single int)
            40f,   // 3: 1st Turn (single int)
            130f,  // 4: Colors (dropdown + swatches)
            100f,  // 5: Balloons (collapsed) — dynamic when expanded
            130f,  // 6: Cadence (min/max + mode)
            80f,   // 7: Initial Count curve
            80f,   // 8: Wave Count curve
            100f,  // 9: Items — dynamic when expanded
            100f,  // 10: Actors — dynamic when expanded
            34f,   // 11: Expand (foldout icon)
            34f,   // 12: Duplicate icon
            34f,   // 13: Delete (trash icon)
            34f,   // 14: Play from this level
        };

        private static readonly string[] ColHeaders =
        {
            "Range", "Spawn", "Board", "1st Turn", "Colors", "Balloons", "Cadence", "Init Count", "Wave Count", "Items", "Actors", "Expand", "Dupe", "Delete", "Play"
        };

        private readonly ConfigAssetCache<LevelPacingConfiguration> _assetCache = new();
        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();
        private readonly ConfigAssetCache<BalloonsConfiguration> _balloonsConfigCache = new();

        private string[] _paletteNames;
        private Color[] _paletteColors;
        private int[] _selectedBalloonPerRow = System.Array.Empty<int>();
        private int[] _selectedItemPerRow = System.Array.Empty<int>();
        private int[] _selectedActorPerRow = System.Array.Empty<int>();

        private LevelPacingConfiguration _asset;
        private SerializedObject _serialized;
        private SerializedProperty _rangesProp;
        private Vector2 _scroll;
        private int _expandedRow = -1;
        private int _focusedRow = -1;
        private bool _balloonsExpanded;
        private bool _itemsExpanded;
        private bool _actorsExpanded;
        private float _collapsedBalloonColWidth = ColWidths[BalloonColIndex];
        private float _collapsedItemColWidth = ColWidths[ItemColIndex];
        private float _collapsedActorColWidth = ColWidths[ActorColIndex];

        private float EffectiveBalloonColWidth => _balloonsExpanded ? BalloonExpandedWidth : _collapsedBalloonColWidth;
        private float EffectiveItemColWidth => _itemsExpanded ? ItemExpandedWidth : _collapsedItemColWidth;
        private float EffectiveActorColWidth => _actorsExpanded ? ActorExpandedWidth : _collapsedActorColWidth;

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

            LevelPacingCurvePanel.Draw(_asset, _serialized);
            EditorGUILayout.Space(4f);
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

            // Compute collapsed actor column width from the widest row
            if (!_actorsExpanded)
            {
                var maxActiveActors = 0;
                for (var i = 0; i < count; i++)
                {
                    var paramsProp = _rangesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_parameters");
                    var aProp = paramsProp?.FindPropertyRelative("_gridActorGates");
                    if (aProp == null || !aProp.isArray)
                    {
                        continue;
                    }

                    if (aProp.arraySize > maxActiveActors)
                    {
                        maxActiveActors = aProp.arraySize;
                    }
                }

                _collapsedActorColWidth = Mathf.Max(
                    ColWidths[ActorColIndex],
                    32f + maxActiveActors * (RowHeight - 4f + 2f));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Group title row
            var groupTitleRect = GUILayoutUtility.GetRect(TotalWidth(), RowHeight);
            var gapBg = new Color(0.15f, 0.15f, 0.15f, 1f);
            EditorGUI.DrawRect(groupTitleRect, gapBg);
            var titleBg = new Color(0.18f, 0.18f, 0.18f, 1f);
            DrawGroupBackground(groupTitleRect, 0, 0, titleBg);
            DrawGroupBackground(groupTitleRect, 1, 3, titleBg);
            DrawGroupBackground(groupTitleRect, 4, 5, titleBg);
            DrawGroupBackground(groupTitleRect, 6, 9, titleBg);
            DrawGroupBackground(groupTitleRect, 10, 10, titleBg);
            DrawGroupTitles(groupTitleRect);

            // Group border separators at both edges of each gap
            for (var i = 0; i < GapBeforeCols.Length; i++)
            {
                var col = GapBeforeCols[i];
                var leftX = groupTitleRect.x + ColX(col);
                var rightX = leftX - GroupGap;
                EditorGUI.DrawRect(new Rect(rightX, groupTitleRect.y, SeparatorWidth, groupTitleRect.height),
                    new Color(0.4f, 0.4f, 0.4f, 0.9f));
                EditorGUI.DrawRect(new Rect(leftX, groupTitleRect.y, SeparatorWidth, groupTitleRect.height),
                    new Color(0.4f, 0.4f, 0.4f, 0.9f));
            }

            // Right edge of Actors group
            var actorRightX = groupTitleRect.x + ColX(ActorColIndex) + EffectiveActorColWidth;
            EditorGUI.DrawRect(new Rect(actorRightX, groupTitleRect.y, SeparatorWidth, groupTitleRect.height),
                new Color(0.4f, 0.4f, 0.4f, 0.9f));

            var hSepColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);
            TableDrawHelper.DrawHorizontalSeparator(groupTitleRect, hSepColor);

            // Header
            var headerRect = GUILayoutUtility.GetRect(TotalWidth(), RowHeight);
            EditorGUI.DrawRect(headerRect, gapBg);
            var headerBg = new Color(0.18f, 0.18f, 0.18f, 1f);
            DrawGroupBackground(headerRect, 0, 0, headerBg);
            DrawGroupBackground(headerRect, 1, 3, headerBg);
            DrawGroupBackground(headerRect, 4, 5, headerBg);
            DrawGroupBackground(headerRect, 6, 9, headerBg);
            DrawGroupBackground(headerRect, 10, 10, headerBg);
            DrawHeaderCells(headerRect);
            TableDrawHelper.DrawHorizontalSeparator(headerRect, hSepColor);

            // Clear focused row when keyboard focus is lost entirely
            if (GUIUtility.keyboardControl == 0)
            {
                _focusedRow = -1;
            }

            // Rows
            for (var i = 0; i < _rangesProp.arraySize; i++)
            {
                DrawRow(i);
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("+ Add Range", GUILayout.Width(120f)))
            {
                _rangesProp.InsertArrayElementAtIndex(_rangesProp.arraySize);
            }

            EditorGUILayout.EndScrollView();
        }

        private float TotalWidth()
        {
            var total = 0f;
            for (var i = 0; i < ColWidths.Length - 3; i++)
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
            if (col >= 11)
            {
                return RightAnchoredCellRect(rowRect, col);
            }

            return new Rect(rowRect.x + ColX(col), rowRect.y, EffectiveColWidth(col), rowRect.height);
        }

        private Rect RightAnchoredCellRect(Rect rowRect, int col)
        {
            const float padding = 4f;
            var rightEdge = _scroll.x + position.width - padding;
            var x = rightEdge;

            for (var i = ColWidths.Length - 1; i >= col; i--)
            {
                x -= EffectiveColWidth(i);
                if (i < ColWidths.Length - 1)
                {
                    x -= SeparatorWidth;
                }
            }

            return new Rect(x, rowRect.y, EffectiveColWidth(col), rowRect.height);
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

            if (col == ActorColIndex)
            {
                return EffectiveActorColWidth;
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

        private void DrawGroupGaps(Rect rowRect, Color gapColor)
        {
            for (var i = 0; i < GapBeforeCols.Length; i++)
            {
                var col = GapBeforeCols[i];
                var gapX = rowRect.x + ColX(col) - GroupGap;
                var gapRect = new Rect(gapX, rowRect.y, GroupGap, rowRect.height);
                EditorGUI.DrawRect(gapRect, gapColor);
            }
        }

        private void DrawGroupTitles(Rect rowRect)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };

            // Group 1: Range (col 0)
            var g1Start = rowRect.x + ColX(0);
            var g1End = rowRect.x + ColX(0) + EffectiveColWidth(0);
            EditorGUI.LabelField(new Rect(g1Start, rowRect.y, g1End - g1Start, rowRect.height), "Range", style);

            // Group 2: Spawning (cols 1–3)
            var g2Start = rowRect.x + ColX(1);
            var g2End = rowRect.x + ColX(3) + EffectiveColWidth(3);
            EditorGUI.LabelField(new Rect(g2Start, rowRect.y, g2End - g2Start, rowRect.height), "Spawning", style);

            // Group 3: Balloons (cols 4–5) with focus dropdown
            var g3Start = rowRect.x + ColX(4);
            var g3End = rowRect.x + ColX(5) + EffectiveColWidth(5);
            var g3Rect = new Rect(g3Start, rowRect.y, g3End - g3Start, rowRect.height);
            DrawBalloonFocusGroup(g3Rect, style);

            // Group 4: Items (cols 6–9) with focus dropdown
            var g4Start = rowRect.x + ColX(6);
            var g4End = rowRect.x + ColX(9) + EffectiveColWidth(9);
            var g4Rect = new Rect(g4Start, rowRect.y, g4End - g4Start, rowRect.height);
            DrawItemFocusGroup(g4Rect, style);

            // Group 5: Actors (col 10) with focus dropdown
            var g5Start = rowRect.x + ColX(10);
            var g5End = rowRect.x + ColX(10) + EffectiveColWidth(10);
            var g5Rect = new Rect(g5Start, rowRect.y, g5End - g5Start, rowRect.height);
            DrawActorFocusGroup(g5Rect, style);
        }

        private void DrawBalloonFocusGroup(Rect groupRect, GUIStyle labelStyle)
        {
            if (!_balloonsExpanded)
            {
                EditorGUI.LabelField(groupRect, "Balloons", labelStyle);
                return;
            }

            var labelW = groupRect.width - 74f;
            EditorGUI.LabelField(new Rect(groupRect.x, groupRect.y, labelW, groupRect.height), "Balloons", labelStyle);

            var allTypes = (BalloonType[])System.Enum.GetValues(typeof(BalloonType));
            var names = new string[allTypes.Length + 1];
            names[0] = "Focus All…";
            for (var i = 0; i < allTypes.Length; i++)
            {
                names[i + 1] = allTypes[i].ToString();
            }

            var dropdownRect = new Rect(groupRect.xMax - 72f, groupRect.y + 2f, 70f, groupRect.height - 4f);
            var picked = EditorGUI.Popup(dropdownRect, 0, names);
            if (picked > 0)
            {
                FocusBalloonTypeInAllRows(allTypes[picked - 1], picked - 1);
            }
        }

        private void FocusBalloonTypeInAllRows(BalloonType type, int typeIndex)
        {
            if (_rangesProp == null)
            {
                return;
            }

            for (var row = 0; row < _rangesProp.arraySize; row++)
            {
                var entry = _rangesProp.GetArrayElementAtIndex(row);
                var paramsProp = entry.FindPropertyRelative("_parameters");
                if (paramsProp == null)
                {
                    continue;
                }

                var balloonsProp = paramsProp.FindPropertyRelative("_balloonWeights");
                if (balloonsProp == null || !balloonsProp.isArray)
                {
                    continue;
                }

                if (FindBalloonEntryIndex(balloonsProp, type) >= 0)
                {
                    EnsureSelectionArraySize(ref _selectedBalloonPerRow, row);
                    _selectedBalloonPerRow[row] = typeIndex;
                }
            }
        }

        private void DrawItemFocusGroup(Rect groupRect, GUIStyle labelStyle)
        {
            if (!_itemsExpanded)
            {
                EditorGUI.LabelField(groupRect, "Items", labelStyle);
                return;
            }

            var labelW = groupRect.width - 74f;
            EditorGUI.LabelField(new Rect(groupRect.x, groupRect.y, labelW, groupRect.height), "Items", labelStyle);

            var allTypes = (ItemType[])System.Enum.GetValues(typeof(ItemType));
            var names = new string[allTypes.Length + 1];
            names[0] = "Focus All…";
            for (var i = 0; i < allTypes.Length; i++)
            {
                names[i + 1] = allTypes[i].ToString();
            }

            var dropdownRect = new Rect(groupRect.xMax - 72f, groupRect.y + 2f, 70f, groupRect.height - 4f);
            var picked = EditorGUI.Popup(dropdownRect, 0, names);
            if (picked > 0)
            {
                FocusItemTypeInAllRows(allTypes[picked - 1], picked - 1);
            }
        }

        private void DrawActorFocusGroup(Rect groupRect, GUIStyle labelStyle)
        {
            if (!_actorsExpanded)
            {
                EditorGUI.LabelField(groupRect, "Actors", labelStyle);
                return;
            }

            var labelW = groupRect.width - 74f;
            EditorGUI.LabelField(new Rect(groupRect.x, groupRect.y, labelW, groupRect.height), "Actors", labelStyle);

            var allTypes = (GridActorType[])System.Enum.GetValues(typeof(GridActorType));
            var names = new string[allTypes.Length + 1];
            names[0] = "Focus All…";
            for (var i = 0; i < allTypes.Length; i++)
            {
                names[i + 1] = allTypes[i].ToString();
            }

            var dropdownRect = new Rect(groupRect.xMax - 72f, groupRect.y + 2f, 70f, groupRect.height - 4f);
            var picked = EditorGUI.Popup(dropdownRect, 0, names);
            if (picked > 0)
            {
                FocusActorTypeInAllRows(allTypes[picked - 1], picked - 1);
            }
        }

        private void FocusItemTypeInAllRows(ItemType type, int typeIndex)
        {
            if (_rangesProp == null)
            {
                return;
            }

            for (var row = 0; row < _rangesProp.arraySize; row++)
            {
                var entry = _rangesProp.GetArrayElementAtIndex(row);
                var paramsProp = entry.FindPropertyRelative("_parameters");
                if (paramsProp == null)
                {
                    continue;
                }

                var itemsProp = paramsProp.FindPropertyRelative("_itemWeights");
                if (itemsProp == null || !itemsProp.isArray)
                {
                    continue;
                }

                if (FindItemEntryIndex(itemsProp, type) >= 0)
                {
                    EnsureSelectionArraySize(ref _selectedItemPerRow, row);
                    _selectedItemPerRow[row] = typeIndex;
                }
            }
        }

        private void FocusActorTypeInAllRows(GridActorType type, int typeIndex)
        {
            if (_rangesProp == null)
            {
                return;
            }

            for (var row = 0; row < _rangesProp.arraySize; row++)
            {
                var entry = _rangesProp.GetArrayElementAtIndex(row);
                var paramsProp = entry.FindPropertyRelative("_parameters");
                if (paramsProp == null)
                {
                    continue;
                }

                var actorsProp = paramsProp.FindPropertyRelative("_gridActorGates");
                if (actorsProp == null || !actorsProp.isArray)
                {
                    continue;
                }

                if (FindActorEntryIndex(actorsProp, type) >= 0)
                {
                    EnsureSelectionArraySize(ref _selectedActorPerRow, row);
                    _selectedActorPerRow[row] = typeIndex;
                }
            }
        }

        private static void EnsureSelectionArraySize(ref int[] array, int index)
        {
            if (array.Length > index)
            {
                return;
            }

            var newArr = new int[index + 16];
            System.Array.Copy(array, newArr, array.Length);
            array = newArr;
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
                else if (i == ActorColIndex)
                {
                    var toggle = _actorsExpanded ? "▼" : "►";
                    var label = $"{toggle} Actors";
                    if (GUI.Button(cell, label, EditorStyles.boldLabel))
                    {
                        _actorsExpanded = !_actorsExpanded;
                    }

                    if (_actorsExpanded)
                    {
                        DrawActorSubHeaders(cell);
                    }
                }
                else
                {
                    EditorGUI.LabelField(cell, ColHeaders[i], style);
                }

                // Separator (skip right-anchored columns and last col before gap)
                if (i >= 11 || HasGapBefore(i + 1))
                {
                    continue;
                }

                var sep = new Rect(cell.xMax, rowRect.y, SeparatorWidth, rowRect.height);
                EditorGUI.DrawRect(sep, new Color(0.35f, 0.35f, 0.35f, 0.8f));
            }

            // Group border separators at both edges of each gap
            for (var i = 0; i < GapBeforeCols.Length; i++)
            {
                var col = GapBeforeCols[i];
                var leftX = rowRect.x + ColX(col);
                var rightX = leftX - GroupGap;
                EditorGUI.DrawRect(new Rect(rightX, rowRect.y, SeparatorWidth, rowRect.height),
                    new Color(0.4f, 0.4f, 0.4f, 0.9f));
                EditorGUI.DrawRect(new Rect(leftX, rowRect.y, SeparatorWidth, rowRect.height),
                    new Color(0.4f, 0.4f, 0.4f, 0.9f));
            }

            // Right edge of Actors group
            var actorRightX = rowRect.x + ColX(ActorColIndex) + EffectiveActorColWidth;
            EditorGUI.DrawRect(new Rect(actorRightX, rowRect.y, SeparatorWidth, rowRect.height),
                new Color(0.4f, 0.4f, 0.4f, 0.9f));
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

            // Track keyboard focus entering this row
            var controlBefore = GUIUtility.keyboardControl;

            // Determine row background color
            var gapColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            var selectedLevel = LevelPacingCurvePanel.SelectedLevel;
            var isActiveRow = !isFallback && selectedLevel >= from && selectedLevel <= to;
            var isFocusedRow = _focusedRow == index;

            Color rowBg;
            if (isFocusedRow)
            {
                rowBg = new Color(0.28f, 0.26f, 0.18f, 1f);
            }
            else if (isActiveRow)
            {
                rowBg = new Color(0.22f, 0.30f, 0.22f, 1f);
            }
            else if (isFallback)
            {
                rowBg = new Color(0.25f, 0.32f, 0.38f, 1f);
            }
            else if (index % 2 == 1)
            {
                rowBg = new Color(0.24f, 0.24f, 0.24f, 1f);
            }
            else
            {
                rowBg = new Color(0.19f, 0.19f, 0.19f, 1f);
            }

            // Paint full row background first (ensures cells + right area all match)
            EditorGUI.DrawRect(rowRect, rowBg);

            // Paint gaps between column groups darker (skip on highlighted rows for uniform tint)
            if (!isFocusedRow && !isActiveRow)
            {
                DrawGroupGaps(rowRect, gapColor);
            }

            // Left-edge accent for active or focused row
            if (isFocusedRow)
            {
                var accent = new Rect(rowRect.x, rowRect.y, 3f, rowRect.height);
                EditorGUI.DrawRect(accent, new Color(0.9f, 0.7f, 0.2f, 0.9f));
            }
            else if (isActiveRow)
            {
                var accent = new Rect(rowRect.x, rowRect.y, 3f, rowRect.height);
                EditorGUI.DrawRect(accent, new Color(0.3f, 0.8f, 0.3f, 0.9f));
            }

            // Separators (only for left-anchored columns 0–10)
            for (var i = 0; i < ColWidths.Length - 4; i++)
            {
                if (HasGapBefore(i + 1))
                {
                    continue;
                }

                var colW = EffectiveColWidth(i);
                var sep = new Rect(rowRect.x + ColX(i) + colW, rowRect.y, SeparatorWidth, rowRect.height);
                EditorGUI.DrawRect(sep, new Color(0.35f, 0.35f, 0.35f, 0.5f));
            }

            // Group border separators at both edges of each gap
            for (var i = 0; i < GapBeforeCols.Length; i++)
            {
                var col = GapBeforeCols[i];
                var leftX = rowRect.x + ColX(col);
                var rightX = leftX - GroupGap;
                // Right edge of previous group
                EditorGUI.DrawRect(new Rect(rightX, rowRect.y, SeparatorWidth, rowRect.height),
                    new Color(0.4f, 0.4f, 0.4f, 0.9f));
                // Left edge of next group
                EditorGUI.DrawRect(new Rect(leftX, rowRect.y, SeparatorWidth, rowRect.height),
                    new Color(0.4f, 0.4f, 0.4f, 0.9f));
            }

            // Right edge of Actors group
            var actorRightX = rowRect.x + ColX(ActorColIndex) + EffectiveActorColWidth;
            EditorGUI.DrawRect(new Rect(actorRightX, rowRect.y, SeparatorWidth, rowRect.height),
                new Color(0.4f, 0.4f, 0.4f, 0.9f));

            // Horizontal row separator at the bottom
            TableDrawHelper.DrawHorizontalSeparator(rowRect);

            // Range (col 0)
            DrawRangeCell(CellRect(rowRect, 0), fromProp, toProp, isFallback);

            if (paramsProp != null)
            {
                PropertyCellDrawer.IntCell(CellRect(rowRect, 1), paramsProp, "_spawnLines");
                PropertyCellDrawer.IntCell(CellRect(rowRect, 2), paramsProp, "_boardLines");
                PropertyCellDrawer.IntCell(CellRect(rowRect, 3), paramsProp, "_firstSpawnTurn");
                DrawMaskCell(CellRect(rowRect, 4), paramsProp);

                if (_balloonsExpanded)
                {
                    DrawBalloonCellExpanded(CellRect(rowRect, BalloonColIndex), balloonsProp, index);
                }
                else
                {
                    DrawBalloonCellCollapsed(CellRect(rowRect, BalloonColIndex), balloonsProp);
                }

                DrawRangedIntCell(CellRect(rowRect, 6), paramsProp, "_itemCadence");
                PropertyCellDrawer.CurveCell(CellRect(rowRect, 7), paramsProp, "_initialItemCountWeights");
                PropertyCellDrawer.CurveCell(CellRect(rowRect, 8), paramsProp, "_itemCountWeights");

                var itemsProp = paramsProp.FindPropertyRelative("_itemWeights");
                if (_itemsExpanded)
                {
                    DrawItemCellExpanded(CellRect(rowRect, ItemColIndex), itemsProp, index);
                }
                else
                {
                    DrawItemCellCollapsed(CellRect(rowRect, ItemColIndex), itemsProp);
                }

                var actorsProp = paramsProp.FindPropertyRelative("_gridActorGates");
                if (_actorsExpanded)
                {
                    DrawActorCellExpanded(CellRect(rowRect, ActorColIndex), actorsProp, index);
                }
                else
                {
                    DrawActorCellCollapsed(CellRect(rowRect, ActorColIndex), actorsProp);
                }
            }

            // Action icons (cols 11-14) — right-anchored.
            var expanded = _expandedRow == index;
            var expandRect = CellRect(rowRect, 11);
            if (GUI.Button(expandRect, IconButton(
                    expanded ? "d_winbtn_win_restore" : "d_winbtn_win_max",
                    expanded ? "▼" : "►", expanded ? "Collapse details" : "Expand details")))
            {
                _expandedRow = expanded ? -1 : index;
            }

            var dupeRect = CellRect(rowRect, 12);
            if (GUI.Button(dupeRect, IconButton("TreeEditor.Duplicate", "⧉", "Duplicate range")))
            {
                _rangesProp.InsertArrayElementAtIndex(index);
                return;
            }

            var delRect = CellRect(rowRect, 13);
            if (GUI.Button(delRect, IconButton("TreeEditor.Trash", "−", "Delete range")))
            {
                _rangesProp.DeleteArrayElementAtIndex(index);
                if (_expandedRow == index)
                {
                    _expandedRow = -1;
                }

                return;
            }

            // Start a run beginning at this range's first level (dev only — the cheat menu has the
            // in-play equivalent). Fallback rows pass their negative FromLevel as the ID so the
            // difficulty resolver picks the correct named fallback.
            var playRect = CellRect(rowRect, 14);
            var playLevel = isFallback ? from : Mathf.Max(1, from);
            var playTip = isFallback ? $"Play fallback ID {from}" : "Play from this level";
            if (GUI.Button(playRect, IconButton("PlayButton", "▶", playTip)))
            {
                StartFromLevel(playLevel);
            }

            if (_expandedRow == index && paramsProp != null)
            {
                DrawExpandedDetails(paramsProp);
            }

            // Detect if keyboard focus entered this row's controls
            if (GUIUtility.keyboardControl != 0 && GUIUtility.keyboardControl != controlBefore)
            {
                _focusedRow = index;
            }
        }

        // A built-in editor icon for a button, falling back to a text glyph if the icon name is absent
        // in this Unity version (so the button always renders something actionable).
        private static GUIContent IconButton(string iconName, string glyphFallback, string tooltip)
        {
            var texture = EditorGUIUtility.FindTexture(iconName);
            if (texture != null)
            {
                return new GUIContent(texture, tooltip);
            }

            return new GUIContent(glyphFallback, tooltip);
        }

        // Sets the dev start-level override and begins a run at that level. In play mode it restarts the
        // live run directly; from edit mode it stashes the level for CheatState to consume on play start.
        private static void StartFromLevel(int level)
        {
            if (Application.isPlaying)
            {
                CheatState.StartLevel = level;
                var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
                scope?.Container.Resolve<RunController>().RestartRun();
                return;
            }

            EditorPrefs.SetInt(CheatState.StartLevelPrefKey, level);
            EditorApplication.isPlaying = true;
        }

        private static void DrawRangeCell(Rect cell, SerializedProperty fromProp, SerializedProperty toProp, bool isFallback)
        {
            if (isFallback)
            {
                var labelRect = new Rect(cell.x, cell.y, 18f, cell.height);
                EditorGUI.LabelField(labelRect, "FB", EditorStyles.miniLabel);
                var idRect = new Rect(cell.x + 18f, cell.y + 2f, cell.width - 20f, cell.height - 4f);
                fromProp.intValue = EditorGUI.IntField(idRect, fromProp.intValue);
                return;
            }

            PropertyCellDrawer.IntRangeCell(cell, fromProp, toProp);
        }

        private static void DrawRangedIntCell(Rect cell, SerializedProperty paramsProp, string fieldName)
        {
            PropertyCellDrawer.RangedIntCell(cell, paramsProp, fieldName);
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

        private static void DrawActorSubHeaders(Rect cell)
        {
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            var x = cell.x + 74f;
            var y = cell.y;
            var h = cell.height;

            EditorGUI.LabelField(new Rect(x, y, 40f, h), "Count", subStyle);
            x += 44f;
            EditorGUI.LabelField(new Rect(x, y, 50f, h), "Mode", subStyle);
            x += 54f;
            EditorGUI.LabelField(new Rect(x, y, 46f, h), "Cluster", subStyle);
        }

        private static void DrawActorCellCollapsed(Rect cell, SerializedProperty actorsProp)
        {
            if (actorsProp == null || !actorsProp.isArray)
            {
                return;
            }

            var count = actorsProp.arraySize;
            var labelRect = new Rect(cell.x + 2f, cell.y, 30f, cell.height);
            EditorGUI.LabelField(labelRect, count.ToString(), EditorStyles.miniLabel);

            var x = labelRect.xMax;
            var thumbSize = cell.height - 4f;

            for (var i = 0; i < count; i++)
            {
                var entryProp = actorsProp.GetArrayElementAtIndex(i);
                var typeProp = entryProp.FindPropertyRelative("_type");
                if (typeProp == null)
                {
                    continue;
                }

                var actorType = (GridActorType)typeProp.intValue;
                var thumbRect = new Rect(x, cell.y + (cell.height - thumbSize) / 2f, thumbSize, thumbSize);
                EditorGUI.DrawRect(thumbRect, new Color(0.4f, 0.4f, 0.4f, 0.6f));
                EditorGUI.LabelField(thumbRect, actorType.ToString()[0].ToString(),
                    new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
                x += thumbSize + 2f;
            }
        }

        private void DrawActorCellExpanded(Rect cell, SerializedProperty actorsProp, int rowIndex)
        {
            if (actorsProp == null || !actorsProp.isArray)
            {
                return;
            }

            EnsureSelectionArraySize(ref _selectedActorPerRow, rowIndex);

            var count = actorsProp.arraySize;
            var allTypes = (GridActorType[])System.Enum.GetValues(typeof(GridActorType));
            var typeNames = new string[allTypes.Length];
            var selectedTypeIndex = _selectedActorPerRow[rowIndex];
            selectedTypeIndex = Mathf.Clamp(selectedTypeIndex, 0, allTypes.Length - 1);

            for (var i = 0; i < allTypes.Length; i++)
            {
                if (i == selectedTypeIndex)
                {
                    typeNames[i] = allTypes[i].ToString();
                }
                else
                {
                    var present = FindActorEntryIndex(actorsProp, allTypes[i]) >= 0;
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
                _selectedActorPerRow[rowIndex] = newTypeIndex;
                selectedTypeIndex = newTypeIndex;

                var selectedType = allTypes[selectedTypeIndex];
                if (FindActorEntryIndex(actorsProp, selectedType) < 0)
                {
                    actorsProp.InsertArrayElementAtIndex(count);
                    var newEntry = actorsProp.GetArrayElementAtIndex(count);
                    newEntry.FindPropertyRelative("_type").intValue = (int)selectedType;
                    var countProp = newEntry.FindPropertyRelative("_count");
                    countProp.FindPropertyRelative("_min").intValue = 1;
                    countProp.FindPropertyRelative("_max").intValue = 3;
                    newEntry.FindPropertyRelative("_maxPerCluster").intValue = 0;
                    count++;
                }
            }

            x += dropdownW + 4f;

            var selectedType2 = allTypes[selectedTypeIndex];
            var entryIndex = FindActorEntryIndex(actorsProp, selectedType2);

            if (entryIndex >= 0)
            {
                var entryProp = actorsProp.GetArrayElementAtIndex(entryIndex);
                var countPropEntry = entryProp.FindPropertyRelative("_count");
                var minProp = countPropEntry.FindPropertyRelative("_min");
                var maxProp = countPropEntry.FindPropertyRelative("_max");
                var clusterProp = entryProp.FindPropertyRelative("_maxPerCluster");

                // Count: min/max + mode
                var fieldW = 18f;
                var modeW = 50f;
                minProp.intValue = EditorGUI.IntField(new Rect(x, y, fieldW, h), minProp.intValue);
                x += fieldW;
                EditorGUI.LabelField(new Rect(x, y, 8f, h), "/");
                x += 8f;
                maxProp.intValue = EditorGUI.IntField(new Rect(x, y, fieldW, h), maxProp.intValue);
                x += fieldW + 2f;
                var modeProp = countPropEntry.FindPropertyRelative("_mode");
                modeProp.enumValueIndex = EditorGUI.Popup(new Rect(x, y, modeW, h), modeProp.enumValueIndex, modeProp.enumDisplayNames);
                x += modeW + 4f;

                // MaxPerCluster
                clusterProp.intValue = EditorGUI.IntField(new Rect(x, y, 24f, h), clusterProp.intValue);
                x += 28f;

                // Remove button
                if (GUI.Button(new Rect(x, y, 18f, h), "−", EditorStyles.miniButton))
                {
                    actorsProp.DeleteArrayElementAtIndex(entryIndex);
                }
            }
            else
            {
                EditorGUI.LabelField(new Rect(x, y, 120f, h), "Select a type", EditorStyles.miniLabel);
            }
        }

        private static int FindActorEntryIndex(SerializedProperty actorsProp, GridActorType type)
        {
            for (var i = 0; i < actorsProp.arraySize; i++)
            {
                var typeProp = actorsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_type");
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

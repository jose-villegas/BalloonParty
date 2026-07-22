using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.EditorUI.Layout;
using BalloonParty.EditorUI.Tables;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    internal sealed class TextureAuditWindow : EditorWindow
    {
        private static readonly string[] MobilePlatforms = { "iPhone", "Android" };

        private static readonly int[] SizeOptions = { 64, 128, 256, 512, 1024, 2048 };

        private static readonly string[] SizeLabels =
            SizeOptions.Select(s => s.ToString()).ToArray();

        private enum FilterMode
        {
            All,
            NoOverride,
            WithOverride
        }

        private static readonly string[] FilterLabels = { "All", "No Override", "With Override" };

        private static readonly Color WarningRowColor = new(1f, 0.85f, 0.7f);

        [Serializable]
        private sealed class Entry : ISelectable
        {
            public string Path;
            public string Name;
            public int SourceWidth;
            public int SourceHeight;
            public int DefaultMax;
            public int IPhoneMax;
            public int AndroidMax;
            public bool IPhoneOverridden;
            public bool AndroidOverridden;
            public bool Selected { get; set; }

            public bool HasOverride => IPhoneOverridden || AndroidOverridden;
        }

        private readonly List<Entry> _entries = new();
        private readonly SortState _sort = new();
        private Vector2 _scroll;
        private FilterMode _filter;
        private int _selectedSizeIndex = 2;
        private string _searchText = "";
        private bool _selectAll;
        private string[] _populatedGuids = Array.Empty<string>();

        [MenuItem("Assets/Texture/Texture Audit Window", false, 2200)]
        private static void OpenFromContextMenu()
        {
            var window = GetWindow<TextureAuditWindow>("Texture Audit");
            window.Populate(Selection.assetGUIDs);
            window.Show();
        }

        [MenuItem("Assets/Texture/Texture Audit Window", true)]
        private static bool ValidateOpenFromContextMenu()
        {
            return Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
        }

        private void Populate(string[] guids)
        {
            _populatedGuids = guids;
            _entries.Clear();

            var paths = new HashSet<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var tGuid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                    {
                        paths.Add(AssetDatabase.GUIDToAssetPath(tGuid));
                    }
                }
                else if (AssetImporter.GetAtPath(path) is TextureImporter)
                {
                    paths.Add(path);
                }
            }

            foreach (var path in paths.OrderBy(p => p))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                var iPhoneSettings = importer.GetPlatformTextureSettings("iPhone");
                var androidSettings = importer.GetPlatformTextureSettings("Android");
                var defaultSettings = importer.GetPlatformTextureSettings("DefaultTexturePlatform");

                _entries.Add(new Entry
                {
                    Path = path,
                    Name = System.IO.Path.GetFileNameWithoutExtension(path),
                    SourceWidth = texture != null ? texture.width : 0,
                    SourceHeight = texture != null ? texture.height : 0,
                    DefaultMax = defaultSettings.maxTextureSize,
                    IPhoneMax = iPhoneSettings.overridden
                        ? iPhoneSettings.maxTextureSize
                        : defaultSettings.maxTextureSize,
                    AndroidMax = androidSettings.overridden
                        ? androidSettings.maxTextureSize
                        : defaultSettings.maxTextureSize,
                    IPhoneOverridden = iPhoneSettings.overridden,
                    AndroidOverridden = androidSettings.overridden
                });
            }
        }

        private string[] CurrentGuids()
        {
            return _populatedGuids;
        }

        private void OnGUI()
        {
            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No textures loaded. Right-click a folder or textures in the Project view " +
                    "and choose Texture > Texture Audit Window.",
                    MessageType.Info);

                if (GUILayout.Button("Refresh from Selection"))
                {
                    Populate(Selection.assetGUIDs);
                }

                return;
            }

            _filter = SearchFilterToolbar.Draw(
                ref _searchText,
                _filter,
                FilterLabels,
                () => Populate(Selection.assetGUIDs));

            DrawTable();
            DrawBottomBar();
        }

        private List<Entry> GetFilteredEntries()
        {
            var result = new List<Entry>();

            foreach (var e in _entries)
            {
                if (_filter == FilterMode.NoOverride && e.HasOverride)
                {
                    continue;
                }

                if (_filter == FilterMode.WithOverride && !e.HasOverride)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_searchText) &&
                    e.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                result.Add(e);
            }

            return result;
        }

        private void DrawTable()
        {
            var filtered = GetFilteredEntries();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _selectAll = SelectionTracker.DrawSelectAllToggle(_selectAll, filtered);
            SortableHeader.Draw("Name", 0, 160, _sort);
            SortableHeader.Draw("Source", 1, 70, _sort);
            SortableHeader.Draw("Default", 2, 55, _sort);
            SortableHeader.Draw("iPhone", 3, 55, _sort);
            SortableHeader.Draw("Android", 4, 55, _sort);
            SortableHeader.Draw("Override", 5, 55, _sort);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            SortableHeader.ApplySort(filtered, _sort, CompareEntries);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var entry in filtered)
            {
                StyledRow.BeginHighlightedRow(!entry.HasOverride, WarningRowColor);
                SelectionTracker.DrawRowToggle(entry);
                AssetLinkLabel.Draw(entry.Name, entry.Path, 160);
                EditorGUILayout.LabelField($"{entry.SourceWidth}×{entry.SourceHeight}",
                    GUILayout.Width(70));
                EditorGUILayout.LabelField(entry.DefaultMax.ToString(), GUILayout.Width(55));
                StyledRow.DrawStyledLabel(
                    entry.IPhoneMax.ToString(),
                    entry.IPhoneOverridden,
                    55);
                StyledRow.DrawStyledLabel(
                    entry.AndroidMax.ToString(),
                    entry.AndroidOverridden,
                    55);
                EditorGUILayout.LabelField(entry.HasOverride ? "✅" : "❌", GUILayout.Width(55));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBottomBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            SelectionTracker.DrawSelectionCount(_entries);
            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Mobile Max:", GUILayout.Width(72));
            _selectedSizeIndex = EditorGUILayout.Popup(_selectedSizeIndex,
                SizeLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(60));

            if (GUILayout.Button("Apply", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ApplyToSelected(SizeOptions[_selectedSizeIndex]);
            }

            GUILayout.Space(8);

            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ResetSelected();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyToSelected(int mobileMaxSize)
        {
            var selected = SelectionTracker.GetSelected(_entries);
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var entry in selected)
            {
                var importer = AssetImporter.GetAtPath(entry.Path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                foreach (var platform in MobilePlatforms)
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.maxTextureSize = mobileMaxSize;
                    importer.SetPlatformTextureSettings(settings);
                }

                importer.SaveAndReimport();
            }

            Debug.Log(
                $"[TextureAudit] Set mobile max size to {mobileMaxSize} on {selected.Count} texture(s).");
            Populate(CurrentGuids());
        }

        private void ResetSelected()
        {
            var selected = SelectionTracker.GetSelected(_entries);
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var entry in selected)
            {
                var importer = AssetImporter.GetAtPath(entry.Path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                foreach (var platform in MobilePlatforms)
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    if (settings.overridden)
                    {
                        settings.overridden = false;
                        importer.SetPlatformTextureSettings(settings);
                    }
                }

                importer.SaveAndReimport();
            }

            Debug.Log($"[TextureAudit] Reset mobile overrides on {selected.Count} texture(s).");
            Populate(CurrentGuids());
        }

        private static int CompareEntries(int column, Entry a, Entry b)
        {
            return column switch
            {
                0 => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
                1 => (a.SourceWidth * a.SourceHeight).CompareTo(b.SourceWidth * b.SourceHeight),
                2 => a.DefaultMax.CompareTo(b.DefaultMax),
                3 => a.IPhoneMax.CompareTo(b.IPhoneMax),
                4 => a.AndroidMax.CompareTo(b.AndroidMax),
                5 => a.HasOverride.CompareTo(b.HasOverride),
                _ => 0
            };
        }
    }
}

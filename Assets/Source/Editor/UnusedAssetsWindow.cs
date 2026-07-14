using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Lists assets that no root can reach — candidates for deletion, not verdicts. Roots:
    ///     every scene under Assets/, everything in any Resources/ folder (runtime-loaded by
    ///     name), Assets/Settings/ (referenced from ProjectSettings, which dependency walking
    ///     can't traverse), and the player's preloaded assets. Reachability via
    ///     <see cref="AssetDatabase.GetDependencies(string[], bool)" />. Scripts, docs, editor
    ///     tooling and plugins are excluded from the scan — their usage isn't asset-reference
    ///     shaped, so they'd only produce noise.
    /// </summary>
    internal sealed class UnusedAssetsWindow : EditorWindow
    {
        private const string ScanPrefix = "Assets/";

        // Folders whose contents are used by mechanisms the dependency walk can't see (or that
        // aren't gameplay assets at all) — never reported.
        private static readonly string[] ExcludedFragments =
        {
            "/Editor/", "/Plugins/", "/Resources/", "/Settings/", "/Plans/", "/Diagrams/",
            "/TextMesh Pro/",
        };

        // Extensions whose usage is code-shaped (or documentation), not asset-reference-shaped.
        private static readonly string[] ExcludedExtensions =
        {
            ".cs", ".asmdef", ".asmref", ".md", ".unity", ".dll", ".json",
        };

        private readonly List<(string Path, long Bytes)> _unused = new();

        private Vector2 _scroll;
        private bool _scanned;
        private long _totalBytes;

        [MenuItem("Tools/BalloonParty/Unused Assets")]
        private static void Open()
        {
            GetWindow<UnusedAssetsWindow>("Unused Assets");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Candidates, not verdicts: anything loaded from code by name/Find (e.g. editor-only " +
                "Shader.Find fallbacks) or referenced only from ProjectSettings can appear here. " +
                "Review before deleting.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan", GUILayout.Width(80)))
            {
                Scan();
            }

            if (_scanned)
            {
                GUILayout.Label($"{_unused.Count} candidates — {FormatBytes(_totalBytes)}");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy Paths", GUILayout.Width(90)))
                {
                    CopyPaths();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!_scanned)
            {
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var (path, bytes) in _unused)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(path, EditorStyles.linkLabel))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(FormatBytes(bytes), GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            var all = AssetDatabase.GetAllAssetPaths();
            var used = new HashSet<string>(AssetDatabase.GetDependencies(CollectRoots(all), true));

            _unused.Clear();
            _totalBytes = 0;

            foreach (var path in all)
            {
                if (!path.StartsWith(ScanPrefix) || used.Contains(path)
                    || AssetDatabase.IsValidFolder(path) || IsExcluded(path))
                {
                    continue;
                }

                var bytes = new FileInfo(path).Length;
                _unused.Add((path, bytes));
                _totalBytes += bytes;
            }

            // Biggest savings first.
            _unused.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
            _scanned = true;
        }

        private static string[] CollectRoots(IReadOnlyList<string> allPaths)
        {
            var roots = new List<string>();

            foreach (var path in allPaths)
            {
                if (!path.StartsWith(ScanPrefix) || AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                if (path.EndsWith(".unity") || path.Contains("/Resources/")
                    || path.StartsWith("Assets/Settings/"))
                {
                    roots.Add(path);
                }
            }

            foreach (var preloaded in PlayerSettings.GetPreloadedAssets())
            {
                if (preloaded != null)
                {
                    roots.Add(AssetDatabase.GetAssetPath(preloaded));
                }
            }

            return roots.ToArray();
        }

        private static bool IsExcluded(string path)
        {
            foreach (var fragment in ExcludedFragments)
            {
                if (path.Contains(fragment))
                {
                    return true;
                }
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            foreach (var excluded in ExcludedExtensions)
            {
                if (extension == excluded)
                {
                    return true;
                }
            }

            return false;
        }

        private void CopyPaths()
        {
            var builder = new StringBuilder();
            foreach (var (path, _) in _unused)
            {
                builder.AppendLine(path);
            }

            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / (1024f * 1024f):0.0} MB";
            }

            return bytes >= 1024 ? $"{bytes / 1024f:0.0} KB" : $"{bytes} B";
        }
    }
}

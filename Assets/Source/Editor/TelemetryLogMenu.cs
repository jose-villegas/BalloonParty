using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Shortcuts to the JSON Lines telemetry logs `JsonLinesTelemetrySink` writes under
    ///     <see cref="Application.persistentDataPath" />. The path differs per platform and per
    ///     company/product name, so hunting for it by hand is a small tax paid repeatedly.
    /// </summary>
    internal static class TelemetryLogMenu
    {
        private const string Directory = "/telemetry/";
        private const string FileSearchPattern = "telemetry_*.jsonl";

        [MenuItem("Tools/BalloonParty/Telemetry/Open Log Folder")]
        private static void OpenFolder()
        {
            var path = Application.persistentDataPath + Directory;
            if (!System.IO.Directory.Exists(path))
            {
                Debug.LogWarning($"[Telemetry] No log folder yet at {path} — enter play mode once " +
                    "(the sink creates it at startup).");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        // Opens the newest file rather than the folder: during a tuning pass the interesting one is
        // always the session that just ended.
        [MenuItem("Tools/BalloonParty/Telemetry/Open Latest Log")]
        private static void OpenLatest()
        {
            if (!TryGetLatest(out var file))
            {
                return;
            }

            if (new FileInfo(file).Length == 0)
            {
                Debug.LogWarning($"[Telemetry] The latest log is empty: {file}. A record is written " +
                    "at a flight, a level flush or game over — a session that never fired writes nothing.");
            }

            EditorUtility.OpenWithDefaultApp(file);
        }

        [MenuItem("Tools/BalloonParty/Telemetry/Log Path To Console")]
        private static void LogPath()
        {
            Debug.Log($"[Telemetry] {Application.persistentDataPath + Directory}");
        }

        private static bool TryGetLatest(out string file)
        {
            file = null;
            var path = Application.persistentDataPath + Directory;
            if (!System.IO.Directory.Exists(path))
            {
                Debug.LogWarning($"[Telemetry] No log folder yet at {path} — enter play mode once.");
                return false;
            }

            // Ordinal sort is chronological: the sink embeds yyyyMMdd_HHmmss in the file name.
            var files = System.IO.Directory.GetFiles(path, FileSearchPattern);
            if (files.Length == 0)
            {
                Debug.LogWarning($"[Telemetry] No {FileSearchPattern} files in {path}.");
                return false;
            }

            Array.Sort(files, StringComparer.Ordinal);
            file = files[files.Length - 1];
            return true;
        }
    }
}

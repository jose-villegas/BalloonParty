using System.Reflection;
using BalloonParty.Configuration.Level;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Displays scoring curve output for validation. The one-time migration from the legacy
    /// override+formula system is complete — this tool now shows per-level curve values for tuning.</summary>
    internal static class LevelPacingMigrationTool
    {
        private const int MaxLevel = 50;

        private static readonly EditorAssetCache<LevelPacingConfiguration> AssetCache = new();

        [MenuItem("Tools/BalloonParty/Show Scoring Curve Values")]
        private static void ShowCurveValues()
        {
            var asset = AssetCache.Value;
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Scoring Curve", "No LevelPacingConfiguration asset found.", "OK");
                return;
            }

            var log = new System.Text.StringBuilder();
            log.AppendLine("Level | Colors | Per-Color | Total (per-color × colors) | Cumulative");
            log.AppendLine("------|--------|-----------|---------------------------|----------");

            var cumulative = 0;
            for (var level = 1; level <= MaxLevel; level++)
            {
                var threshold = asset.ThresholdForLevel(level);
                var colors = asset.ColorsForLevel(level);
                var totalPerLevel = threshold * colors;
                cumulative += totalPerLevel;

                log.AppendLine($"  {level,3} | {colors,6} | {threshold,9} | {totalPerLevel,25} | {cumulative,10}");
            }

            Debug.Log($"[LevelPacingScoringCurve] Values for levels 1–{MaxLevel}:\n{log}");
        }
    }
}

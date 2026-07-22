using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration.Level;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Migrates existing <see cref="LevelThresholdOverride"/> data and formula output into
    /// a <see cref="LevelScoringCurve"/> on the same asset. Also provides a comparison view.</summary>
    internal static class LevelPacingMigrationTool
    {
        private const int CompareMaxLevel = 50;

        private static readonly ConfigAssetCache<LevelPacingConfiguration> AssetCache = new();

        [MenuItem("Tools/BalloonParty/Migrate Level Pacing → Scoring Curve")]
        private static void Migrate()
        {
            var asset = AssetCache.Value;
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Migration", "No LevelPacingConfiguration asset found.", "OK");
                return;
            }

            var serialized = new SerializedObject(asset);
            var curveProp = serialized.FindProperty("_scoringCurve");
            var pointsProp = curveProp.FindPropertyRelative("_controlPoints");

            if (pointsProp.arraySize > 0)
            {
                if (!EditorUtility.DisplayDialog(
                    "Migration",
                    "The scoring curve already has control points. Overwrite with migrated data?",
                    "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            // Sample the legacy system to build cumulative milestones.
            var controlPoints = BuildControlPointsFromLegacy(asset);

            if (controlPoints.Count == 0)
            {
                EditorUtility.DisplayDialog("Migration", "Legacy system produced no usable data.", "OK");
                return;
            }

            // Write to the serialized asset.
            pointsProp.arraySize = controlPoints.Count;
            for (var i = 0; i < controlPoints.Count; i++)
            {
                var element = pointsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_level").intValue = controlPoints[i].Level;
                element.FindPropertyRelative("_cumulativeScore").floatValue = controlPoints[i].CumulativeScore;
            }

            // Set tail growth to geometric at 1.05 (gentle escalation beyond last CP).
            var tailProp = curveProp.FindPropertyRelative("_tailGrowth");
            tailProp.FindPropertyRelative("_mode").enumValueIndex = (int)TailGrowthMode.Geometric;
            tailProp.FindPropertyRelative("_rate").floatValue = 1.05f;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LevelPacingMigration] Migrated {controlPoints.Count} control points to scoring curve.");
            EditorUtility.DisplayDialog(
                "Migration Complete",
                $"Created {controlPoints.Count} control points.\n" +
                "Use Tools → BalloonParty → Compare Pacing Curves to validate.",
                "OK");
        }

        [MenuItem("Tools/BalloonParty/Compare Pacing Curves (Old vs New)")]
        private static void CompareCurves()
        {
            var asset = AssetCache.Value;
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Compare", "No LevelPacingConfiguration asset found.", "OK");
                return;
            }

            var log = new System.Text.StringBuilder();
            log.AppendLine("Level | Old (legacy) | New (curve) | Delta");
            log.AppendLine("------|-------------|-------------|------");

            var mismatchCount = 0;

            for (var level = 1; level <= CompareMaxLevel; level++)
            {
                var oldValue = GetLegacyThreshold(asset, level);
                var newValue = GetCurveThreshold(asset, level);
                var delta = newValue - oldValue;
                var flag = Mathf.Abs(delta) > 50 ? " ⚠" : "";

                log.AppendLine($"  {level,3} | {oldValue,11} | {newValue,11} | {delta,+5}{flag}");

                if (Mathf.Abs(delta) > 50)
                {
                    mismatchCount++;
                }
            }

            log.AppendLine();
            log.AppendLine(mismatchCount > 0
                ? $"⚠ {mismatchCount} levels differ by more than one rounding step."
                : "✅ All levels within rounding tolerance.");

            Debug.Log($"[LevelPacingMigration] Curve comparison:\n{log}");
        }

        private static List<ScoringControlPoint> BuildControlPointsFromLegacy(LevelPacingConfiguration asset)
        {
            var points = new List<ScoringControlPoint>();
            var cumulative = 0f;

            // Sample legacy thresholds and build cumulative milestones.
            // Use all override boundaries as control points, plus formula samples.
            var overrides = GetOverrides(asset);
            var sampledLevels = new HashSet<int>();

            // Always include level 1.
            sampledLevels.Add(1);

            // Include all override boundary levels.
            foreach (var entry in overrides)
            {
                sampledLevels.Add(entry.FromLevel);
                sampledLevels.Add(entry.ToLevel);
            }

            // Sample the formula tail at regular intervals.
            var lastOverrideLevel = 0;
            foreach (var entry in overrides)
            {
                if (entry.ToLevel > lastOverrideLevel)
                {
                    lastOverrideLevel = entry.ToLevel;
                }
            }

            for (var level = lastOverrideLevel + 1; level <= lastOverrideLevel + 20; level += 5)
            {
                sampledLevels.Add(level);
            }

            // Sort and compute cumulative at each sampled level.
            var sortedLevels = new List<int>(sampledLevels);
            sortedLevels.Sort();

            var prevLevel = 0;
            foreach (var level in sortedLevels)
            {
                // Fill in the cumulative for levels between samples using the legacy threshold.
                for (var l = prevLevel + 1; l <= level; l++)
                {
                    cumulative += GetLegacyThreshold(asset, l) * GetColorsForLevel(asset, l);
                }

                points.Add(new ScoringControlPoint(level, cumulative));
                prevLevel = level;
            }

            return points;
        }

        private static int GetLegacyThreshold(LevelPacingConfiguration asset, int level)
        {
            // Call the legacy path via reflection to bypass the curve check.
            var method = typeof(LevelPacingConfiguration).GetMethod(
                "ThresholdFromLegacy",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (int)method!.Invoke(asset, new object[] { level });
        }

        private static int GetCurveThreshold(LevelPacingConfiguration asset, int level)
        {
            // Use the public ThresholdForLevel — if curve is empty, this returns legacy too.
            return asset.ThresholdForLevel(level);
        }

        private static int GetColorsForLevel(LevelPacingConfiguration asset, int level)
        {
            var method = typeof(LevelPacingConfiguration).GetMethod(
                "ColorsForLevel",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (int)method!.Invoke(asset, new object[] { level });
        }

        private static LevelThresholdOverride[] GetOverrides(LevelPacingConfiguration asset)
        {
            var field = typeof(LevelPacingConfiguration).GetField(
                "_thresholdOverrides",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (LevelThresholdOverride[])field!.GetValue(asset);
        }
    }
}

using System.Collections.Generic;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Plain-C# level-pacing invariant checks — shared by <see cref="LevelPacingConfiguration" />'s
    /// editor-only <c>OnValidate</c> warnings and EditMode test coverage, so authoring checks and test
    /// checks can't drift by verifying the same invariants two different ways. The generation catalogue
    /// checks its own entries (<see cref="LevelGenerationSettings.Validate" />).</summary>
    internal static class LevelPacingValidator
    {
        /// <summary>Returns one issue message per problem found; an empty list means the config is clean.
        /// <paramref name="levelsToCheck" /> bounds the non-monotonic-threshold scan (0 skips it) — the
        /// caller derives this from its own scoring curve, since that data isn't on the interface.</summary>
        internal static IReadOnlyList<string> Validate(
            ILevelPacingConfiguration config, int levelsToCheck, string configName)
        {
            var issues = new List<string>();
            WarnOnPresetIssues(config, configName, issues);
            WarnOnEmptyWeightedSets(config, configName, issues);
            WarnOnNonMonotonicThreshold(config, levelsToCheck, configName, issues);
            return issues;
        }

        /// <summary>
        ///     Debug presets skip <see cref="LevelGenerator" /> and always play on the sky, so a preset that lists a
        ///     ground-only actor (<paramref name="gridActorCatalogue" />'s <c>RequiresGround</c>) would float it
        ///     over the sky backdrop. One issue per such preset and actor; a gate whose count is 0 is fine.
        /// </summary>
        internal static IReadOnlyList<string> ValidateGroundOnlyInPresets(
            IReadOnlyList<LevelRangeEntry> presets, IReadOnlyList<GridActorContentEntry> gridActorCatalogue,
            string configName)
        {
            var issues = new List<string>();
            for (var i = 0; i < presets.Count; i++)
            {
                var gates = presets[i].Parameters?.GridActorGates;
                if (gates == null)
                {
                    continue;
                }

                foreach (var gate in gates)
                {
                    if (Mathf.Max(gate.Count.Min, gate.Count.Max) > 0 && RequiresGround(gate.Type, gridActorCatalogue))
                    {
                        issues.Add(
                            $"LevelPacingConfiguration ({configName}): preset {presets[i].FromLevel} lists {gate.Type}, " +
                            "which only belongs on the ground, but presets always play on the sky — it would float. " +
                            "Set its count to 0.");
                    }
                }
            }

            return issues;
        }

        private static void WarnOnPresetIssues(ILevelPacingConfiguration config, string configName, List<string> issues)
        {
            var presets = config.DebugPresets;
            var seenIds = new HashSet<int>();

            for (var i = 0; i < presets.Count; i++)
            {
                if (!presets[i].IsPreset)
                {
                    issues.Add(
                        $"LevelPacingConfiguration ({configName}): preset starting at {presets[i].FromLevel} is " +
                        "for a positive level, which is generated; it's ignored. Key presets by a negative FromLevel.");
                    continue;
                }

                if (!seenIds.Add(presets[i].FromLevel))
                {
                    issues.Add(
                        $"LevelPacingConfiguration ({configName}): duplicate preset ID {presets[i].FromLevel} " +
                        "— each preset must have a unique FromLevel.");
                }
            }
        }

        private static void WarnOnEmptyWeightedSets(ILevelPacingConfiguration config, string configName, List<string> issues)
        {
            var presets = config.DebugPresets;
            for (var i = 0; i < presets.Count; i++)
            {
                var weights = presets[i].Parameters?.BalloonWeights;
                var hasPositiveWeight = false;
                if (weights != null)
                {
                    foreach (var weight in weights)
                    {
                        if (weight.Weight > 0f)
                        {
                            hasPositiveWeight = true;
                            break;
                        }
                    }
                }

                if (!hasPositiveWeight)
                {
                    issues.Add(
                        $"LevelPacingConfiguration ({configName}): preset {presets[i].FromLevel} has no " +
                        "balloon type with a positive weight — nothing could spawn.");
                }
            }
        }

        private static bool RequiresGround(
            GridActorType type, IReadOnlyList<GridActorContentEntry> gridActorCatalogue)
        {
            for (var i = 0; i < gridActorCatalogue.Count; i++)
            {
                if (gridActorCatalogue[i].Type == type && gridActorCatalogue[i].RequiresGround)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WarnOnNonMonotonicThreshold(
            ILevelPacingConfiguration config, int levelsToCheck, string configName, List<string> issues)
        {
            var previousTotal = int.MinValue;

            for (var level = 1; level <= levelsToCheck; level++)
            {
                var perColor = config.ThresholdForLevel(level);
                var colors = config.ColorsForLevel(level);
                var total = perColor * colors;

                if (perColor <= 0)
                {
                    issues.Add(
                        $"LevelPacingConfiguration ({configName}): threshold at level {level} is non-positive " +
                        $"({perColor}) — check the scoring curve milestones.");
                }
                else if (total < previousTotal)
                {
                    issues.Add(
                        $"LevelPacingConfiguration ({configName}): total difficulty drops at level {level} " +
                        $"({previousTotal} → {total}, {colors} colors × {perColor}/color) — " +
                        "ensure the cumulative curve increment grows with level.");
                }

                previousTotal = total;
            }
        }
    }
}

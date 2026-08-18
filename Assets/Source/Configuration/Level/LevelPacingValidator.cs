using System.Collections.Generic;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Plain-C# level-pacing invariant checks — shared by <see cref="LevelPacingConfiguration" />'s
    /// editor-only <c>OnValidate</c> warnings and EditMode test coverage, so authoring checks and test
    /// checks can't drift by verifying the same invariants two different ways.</summary>
    internal static class LevelPacingValidator
    {
        /// <summary>Returns one issue message per problem found; an empty list means the config is clean.
        /// <paramref name="levelsToCheck" /> bounds the non-monotonic-threshold scan (0 skips it) — the
        /// caller derives this from its own scoring curve, since that data isn't on the interface.</summary>
        internal static IReadOnlyList<string> Validate(
            ILevelPacingConfiguration config, int levelsToCheck, string configName)
        {
            var issues = new List<string>();
            WarnOnGapsAndOverlaps(config, configName, issues);
            WarnOnFallbackIssues(config, configName, issues);
            WarnOnEmptyWeightedSets(config, configName, issues);
            WarnOnNonMonotonicThreshold(config, levelsToCheck, configName, issues);
            return issues;
        }

        private static void WarnOnGapsAndOverlaps(ILevelPacingConfiguration config, string configName, List<string> issues)
        {
            var ranges = config.Ranges;
            for (var i = 1; i < ranges.Count; i++)
            {
                var previous = ranges[i - 1];
                var current = ranges[i];

                if (previous.IsFallback || current.IsFallback)
                {
                    continue;
                }

                if (previous.IsOpenEnded)
                {
                    issues.Add(
                        $"LevelPacingConfiguration ({configName}): range starting at {previous.FromLevel} is open-ended " +
                        $"but is followed by a range starting at {current.FromLevel} — the later range is unreachable.");
                    continue;
                }

                if (current.FromLevel != previous.ToLevel + 1)
                {
                    issues.Add(
                        $"LevelPacingConfiguration ({configName}): gap or overlap between ranges " +
                        $"[{previous.FromLevel}-{previous.ToLevel}] and [{current.FromLevel}-{current.ToLevel}] " +
                        "— ranges must be contiguous.");
                }
            }
        }

        private static void WarnOnFallbackIssues(ILevelPacingConfiguration config, string configName, List<string> issues)
        {
            var ranges = config.Ranges;
            var hasDefault = false;
            var seenIds = new HashSet<int>();

            for (var i = 0; i < ranges.Count; i++)
            {
                if (!ranges[i].IsFallback)
                {
                    continue;
                }

                if (ranges[i].FromLevel == -1)
                {
                    hasDefault = true;
                }

                if (!seenIds.Add(ranges[i].FromLevel))
                {
                    issues.Add(
                        $"LevelPacingConfiguration ({configName}): duplicate fallback ID {ranges[i].FromLevel} " +
                        "— each fallback must have a unique FromLevel.");
                }
            }

            if (!hasDefault)
            {
                issues.Add(
                    $"LevelPacingConfiguration ({configName}): missing default fallback (FromLevel = -1). " +
                    "Normal gameplay requires exactly one entry with FromLevel = -1.");
            }
        }

        private static void WarnOnEmptyWeightedSets(ILevelPacingConfiguration config, string configName, List<string> issues)
        {
            var ranges = config.Ranges;
            for (var i = 0; i < ranges.Count; i++)
            {
                var weights = ranges[i].Parameters?.BalloonWeights;
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
                        $"LevelPacingConfiguration ({configName}): range starting at {ranges[i].FromLevel} has no " +
                        "balloon type with a positive weight — nothing could spawn.");
                }
            }
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

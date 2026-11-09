using System.Collections.Generic;
using BalloonParty.Balloon.Type;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Level pacing; read by <c>LevelDifficultyResolver</c> (and by the balloon spawner's pool prewarm,
    ///     via <see cref="MaxConcurrentBalloons" />).
    /// </summary>
    internal interface ILevelPacingConfiguration
    {
        /// <summary>Authored debug presets, keyed by a negative <c>FromLevel</c> the start-level cheat selects.</summary>
        IReadOnlyList<LevelRangeEntry> DebugPresets { get; }

        /// <summary>When balloon lines arrive; read by the spawn clock.</summary>
        ISpawnClockSettings SpawnClock { get; }

        /// <summary>The bubbles that come in with the lines on water levels; read by the balloon spawner.</summary>
        IBubbleRiseSettings BubbleRise { get; }

        /// <summary>
        ///     The mix for <paramref name="level" />: generated for a positive level, the matching debug preset
        ///     for a negative one. <paramref name="runSeed" /> (<c>IRunSeed</c>) picks the run's ground and sky
        ///     levels, which decide the ground-only actors and the meadow's clouds.
        /// </summary>
        RangedLevelParameters ParametersForLevel(int level, int runSeed);

        /// <summary>The seed that rolls <paramref name="level" />'s mix; the same level always gets the same seed.</summary>
        int SeedForLevel(int level);

        /// <summary>
        ///     Whether <paramref name="level" /> is set in the sky, on the ground or over water in the run rolled by
        ///     <paramref name="runSeed" />. Debug presets (level &lt; 1) are sky.
        /// </summary>
        ScenarioKind ScenarioForLevel(int level, int runSeed);

        /// <summary>Points required per colour to complete <paramref name="level" /> — derived from the scoring curve's cumulative milestones, divided by colour count, and snapped to a clean multiple.</summary>
        int ThresholdForLevel(int level);

        /// <summary>Count of colours allowed at <paramref name="level" />.</summary>
        int ColorsForLevel(int level);

        /// <summary>
        ///     Upper bound on how many of a balloon type can share the board at any level — the pool prewarm
        ///     size. An uncapped type (0 cap) scales to the largest board (<paramref name="columns" /> × the most
        ///     board lines). Returns 0 for a type no level can include.
        /// </summary>
        int MaxConcurrentBalloons(BalloonType type, int columns);
    }
}

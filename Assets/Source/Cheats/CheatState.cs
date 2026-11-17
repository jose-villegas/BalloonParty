#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE

using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>What <see cref="CheatState.PerfOff" /> can switch off.</summary>
    [System.Flags]
    internal enum PerfOff
    {
        None = 0,

        /// <summary>The effects-clearing lens: its mask, both effects maps and their composites.</summary>
        Lens = 1,

        /// <summary>Only the HUD's effects map (the score trails').</summary>
        HudMap = 2,

        /// <summary>The scene capture the bounce light and soft shadows are made from.</summary>
        Capture = 4,

        /// <summary>The balloons' idle-sway Animators.</summary>
        Animators = 8,

        /// <summary>Post-processing on the main camera: bloom, tonemapping, the colour grade.</summary>
        Post = 16,

        /// <summary>The HUD canvases (they stop drawing; their scripts keep running).</summary>
        Hud = 32
    }

    /// <summary>Shared dev-cheat flags read by gameplay under the same compile guard, so they strip from
    /// release builds. Static because the reader (gameplay) and writer (a cheat) live in different objects
    /// and we don't want a cheat concept in production DI.</summary>
    internal static class CheatState
    {
        internal const string StartLevelPrefKey = "BalloonParty.Cheats.StartLevel";

        private static readonly int UrpNeutralTonemapId = Shader.PropertyToID("_BalloonPartyUrpNeutral");

        /// <summary>While true, the current level is LOCKED: score trails still fly on a pop, but nothing sticks —
        /// <c>ClaimProgress</c> grants the visual points without advancing progress, and both
        /// <c>OnTrailArrived</c> handlers (score + level) skip their commit, so score and bars stay put. The
        /// level-up detection in <c>TryBeginCompleting</c> never triggers (no ceremony), <c>PlayerHealthController
        /// .Damage</c> is a no-op (hearts never drain), and <c>RunController.EndRun</c> is a no-op (no loss).
        /// Toggled by <see cref="BlockLevelUpCheat" />.</summary>
        public static bool BlockLevelUp;

        /// <summary>Parts of the game a performance run has switched off, to measure what each one costs on a
        /// device (the autoplay's <c>off</c> key, <c>Tools/perf/device_perf.py --off</c>). The game looks wrong
        /// with any of them set; nothing but a measurement sets them.</summary>
        public static PerfOff PerfOff;

        /// <summary>While true, <c>ScoreTrailService</c> resolves each score group INSTANTLY — it publishes the
        /// arrival immediately with the group's full points instead of spawning flying pen-trails. Set only for
        /// the span of a bulk progress fill (see <see cref="ScoreCheatHelper.FillColor" />): filling a whole
        /// level's threshold through the real pipeline would otherwise spawn one pen per point (a level's worth
        /// in a single frame). Score, bars and progress still update through their normal arrival handlers.</summary>
        public static bool InstantScoreTrails;

        /// <summary>The level a fresh run BEGINS at (dev "play from level N"). 1 = normal. Read by
        /// LevelController/LevelDifficultyResolver on run reset. The Level Pacing window stashes it in
        /// EditorPrefs under <see cref="StartLevelPrefKey" /> to carry it across the enter-play reload;
        /// the cheat menu sets it directly and restarts.</summary>
        public static int StartLevel = 1;

        /// <summary>One-shot: the next run reset keeps the run's seed instead of rolling a new one, then clears
        /// this. Set by <see cref="JumpToScenarioCheat" />, which picks its level from the current seed's
        /// schedule; read by <c>RunSeed.ResetRun</c>.</summary>
        public static bool KeepRunSeed;

        /// <summary>Flips the tough snake's springs (<c>IToughSnakeSpringSettings.Enabled</c>) from what the
        /// config says, to compare with and without them in play. Toggled by <see cref="ToughSnakeSpringsCheat" />;
        /// read by <c>ToughSnakeSpringView</c>.</summary>
        public static bool FlipToughSnakeSprings;

        /// <summary>One-shot: when not 0, the next run reset takes this seed instead of rolling one, then clears
        /// this, so a staged scene deals the same board every time. Set by the scene stage
        /// (<c>Editor/Staging</c>); read by <c>RunSeed.ResetRun</c>.</summary>
        public static int ForcedRunSeed;

        /// <summary>While true, no balloons come on their own: a run's opening board isn't dealt
        /// (<c>BalloonSpawner.SpawnAsync</c>) and the spawn clock sends no lines, so the board holds only what's
        /// put on it. The level's obstacles still come. Set by the scene stage (<c>Editor/Staging</c>) for its
        /// still scenes.</summary>
        public static bool EmptyBoard;

        /// <summary>Speed multiplier for the Realtime time-of-day clock (see <c>TimeOfDayClock</c>):
        /// 1 = the authored <c>SecondsPerCycle</c>, 0 = frozen, higher = faster. Set by
        /// <see cref="TimeOfDayCheat" />; read (under this guard) by the clock each tick.</summary>
        public static float TimeOfDaySpeedScale = 1f;

        /// <summary>While true, <c>ScenarioGradeView</c> drops the scenario colour grade, leaving the base look
        /// of the default volume profile, to compare the scenes with and without it. Set by
        /// <see cref="ScenarioGradeCheat" />.</summary>
        public static bool ScenarioGradeOff;

        /// <summary>Sticky "this session touched the cheat console" flag, read by
        /// <c>GameplayMetricsService</c> at every flush so the records it writes are filterable. The other
        /// flags cannot answer that question on their own: <see cref="StartLevel" /> and
        /// <see cref="TimeOfDaySpeedScale" /> default to 1 rather than 0, and the one-shot cheats
        /// (<c>AwardScorePopCheat</c>, <c>TriggerLevelUpCheat</c>, <c>AddShieldCheat</c>, …) leave no trace
        /// at all. Set when <c>CheatConsoleView</c> opens — every cheat except the Level Pacing window goes
        /// through the console, and that window already writes <see cref="StartLevel" />.</summary>
        public static bool AnyCheatUsed;

        /// <summary>While true, the colour-grading LUT tonemaps with URP's own Neutral curve instead of the PBR
        /// Neutral the game builds (<c>Display/LutBuilderHdr.shader</c>), to compare the two. Set through
        /// <see cref="SetUrpNeutralTonemap" />, which also sets the shader global the LUT builder reads.</summary>
        public static bool UrpNeutralTonemap { get; private set; }

        internal static void SetUrpNeutralTonemap(bool on)
        {
            UrpNeutralTonemap = on;
            Shader.SetGlobalFloat(UrpNeutralTonemapId, on ? 1f : 0f);
        }

        // With Enter Play Mode Options disabling domain reload, statics survive between play sessions — reset
        // the flags on each play start so they never silently carry over. Runs earliest, before scene load.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            BlockLevelUp = false;
            InstantScoreTrails = false;
            TimeOfDaySpeedScale = 1f;
            AnyCheatUsed = false;
            KeepRunSeed = false;
            ScenarioGradeOff = false;
            SetUrpNeutralTonemap(false);
#if UNITY_EDITOR
            // Pick up (and consume) the "play from here" level the pacing window stashed before entering play.
            StartLevel = UnityEditor.EditorPrefs.GetInt(StartLevelPrefKey, 1);
            if (StartLevel >= 0)
            {
                StartLevel = Mathf.Max(1, StartLevel);
            }

            UnityEditor.EditorPrefs.DeleteKey(StartLevelPrefKey);
#else
            StartLevel = 1;
#endif
        }
    }
}
#endif

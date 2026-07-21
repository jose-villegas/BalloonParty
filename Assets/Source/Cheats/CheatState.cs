#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>Shared dev-cheat flags read by gameplay under the same compile guard, so they strip from
    /// release builds. Static because the reader (gameplay) and writer (a cheat) live in different objects
    /// and we don't want a cheat concept in production DI.</summary>
    internal static class CheatState
    {
        /// <summary>While true, the current level is LOCKED: score trails still fly on a pop, but nothing sticks —
        /// <c>ClaimProgress</c> grants the visual points without advancing progress, and both <c>OnTrailArrived</c>
        /// handlers (score + level) skip their commit, so score and bars stay put. <c>WillLevelUp</c>/
        /// <c>CheckLevelUp</c> never complete (no cinematic or ceremony), <c>PlayerHealthController.Damage</c> is a
        /// no-op (hearts never drain), and <c>RunController.EndRun</c> is a no-op (no loss). Toggled by
        /// <see cref="BlockLevelUpCheat" />.</summary>
        public static bool BlockLevelUp;

        /// <summary>The level a fresh run BEGINS at (dev "play from level N"). 1 = normal. Read by
        /// LevelController/LevelDifficultyResolver on run reset. The Level Pacing window stashes it in
        /// EditorPrefs under <see cref="StartLevelPrefKey" /> to carry it across the enter-play reload;
        /// the cheat menu sets it directly and restarts.</summary>
        public static int StartLevel = 1;

        internal const string StartLevelPrefKey = "BalloonParty.Cheats.StartLevel";

        // With Enter Play Mode Options disabling domain reload, statics survive between play sessions — reset
        // the flags on each play start so they never silently carry over. Runs earliest, before scene load.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            BlockLevelUp = false;
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

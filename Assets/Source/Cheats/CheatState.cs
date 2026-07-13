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

        // With Enter Play Mode Options disabling domain reload, statics survive between play sessions — reset
        // the lock on each play start so it never silently carries over. Runs earliest, before scene load.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            BlockLevelUp = false;
        }
    }
}
#endif

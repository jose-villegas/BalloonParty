using UnityEngine;

namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Static hand-off (like <see cref="Navigation" />) for the launch "ascend" cloud-scroll, so the
    ///     Launcher's Play trigger can drive the Game-scope cloud field without cross-scene DI: the trigger
    ///     <see cref="Begin" />s the roll, waits <see cref="Duration" />, then transitions to Game — so the
    ///     game doesn't appear until the initial scroll finishes. <c>CloudFieldService</c> reflects this into
    ///     its cloud world offset; nothing else needs to know.
    /// </summary>
    internal static class LaunchAscend
    {
        public static bool IsActive { get; private set; }
        public static float StartTime { get; private set; }
        public static float Duration { get; private set; }
        public static Vector2 Scroll { get; private set; }

        public static void Begin(float duration, Vector2 scroll)
        {
            Duration = Mathf.Max(0.0001f, duration);
            Scroll = scroll;
            StartTime = Time.unscaledTime;
            IsActive = true;
        }

        // Enter Play Mode with domain reload off keeps statics alive between sessions — reset on each start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsActive = false;
            StartTime = 0f;
            Duration = 0f;
            Scroll = Vector2.zero;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Disturbance
{
    /// <summary>
    ///     Static hand-off (like <c>LaunchAscend</c>) letting any View request a one-shot disturbance stamp
    ///     without injecting <see cref="DisturbanceFieldService" /> — so it also works from the launcher, whose
    ///     buttons live in a sibling scope with no cross-scene DI. Enqueued requests are drained and applied by
    ///     <see cref="DisturbanceStampRequestReader" />, which owns the field.
    /// </summary>
    internal static class DisturbanceStampRequest
    {
        private static readonly Queue<PendingStamp> Pending = new();

        public static void Enqueue(Vector3 world, float radius, float strength, Vector2 direction, float duration)
        {
            Pending.Enqueue(new PendingStamp
            {
                World = world,
                Radius = radius,
                Strength = strength,
                Direction = direction,
                Duration = duration
            });
        }

        public static bool TryDequeue(out PendingStamp stamp)
        {
            if (Pending.Count > 0)
            {
                stamp = Pending.Dequeue();
                return true;
            }

            stamp = default;
            return false;
        }

        // Enter Play Mode with domain reload off keeps statics alive between sessions — drop stale requests.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Pending.Clear();
        }

        internal struct PendingStamp
        {
            public Vector3 World;
            public float Radius;
            public float Strength;
            public Vector2 Direction;
            public float Duration;
        }
    }
}

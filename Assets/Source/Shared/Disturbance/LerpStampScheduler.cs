using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Disturbance
{
    /// <summary>
    ///     Ramps "duration" stamps over time: rather than a single-frame pop, a stamp's strength is
    ///     spread across its duration as a sequence of instant sub-stamps, producing a smooth shockwave.
    ///     Holds at most <c>maxStamps</c> active ramps, evicting the oldest when full.
    /// </summary>
    internal class LerpStampScheduler
    {
        private readonly int _maxStamps;
        private readonly List<LerpStamp> _active = new();

        public LerpStampScheduler(int maxStamps)
        {
            _maxStamps = maxStamps;
        }

        public void Add(Vector3 position, float radius, float strength, Vector2 direction, float duration)
        {
            if (_active.Count >= _maxStamps)
            {
                _active.RemoveAt(0);
            }

            _active.Add(new LerpStamp
            {
                Position = position,
                Radius = radius,
                Strength = strength,
                Direction = direction,
                Duration = duration,
                Elapsed = 0f,
                LastT = 0f
            });
        }

        /// <summary>
        ///     Advances every active ramp by <paramref name="dt" />, calling <paramref name="emit" />
        ///     (position, radius, strength, direction) with the strength accrued this step.
        /// </summary>
        public void Tick(float dt, Action<Vector3, float, float, Vector2> emit)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var s = _active[i];
                s.Elapsed += dt;
                var t = Mathf.Clamp01(s.Elapsed / s.Duration);

                var delta = t - s.LastT;
                s.LastT = t;
                _active[i] = s;

                if (delta > 0.0001f)
                {
                    var radiusNow = Mathf.Lerp(s.Radius * 0.3f, s.Radius, t);
                    emit(s.Position, radiusNow, s.Strength * delta, s.Direction);
                }

                if (t >= 1f)
                {
                    _active.RemoveAt(i);
                }
            }
        }

        private struct LerpStamp
        {
            public Vector3 Position;
            public float Radius;
            public float Strength;
            public Vector2 Direction;
            public float Duration;
            public float Elapsed;
            public float LastT;
        }
    }
}

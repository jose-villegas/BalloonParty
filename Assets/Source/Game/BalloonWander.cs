using BalloonParty.UI.ClothRibbon;
using UnityEngine;

namespace BalloonParty.Game
{
    /// <summary>
    ///     A balloon drifting about an area on its own: it heads for one spot after another, picked at random,
    ///     easing in as it arrives, and slides round a keep-out it may never enter. A spot it can't reach in time is
    ///     given up for another. <see cref="Shown" /> adds a gentle bob.
    /// </summary>
    internal sealed class BalloonWander
    {
        private const int TargetTries = 32;

        private readonly BalloonWanderSettings _settings;
        private readonly Rect _bounds;
        private readonly KeepOut _keepOut;
        private readonly System.Random _random;

        private Vector2 _target;
        private float _sinceTarget;
        private float _time;

        internal Vector2 Position { get; private set; }

        internal Vector2 Velocity { get; private set; }

        internal Vector2 Target => _target;

        internal Vector2 Shown => Position + Vector2.up * (_settings.Bob * Mathf.Sin(2f * Mathf.PI * _settings.BobSpeed * _time));

        /// <summary>
        ///     Starts at <paramref name="start" />, or somewhere allowed if that isn't, within <paramref name="bounds" />
        ///     and clear of <paramref name="keepOut" />.
        /// </summary>
        internal BalloonWander(
            BalloonWanderSettings settings, Rect bounds, KeepOut keepOut, Vector2 start, int seed)
        {
            _settings = settings;
            _bounds = bounds;
            _keepOut = keepOut;
            _random = new System.Random(seed);
            Position = Allowed(start) ? start : RandomAllowed(start);
            _target = Position;
            PickTarget();
        }

        internal bool Allowed(Vector2 point)
        {
            return _bounds.Contains(point) && !_keepOut.Contains(point);
        }

        internal void Step(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _time += deltaTime;
            _sinceTarget += deltaTime;
            var toTarget = _target - Position;
            var distance = toTarget.magnitude;
            if (distance <= _settings.ArrivedWithin || _sinceTarget >= _settings.RetargetSeconds)
            {
                PickTarget();
                toTarget = _target - Position;
                distance = toTarget.magnitude;
            }

            var easing = Mathf.Clamp01(distance / (3f * _settings.ArrivedWithin));
            var wanted = distance > 1e-4f ? toTarget / distance * (_settings.Speed * easing) : Vector2.zero;
            var velocity = Vector2.MoveTowards(Velocity, wanted, _settings.Acceleration * deltaTime);
            var next = Position + velocity * deltaTime;

            // Slide round the keep-out: out to its edge, losing only the part of the motion that went in.
            if (_keepOut.Contains(next))
            {
                next = _keepOut.PushOut(next, out var outward);
                var inward = Vector2.Dot(velocity, outward);
                if (inward < 0f)
                {
                    velocity -= inward * outward;
                }
            }

            var clamped = new Vector2(
                Mathf.Clamp(next.x, _bounds.xMin, _bounds.xMax), Mathf.Clamp(next.y, _bounds.yMin, _bounds.yMax));
            if (!Mathf.Approximately(clamped.x, next.x))
            {
                velocity.x = 0f;
            }

            if (!Mathf.Approximately(clamped.y, next.y))
            {
                velocity.y = 0f;
            }

            // Clamping into the bounds can land back in the keep-out where the two meet; it then stays put.
            if (!_keepOut.Contains(clamped))
            {
                Position = clamped;
            }
            else
            {
                velocity = Vector2.zero;
            }

            Velocity = velocity;
        }

        private void PickTarget()
        {
            _sinceTarget = 0f;
            for (var i = 0; i < TargetTries; i++)
            {
                var candidate = RandomPoint();
                if (Allowed(candidate) && Vector2.Distance(candidate, Position) > 2f * _settings.ArrivedWithin)
                {
                    _target = candidate;
                    return;
                }
            }
        }

        private Vector2 RandomAllowed(Vector2 fallback)
        {
            for (var i = 0; i < TargetTries; i++)
            {
                var candidate = RandomPoint();
                if (Allowed(candidate))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private Vector2 RandomPoint()
        {
            return new Vector2(
                Mathf.Lerp(_bounds.xMin, _bounds.xMax, (float)_random.NextDouble()),
                Mathf.Lerp(_bounds.yMin, _bounds.yMax, (float)_random.NextDouble()));
        }
    }
}

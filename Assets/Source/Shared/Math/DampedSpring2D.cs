using UnityEngine;

namespace BalloonParty.Shared.Math
{
    /// <summary>
    /// Second-order damped spring tracking a 2D target.
    /// Pure value type — no Unity lifecycle, no allocations.
    /// </summary>
    internal struct DampedSpring2D
    {
        public Vector2 Position;
        public Vector2 Velocity;

        public DampedSpring2D(Vector2 initial)
        {
            Position = initial;
            Velocity = Vector2.zero;
        }

        public void Step(Vector2 target, float frequency, float damping, float dt)
        {
            var omega = frequency * 2f * Mathf.PI;
            Velocity += (target - Position) * (omega * omega * dt);
            Velocity *= Mathf.Exp(-2f * damping * omega * dt);
            Position += Velocity * dt;
        }

        public void AddImpulse(Vector2 impulse)
        {
            Velocity += impulse;
        }

        public void Reset(Vector2 position)
        {
            Position = position;
            Velocity = Vector2.zero;
        }
    }
}

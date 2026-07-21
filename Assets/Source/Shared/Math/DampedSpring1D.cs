using UnityEngine;

namespace BalloonParty.Shared.Math
{
    /// <summary>
    /// Second-order damped spring tracking a scalar target.
    /// Pure value type — no Unity lifecycle, no allocations.
    /// </summary>
    internal struct DampedSpring1D
    {
        public float Position;
        public float Velocity;

        public DampedSpring1D(float initial)
        {
            Position = initial;
            Velocity = 0f;
        }

        public void Step(float target, float frequency, float damping, float dt)
        {
            var omega = frequency * 2f * Mathf.PI;
            Velocity += (target - Position) * (omega * omega * dt);
            Velocity *= Mathf.Exp(-2f * damping * omega * dt);
            Position += Velocity * dt;
        }

        public void AddImpulse(float impulse)
        {
            Velocity += impulse;
        }

        public void Reset(float position)
        {
            Position = position;
            Velocity = 0f;
        }
    }
}

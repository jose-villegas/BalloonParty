using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared
{
    /// <summary>Frame-scoped impact events, written via <see cref="Report"/> and cleared each end-of-frame.</summary>
    internal class ImpactEventBus : ILateTickable
    {
        private readonly List<Impact> _pending = new();

        internal IReadOnlyList<Impact> Pending => _pending;

        /// <summary>
        ///     <paramref name="blast" /> marks the impact as an explosion that throws aside what it lands in (the
        ///     water's crater). Left out, the impact is an ordinary one.
        /// </summary>
        internal void Report(Vector3 position, float radius, Vector2 direction = default, Blast blast = default)
        {
            _pending.Add(new Impact(position, radius, direction, blast));
        }

        public void LateTick()
        {
            if (_pending.Count > 0)
            {
                _pending.Clear();
            }
        }

        internal readonly struct Impact
        {
            internal readonly Vector3 Position;
            internal readonly float Radius;
            // The stamp's heading (a ball's travel), zero for a burst that pushes every way.
            internal readonly Vector2 Direction;
            // What the impact throws aside, for an explosion; none (the default) for every other impact.
            internal readonly Blast Blast;

            internal Impact(Vector3 position, float radius, Vector2 direction, Blast blast = default)
            {
                Position = position;
                Radius = radius;
                Direction = direction;
                Blast = blast;
            }
        }

        /// <summary>
        ///     An explosion's share of an impact: how far it clears what it lands in, how hard, and for how long it
        ///     throws. The default is no blast, which leaves an impact as it was before blasts existed.
        /// </summary>
        internal readonly struct Blast
        {
            // World units the blast clears around the impact: its crater's edge.
            internal readonly float Radius;
            // How hard it throws, 1 being a bomb.
            internal readonly float Strength;
            // Seconds it takes to throw aside what it clears.
            internal readonly float Seconds;
            // For a blast that runs along a line (a lance's discharge): the line, from where it starts to where
            // it ends, with the impact's position at its middle. It runs it in its seconds. Zero for a blast that
            // stays where it lands.
            internal readonly Vector2 Line;

            internal bool IsBlast => Radius > 0f && Strength > 0f;

            internal Blast(float radius, float strength, float seconds, Vector2 line = default)
            {
                Radius = radius;
                Strength = strength;
                Seconds = seconds;
                Line = line;
            }
        }
    }
}

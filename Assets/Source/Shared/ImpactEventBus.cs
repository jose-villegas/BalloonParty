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

        internal void Report(Vector3 position, float radius, Vector2 direction = default)
        {
            _pending.Add(new Impact(position, radius, direction));
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

            internal Impact(Vector3 position, float radius, Vector2 direction)
            {
                Position = position;
                Radius = radius;
                Direction = direction;
            }
        }
    }
}

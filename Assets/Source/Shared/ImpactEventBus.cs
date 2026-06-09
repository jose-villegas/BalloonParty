using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared
{
    /// <summary>
    /// Frame-scoped one-shot impact events. Any system (bomb, laser, paint,
    /// projectile path) writes via <see cref="Report"/>; any visual consumer
    /// reads <see cref="Pending"/> during update/late-update. Cleared
    /// automatically at end-of-frame via <see cref="ILateTickable"/>.
    /// </summary>
    internal class ImpactEventBus : ILateTickable
    {
        private readonly List<Impact> _pending = new();

        internal IReadOnlyList<Impact> Pending => _pending;

        internal void Report(Vector3 position, float radius)
        {
            _pending.Add(new Impact(position, radius));
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

            internal Impact(Vector3 position, float radius)
            {
                Position = position;
                Radius = radius;
            }
        }
    }
}


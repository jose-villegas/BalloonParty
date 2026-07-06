using System.Collections.Generic;
using BalloonParty.Game.Run;
using UnityEngine;

namespace BalloonParty.Game.Health
{
    /// <summary>
    ///     Heart trails currently in flight; launch order preserved so [0] is the next to land.
    /// </summary>
    internal sealed class HeartTrailTracker : IRunResettable
    {
        private readonly List<Transform> _active = new();

        public IReadOnlyList<Transform> Active => _active;

        public int ResetOrder => RunResetOrder.Counters;

        public void Add(Transform trail)
        {
            _active.Add(trail);
        }

        public void Remove(Transform trail)
        {
            _active.Remove(trail);
        }

        public void ResetRun(int generation)
        {
            _active.Clear();
        }
    }
}

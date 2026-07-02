using System.Collections.Generic;
using BalloonParty.Game.Run;
using UnityEngine;

namespace BalloonParty.Game.Health
{
    /// <summary>
    ///     The heart trails currently in flight (health UI → overflow pop). <c>HeartTrailController</c>
    ///     adds one when it spawns and removes it on arrival; the heart-drain cinematic follows the
    ///     hearts in this set (launch order preserved — [0] is the next to land). Lives in the parent scope so both the UI-scope controller and the
    ///     cinematic can reach it. Cleared on run reset.
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

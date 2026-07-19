using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Central registry for all in-flight trails: per-ID lookup plus bulk operations.
    /// </summary>
    internal class TrailFlightRegistry<TId>
        where TId : struct, IEquatable<TId>
    {
        private readonly Dictionary<TId, TrailFlight> _flights = new();

        internal int ActiveCount => _flights.Count;

        internal TrailFlight Register(TId id, Transform transform, Vector3 origin)
        {
            var flight = new TrailFlight(transform, origin);
            _flights[id] = flight;
            return flight;
        }

        internal void Unregister(TId id)
        {
            // Mark the handle inert: holders of a direct reference (the level-up cinematic's tracked
            // flight) must stop steering/completing the pooled instance once its arrival has fired.
            if (_flights.Remove(id, out var flight))
            {
                flight.MarkCompleted();
            }
        }

        internal bool TryGet(TId id, out TrailFlight flight)
        {
            return _flights.TryGetValue(id, out flight);
        }

        internal TrailFlight Get(TId id)
        {
            return _flights.TryGetValue(id, out var flight) ? flight : null;
        }

        internal bool Contains(TId id)
        {
            return _flights.ContainsKey(id);
        }

        internal void PauseAll()
        {
            foreach (var flight in _flights.Values)
            {
                flight.Pause();
            }
        }

        /// <remarks>
        ///     Snapshots and clears first so <see cref="Unregister"/> callbacks can't mutate mid-iteration.
        /// </remarks>
        internal void CompleteAll()
        {
            var snapshot = new List<TrailFlight>(_flights.Values);
            _flights.Clear();

            foreach (var flight in snapshot)
            {
                flight.Complete();
            }
        }
    }
}

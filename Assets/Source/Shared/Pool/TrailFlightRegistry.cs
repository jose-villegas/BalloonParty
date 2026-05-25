using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Central registry for all in-flight trails. Provides per-ID lookup and
    ///     bulk operations (pause, resume, complete, speed).
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
            _flights.Remove(id);
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

        internal void PauseWhere(Func<TId, bool> predicate)
        {
            foreach (var kvp in _flights)
            {
                if (predicate(kvp.Key))
                {
                    kvp.Value.Pause();
                }
            }
        }

        internal void ResumeAll()
        {
            foreach (var flight in _flights.Values)
            {
                flight.Resume();
            }
        }

        /// <remarks>
        ///     Snapshots and clears the dictionary before completing so that
        ///     tween onComplete callbacks that call <see cref="Unregister"/>
        ///     don't modify the collection during iteration.
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

        internal void CompleteWhere(Func<TId, bool> predicate)
        {
            var toComplete = new List<TrailFlight>();
            var toRemove = new List<TId>();

            foreach (var kvp in _flights)
            {
                if (predicate(kvp.Key))
                {
                    toComplete.Add(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _flights.Remove(id);
            }

            foreach (var flight in toComplete)
            {
                flight.Complete();
            }
        }

        internal void StopAll()
        {
            foreach (var flight in _flights.Values)
            {
                flight.Stop();
            }

            _flights.Clear();
        }

        internal void SetSpeedAll(float speed)
        {
            foreach (var flight in _flights.Values)
            {
                flight.SetSpeed(speed);
            }
        }

        internal void SetSpeedWhere(float speed, Func<TId, bool> predicate)
        {
            foreach (var kvp in _flights)
            {
                if (predicate(kvp.Key))
                {
                    kvp.Value.SetSpeed(speed);
                }
            }
        }
    }
}

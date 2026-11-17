using System.Collections.Generic;
using BalloonParty.Slots.Actor;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     How many cycles each lone roamer has stood since it last moved, and which one moved last. The balancer
    ///     counts a cycle with <see cref="BeginCycle" />, then <see cref="Stand" /> for every roamer on the board, then
    ///     <see cref="EndCycle" />, which forgets the ones it didn't see: a roamer that left the board, or joined a
    ///     formation, starts its rest over when it's free again.
    /// </summary>
    internal sealed class RoamRests
    {
        private readonly Dictionary<IWriteableDynamicSlotActor, Rest> _rests = new();
        private readonly List<IWriteableDynamicSlotActor> _gone = new();

        private int _cycle;
        private IWriteableDynamicSlotActor _lastMover;

        /// <summary>The roamer that moved last is still on its way, so no other may set off.</summary>
        internal bool LastMoverOnItsWay =>
            _lastMover != null && _rests.ContainsKey(_lastMover) && !_lastMover.IsStable.Value;

        internal void BeginCycle()
        {
            _cycle++;
        }

        /// <summary>Counts this cycle into the roamer's rest and returns its cycles at rest: 0 for one new to the board.</summary>
        internal int Stand(IWriteableDynamicSlotActor roamer)
        {
            var cycles = _rests.TryGetValue(roamer, out var known) ? known.Cycles + 1 : 0;
            _rests[roamer] = new Rest { Cycles = cycles, SeenOnCycle = _cycle };
            return cycles;
        }

        internal void EndCycle()
        {
            _gone.Clear();
            foreach (var (roamer, rest) in _rests)
            {
                if (rest.SeenOnCycle != _cycle)
                {
                    _gone.Add(roamer);
                }
            }

            for (var i = 0; i < _gone.Count; i++)
            {
                _rests.Remove(_gone[i]);
            }

            _gone.Clear();
        }

        internal int CyclesAtRest(IWriteableDynamicSlotActor roamer)
        {
            return _rests.TryGetValue(roamer, out var rest) ? rest.Cycles : 0;
        }

        internal void Moved(IWriteableDynamicSlotActor roamer)
        {
            _rests[roamer] = new Rest { Cycles = 0, SeenOnCycle = _cycle };
            _lastMover = roamer;
        }

        internal void Clear()
        {
            _rests.Clear();
            _lastMover = null;
            _cycle = 0;
        }

        private struct Rest
        {
            public int Cycles;
            public int SeenOnCycle;
        }
    }
}

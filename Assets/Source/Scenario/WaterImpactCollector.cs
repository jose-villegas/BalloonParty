using System.Collections.Generic;
using BalloonParty.Shared;
using VContainer.Unity;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     Holds this frame's impacts (every outward disturbance stamp: a ball's steps, pops, bombs, balloons gliding
    ///     in) for the water's ripples until <c>GroundView</c> reads them in its LateUpdate. <see cref="ImpactEventBus" />
    ///     clears in VContainer's LateTick, which runs before MonoBehaviour LateUpdate, so the view can't read the
    ///     bus itself; this copies it after Update, when the frame's fixed steps and updates have all reported.
    /// </summary>
    internal sealed class WaterImpactCollector : IPostTickable
    {
        // A burst past this many in one frame (a line of balloons gliding in at once) drops the rest: the water
        // only takes so many dips a frame anyway.
        private const int MaxImpacts = 64;

        private readonly ImpactEventBus _bus;
        private readonly List<ImpactEventBus.Impact> _impacts = new();

        internal IReadOnlyList<ImpactEventBus.Impact> Impacts => _impacts;

        internal WaterImpactCollector(ImpactEventBus bus)
        {
            _bus = bus;
        }

        void IPostTickable.PostTick()
        {
            var pending = _bus.Pending;
            for (var i = 0; i < pending.Count && _impacts.Count < MaxImpacts; i++)
            {
                _impacts.Add(pending[i]);
            }
        }

        internal void Clear()
        {
            _impacts.Clear();
        }
    }
}

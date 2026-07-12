using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     Lets a dynamic actor shape how the balancer moves it: a per-candidate weight offset added to the
    ///     support-based score (e.g. a tendency away from same-type neighbours), a per-rebalance step
    ///     allowance so heavier actors visibly rise slower, and an intervention order so faster actors win
    ///     contested slots each round.
    /// </summary>
    internal interface IBalanceInfluence
    {
        /// <summary>Max slots this actor moves per rebalance; 0 = unlimited.</summary>
        int MaxBalanceSteps { get; }

        /// <summary>Order this actor acts within each rebalance round; higher moves first (the race).</summary>
        int BalancePriority { get; }

        /// <summary>Animate a resolve straight to the final slot instead of touring every waypoint.</summary>
        bool DirectBalanceMotion { get; }

        /// <summary>Offset added to <paramref name="candidate" />'s balance weight; higher = preferred.</summary>
        int WeightBias(SlotGrid grid, Vector2Int candidate);
    }
}

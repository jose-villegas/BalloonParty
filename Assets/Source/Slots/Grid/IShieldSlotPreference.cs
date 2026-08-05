using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>
    ///     A balance-weight nudge that keeps shields somewhere a shot can still collect them.
    /// </summary>
    /// <remarks>
    ///     Lives here rather than in <c>Item/</c> so <see cref="MoveWeightEvaluator" /> depends on an
    ///     interface beside it instead of on the item layer — the arrow points the way the rest of
    ///     Slots does, and the sim's board mirror can leave it null.
    /// </remarks>
    internal interface IShieldSlotPreference
    {
        /// <summary>
        ///     Weight to add for moving <paramref name="actor" /> into <paramref name="candidate" />.
        ///     Zero for anything not carrying a shield.
        /// </summary>
        int WeightFor(ISlotActor actor, Vector2Int candidate);
    }
}

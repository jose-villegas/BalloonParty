using UnityEngine;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>How many of the unbreakables the formations called in a spawn line can bring.</summary>
    internal static class ReinforcementPlan
    {
        /// <summary>
        ///     The calls, held to the ceiling's room for more called-in unbreakables alive at once and to the
        ///     line's open places.
        /// </summary>
        internal static int ToAdd(int calls, int alive, int ceiling, int lineRoom)
        {
            return Mathf.Max(0, Mathf.Min(calls, Mathf.Min(ceiling - alive, lineRoom)));
        }
    }
}

using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Nudge
{
    public readonly struct NudgeMessage
    {
        /// <summary>Target actor. Null for shockwave (affects all nudgeable actors).</summary>
        public readonly IHasNudge Actor;

        public readonly Vector3 Origin;
        public readonly NudgeType Source;

        /// <summary>Publisher-side overrides (e.g. bomb settings). Null = use actor/global defaults.</summary>
        public readonly NudgeOverride[] Overrides;

        public NudgeMessage(
            IHasNudge actor,
            Vector3 origin,
            NudgeType source,
            NudgeOverride[] overrides = null)
        {
            Actor = actor;
            Origin = origin;
            Source = source;
            Overrides = overrides;
        }
    }
}

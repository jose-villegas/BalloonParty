using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Nudge
{
    public readonly struct BalloonNudgeMessage
    {
        /// <summary>Target balloon. Null for shockwave (all balloons).</summary>
        public readonly IBalloonModel Balloon;

        public readonly Vector3 Origin;

        public readonly NudgeType Source;

        /// <summary>Publisher-side overrides (e.g. bomb settings). Null = use balloon/global defaults.</summary>
        public readonly NudgeOverride[] Overrides;

        public BalloonNudgeMessage(
            IBalloonModel balloon,
            Vector3 origin,
            NudgeType source,
            NudgeOverride[] overrides = null)
        {
            Balloon = balloon;
            Origin = origin;
            Source = source;
            Overrides = overrides;
        }
    }
}

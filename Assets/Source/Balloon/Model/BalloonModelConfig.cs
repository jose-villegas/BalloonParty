using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Nudge;

namespace BalloonParty.Balloon.Model
{
    internal readonly struct BalloonModelConfig
    {
        public readonly BalloonType TypeName;
        public readonly int ScoreValue;
        public readonly bool CanHoldItem;
        public readonly int HitsToPop;
        public readonly IReadOnlyList<NudgeOverride> NudgeOverrides;

        public BalloonModelConfig(
            BalloonType typeName = default,
            int scoreValue = 1,
            bool canHoldItem = false,
            int hitsToPop = 1,
            NudgeOverride[] nudgeOverrides = null)
        {
            TypeName = typeName;
            ScoreValue = scoreValue;
            CanHoldItem = canHoldItem;
            HitsToPop = hitsToPop;
            NudgeOverrides = nudgeOverrides;
        }

        internal static BalloonModelConfig From(BalloonPrefabEntry entry)
        {
            return new BalloonModelConfig(entry.BalloonType,
                entry.ScoreValue,
                entry.CanHoldItem,
                entry.HitsToPop,
                entry.NudgeOverrides);
        }
    }
}

using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Balloon.Model
{
    internal readonly struct BalloonModelConfig
    {
        public readonly BalloonType TypeName;
        public readonly int ScoreValue;
        public readonly int HitsToPop;
        public readonly IReadOnlyList<NudgeOverride> NudgeOverrides;
        public readonly float ItemActivationWeight;
        public readonly float Spillover;

        public BalloonModelConfig(
            BalloonType typeName = default,
            int scoreValue = 1,
            int hitsToPop = 1,
            NudgeOverride[] nudgeOverrides = null,
            float itemActivationWeight = 1f,
            float spillover = 0f)
        {
            TypeName = typeName;
            ScoreValue = scoreValue;
            HitsToPop = hitsToPop;
            NudgeOverrides = nudgeOverrides;
            ItemActivationWeight = itemActivationWeight;
            Spillover = spillover;
        }

        internal static BalloonModelConfig From(BalloonPrefabEntry entry)
        {
            return new BalloonModelConfig(entry.BalloonType,
                entry.ScoreValue,
                entry.HitsToPop,
                entry.NudgeOverrides,
                entry.ItemActivationWeight,
                entry.Spillover);
        }
    }
}

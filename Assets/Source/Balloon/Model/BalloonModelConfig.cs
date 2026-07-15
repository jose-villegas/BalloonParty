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
        public readonly float BalanceBias;
        public readonly int MaxBalanceSteps;
        public readonly int BalancePriority;
        public readonly float DeflectStampScale;
        public readonly bool DirectBalanceMotion;
        public readonly bool OmnidirectionalBalance;

        // Value constructor — mainly for tests, which build a config without a prefab entry.
        public BalloonModelConfig(
            BalloonType typeName = default,
            int scoreValue = 1,
            int hitsToPop = 1,
            NudgeOverride[] nudgeOverrides = null,
            float itemActivationWeight = 1f,
            float balanceBias = 0f,
            int maxBalanceSteps = 0,
            int balancePriority = 0,
            float deflectStampScale = 0f,
            bool directBalanceMotion = false,
            bool omnidirectionalBalance = false)
        {
            TypeName = typeName;
            ScoreValue = scoreValue;
            HitsToPop = hitsToPop;
            NudgeOverrides = nudgeOverrides;
            ItemActivationWeight = itemActivationWeight;
            BalanceBias = balanceBias;
            MaxBalanceSteps = maxBalanceSteps;
            BalancePriority = balancePriority;
            DeflectStampScale = deflectStampScale;
            DirectBalanceMotion = directBalanceMotion;
            OmnidirectionalBalance = omnidirectionalBalance;
        }

        internal BalloonModelConfig(BalloonPrefabEntry entry)
        {
            TypeName = entry.BalloonType;
            ScoreValue = entry.ScoreValue;
            HitsToPop = entry.HitsToPop;
            NudgeOverrides = entry.NudgeOverrides;
            ItemActivationWeight = entry.ItemActivationWeight;
            BalanceBias = entry.BalanceBias;
            MaxBalanceSteps = entry.MaxBalanceSteps;
            BalancePriority = entry.BalancePriority;
            DeflectStampScale = entry.DeflectStampScale;
            DirectBalanceMotion = entry.DirectBalanceMotion;
            OmnidirectionalBalance = entry.OmnidirectionalBalance;
        }
    }
}

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
        public readonly float SeparationBias;
        public readonly int MaxBalanceSteps;
        public readonly int BalancePriority;

        // Value constructor — mainly for tests, which build a config without a prefab entry.
        public BalloonModelConfig(
            BalloonType typeName = default,
            int scoreValue = 1,
            int hitsToPop = 1,
            NudgeOverride[] nudgeOverrides = null,
            float itemActivationWeight = 1f,
            float separationBias = 0f,
            int maxBalanceSteps = 0,
            int balancePriority = 0)
        {
            TypeName = typeName;
            ScoreValue = scoreValue;
            HitsToPop = hitsToPop;
            NudgeOverrides = nudgeOverrides;
            ItemActivationWeight = itemActivationWeight;
            SeparationBias = separationBias;
            MaxBalanceSteps = maxBalanceSteps;
            BalancePriority = balancePriority;
        }

        internal BalloonModelConfig(BalloonPrefabEntry entry)
        {
            TypeName = entry.BalloonType;
            ScoreValue = entry.ScoreValue;
            HitsToPop = entry.HitsToPop;
            NudgeOverrides = entry.NudgeOverrides;
            ItemActivationWeight = entry.ItemActivationWeight;
            SeparationBias = entry.SeparationBias;
            MaxBalanceSteps = entry.MaxBalanceSteps;
            BalancePriority = entry.BalancePriority;
        }
    }
}

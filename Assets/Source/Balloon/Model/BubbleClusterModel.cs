using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    /// Model for <c>BalloonType.BubbleCluster</c>.
    /// Tracks only durability (bubble count) — the cluster carries no item,
    /// contributes no score, and has no palette colour.
    /// </summary>
    internal class BubbleClusterModel : BalloonModelBase, IHasDurability
    {
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal BubbleClusterModel(BalloonModelConfig config) : base(config)
        {
            NudgeOverrides = config.NudgeOverrides;
        }
    }
}

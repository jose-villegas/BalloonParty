using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    internal class BalloonModel : BalloonModelBase, IHasWriteableColor, IHasWriteableItemSlot, IHasDurability, IHasScore
    {
        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);
        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<string> IHasColor.Color => Color;
        IReadOnlyReactiveProperty<ItemType> IHasItemSlot.Item => Item;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal BalloonModel() : this(new BalloonModelConfig()) { }

        internal BalloonModel(BalloonModelConfig config) : base(config)
        {
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }
    }
}

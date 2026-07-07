using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Balloon.Model
{
    internal class BalloonModel : BalloonModelBase, IPaintable, IHasWriteableItemSlot, IHasWriteableRainbowMode,
        IHasDurability, IHasScore, IHasScoreColor
    {
        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);
        public ReactiveProperty<bool> IsRainbow { get; } = new(false);
        public int ScoreValue { get; }
        public float ItemActivationWeight { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<string> IHasColor.Color => Color;
        IReadOnlyReactiveProperty<ItemType> IHasItemSlot.Item => Item;
        IReadOnlyReactiveProperty<bool> IHasRainbowMode.IsRainbow => IsRainbow;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal BalloonModel() : this(new BalloonModelConfig()) { }

        internal BalloonModel(BalloonModelConfig config) : base(config)
        {
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
            ItemActivationWeight = config.ItemActivationWeight;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            if (HitsRemaining.Value > 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Color.Value))
            {
                results.Add(new ScoreAttribution(Color.Value, ScoreValue));
            }
        }
    }
}

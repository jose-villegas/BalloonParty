using BalloonParty.Configuration;
using BalloonParty.Slots.Capabilities;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    internal class BalloonModel : BalloonModelBase, IHasWriteableColor, IHasWriteableItemSlot
    {
        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);

        IReadOnlyReactiveProperty<string> IHasColor.Color => Color;
        IReadOnlyReactiveProperty<ItemType> IHasItemSlot.Item => Item;

        internal BalloonModel() : this(new BalloonModelConfig()) { }

        internal BalloonModel(BalloonModelConfig config) : base(config) { }
    }
}

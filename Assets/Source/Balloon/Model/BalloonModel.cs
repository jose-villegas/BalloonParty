using BalloonParty.Slots;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    internal class BalloonModel : BalloonModelBase, IHasWriteableColor
    {
        public ReactiveProperty<string> Color { get; } = new();

        IReadOnlyReactiveProperty<string> IHasColor.Color => Color;

        internal BalloonModel() : this(new BalloonModelConfig(canHoldItem: true)) { }

        internal BalloonModel(BalloonModelConfig config) : base(config) { }
    }
}

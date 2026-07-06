using BalloonParty.Configuration;
using UniRx;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Slots.Capabilities
{
    /// <summary>Extends <see cref="IHasColor"/> because item visuals are always tinted to the host's color.</summary>
    public interface IHasItemSlot : IHasColor
    {
        IReadOnlyReactiveProperty<ItemType> Item { get; }
    }
}

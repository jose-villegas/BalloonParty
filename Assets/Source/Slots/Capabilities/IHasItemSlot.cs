using BalloonParty.Configuration;
using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    /// <summary>
    /// Declares that an actor can host an item. Extends <see cref="IHasColor"/> because item
    /// visuals are always tinted to the host's color — a colorless item host is incoherent.
    /// NOTE: In Phase 2 this will extend IHasScoreColor (reactive mask) instead of IHasColor.
    /// See PLAN-ColorScoreAttribution.md § 2e.
    /// </summary>
    public interface IHasItemSlot : IHasColor
    {
        IReadOnlyReactiveProperty<ItemType> Item { get; }
    }
}

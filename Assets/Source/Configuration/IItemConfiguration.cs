using System.Collections.Generic;

namespace BalloonParty.Configuration
{
    public interface IItemConfiguration
    {
        IReadOnlyList<ItemSettings> Items { get; }
        ItemSettings this[ItemType type] { get; }
    }
}

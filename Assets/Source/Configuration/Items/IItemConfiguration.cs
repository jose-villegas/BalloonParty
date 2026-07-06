using System.Collections.Generic;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Configuration.Items
{
    public interface IItemConfiguration
    {
        IReadOnlyList<ItemSettings> Items { get; }
        ItemSettings this[ItemType type] { get; }
    }
}

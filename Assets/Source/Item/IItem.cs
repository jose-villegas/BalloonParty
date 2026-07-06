using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item
{
    public interface IItem
    {
        ItemType Type { get; }
    }
}

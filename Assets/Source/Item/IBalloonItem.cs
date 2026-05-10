using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;

namespace BalloonParty.Item
{
    public interface IBalloonItem : IItem
    {
        ItemType Type { get; }
        void Setup(IBalloonModel balloon);
    }
}

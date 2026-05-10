using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;

namespace BalloonParty.Balloon.Items
{
    public interface IBalloonItem : IItem
    {
        ItemType Type { get; }
        void Setup(IBalloonModel balloon);
    }
}


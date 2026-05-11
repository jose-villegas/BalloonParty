using BalloonParty.Balloon.Model;

namespace BalloonParty.Item
{
    public interface IBalloonItem : IItem
    {
        void Setup(IBalloonModel balloon);
    }
}

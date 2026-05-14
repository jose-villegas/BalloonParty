using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Type
{
    public interface IBalloonVariant
    {
        BalloonType TypeName { get; }
        void Initialize(IWriteableBalloonModel model);
    }
}

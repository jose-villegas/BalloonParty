using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Type
{
    public interface IBalloonTypeConfiguration
    {
        BalloonType TypeName { get; }
        int HitsToPop { get; }
        void Initialize(IWriteableBalloonModel model);
    }
}

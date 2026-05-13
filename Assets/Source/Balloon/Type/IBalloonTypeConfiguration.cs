namespace BalloonParty.Balloon.Type
{
    public interface IBalloonTypeConfiguration
    {
        string TypeName { get; }
        int HitsToPop { get; }
    }
}

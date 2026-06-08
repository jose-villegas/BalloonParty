namespace BalloonParty.Configuration
{
    public interface IGameDisplayConfiguration
    {
        float ReferenceWorldWidth { get; }
        float ReferenceWorldHeight { get; }
        float GetOrthogonalSize();
    }
}

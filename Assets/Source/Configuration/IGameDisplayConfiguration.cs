namespace BalloonParty.Configuration
{
    public interface IGameDisplayConfiguration
    {
        float ReferenceWorldWidth { get; }
        float ReferenceWorldHeight { get; }
        int SceneCaptureDownscale { get; }
        int SceneCaptureFrameInterval { get; }
        float GetOrthogonalSize();
    }
}

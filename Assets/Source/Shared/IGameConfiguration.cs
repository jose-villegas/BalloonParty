using DG.Tweening;

namespace BalloonParty.Shared
{
    public interface IGameConfiguration : IRunConfig, IProjectileFlightConfig, ISlotGridConfig,
        IPredictionTraceConfig, IScoreTrailConfig
    {
        float ProjectileDisappearDuration { get; }
        Ease ProjectileDisappearEase { get; }
        float ProjectileDeadDriftFactor { get; }
    }
}

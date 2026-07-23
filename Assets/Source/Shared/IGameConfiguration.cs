using DG.Tweening;

namespace BalloonParty.Shared
{
    public interface IGameConfiguration : IProjectileFlightConfig, ISlotGridConfig,
        IPredictionTraceConfig, IScoreTrailConfig
    {
        int StartingHitPoints { get; }
        float ProjectileLoadDuration { get; }
        float ProjectileDisappearDuration { get; }
        Ease ProjectileDisappearEase { get; }
        float ProjectileDeadDriftFactor { get; }
    }
}

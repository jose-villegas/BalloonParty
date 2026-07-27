using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    /// <summary>Read-only view of the motion resolver's per-shot bookkeeping — observable (e.g. by
    /// tooling or feedback) without the ability to mutate it. The writeable
    /// <see cref="ProjectileFlightState" /> is exposed only on <see cref="IWriteableProjectileModel" />.</summary>
    public interface IProjectileFlightState
    {
        int BankedPierceCharges { get; }
        int BankedRainbowPierceCharges { get; }
        int ConsecutiveWallBounces { get; }
        int TotalCruiseTaps { get; }
        SpeedTransitionKind TransitionKind { get; }
        float TransitionElapsed { get; }
        float CurrentSpeed { get; }
        int TotalSweeps { get; }
        int SegmentPopCount { get; }
        bool SegmentSweepValid { get; }
        Vector3 LastBouncePosition { get; }
        Vector3 SegmentStartPosition { get; }
        float SegmentElapsed { get; }
    }
}

using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    /// <summary>Read-only view of the motion resolver's per-shot bookkeeping — observable (e.g. by
    /// tooling or feedback) without the ability to mutate it. The writeable
    /// <see cref="ProjectileFlightState" /> is exposed only on <see cref="IWriteableProjectileModel" />.</summary>
    public interface IProjectileFlightState
    {
        // Toughs the piercing shot has plowed and not yet shattered: above zero, its next wall discharges.
        int PendingPierceHitCount { get; }
        int BankedPierceCharges { get; }
        int BankedRainbowPierceCharges { get; }

        // Meadow mushroom caps the shot has cut as a lance, each of which banked it a charge.
        int CapsCut { get; }
        int ConsecutiveWallBounces { get; }
        int TotalCruiseTaps { get; }
        SpeedTransitionKind TransitionKind { get; }
        float TransitionElapsed { get; }
        float CurrentSpeed { get; }
        int ConsecutiveSweeps { get; }
        int SegmentPopCount { get; }
        bool SegmentSweepValid { get; }
        Vector3 LastBouncePosition { get; }
        Vector3 SegmentStartPosition { get; }
        float SegmentElapsed { get; }

        /// <summary>Whether the shot plowed <paramref name="balloon" /> and hasn't discharged it yet.</summary>
        bool HasPendingPierceHit(IBalloonModel balloon);
    }
}

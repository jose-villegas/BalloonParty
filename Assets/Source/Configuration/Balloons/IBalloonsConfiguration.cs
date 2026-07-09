using System.Collections.Generic;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Configuration.Balloons
{
    public interface IBalloonsConfiguration
    {
        /// <summary>The full catalog, incl. types gated out of the active level — see IActiveLevelParameters.</summary>
        IReadOnlyList<BalloonPrefabEntry> Entries { get; }
        ParticleSystem DefaultPopVfxPrefab { get; }

        /// <summary>Swapped onto a balloon's body renderer while it's in rainbow mode — see BalloonView.</summary>
        Material RainbowMaterial { get; }

        float NewBalloonLinesTimeInterval { get; }
        Vector2 BalloonSpawnAnimationDurationRange { get; }
        int SpawnEntryRowOffset { get; }
        float TimeForBalloonsBalance { get; }

        /// <summary>Interval between board re-balances while a projectile is in flight. 0 disables.</summary>
        float FlightRebalanceInterval { get; }
        float NudgeDistance { get; }
        float NudgeDuration { get; }
        float NudgeFalloff { get; }
    }
}

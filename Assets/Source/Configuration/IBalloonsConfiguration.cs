using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    public interface IBalloonsConfiguration
    {
        IReadOnlyList<BalloonPrefabEntry> Entries { get; }
        ParticleSystem DefaultPopVfxPrefab { get; }
        int GameStartedBalloonLines { get; }
        int NewProjectileBalloonLines { get; }
        float NewBalloonLinesTimeInterval { get; }
        Vector2 BalloonSpawnAnimationDurationRange { get; }
        int SpawnEntryRowOffset { get; }
        float TimeForBalloonsBalance { get; }
        float NudgeDistance { get; }
        float NudgeDuration { get; }
        float NudgeFalloff { get; }
    }
}


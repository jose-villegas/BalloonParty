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

        /// <summary>Catalog default — runtime reads the resolved value via IActiveLevelParameters.BoardLines.</summary>
        int GameStartedBalloonLines { get; }

        /// <summary>Catalog default — runtime reads the resolved value via IActiveLevelParameters.SpawnLines.</summary>
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

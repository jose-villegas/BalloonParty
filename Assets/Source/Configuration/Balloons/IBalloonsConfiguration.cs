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

        /// <summary>World-space radius of the neutral flash light emitted when a rainbow balloon pops. 0 = no flash.</summary>
        float RainbowPopFlashRadius { get; }

        /// <summary>Peak intensity of the rainbow-pop flash light.</summary>
        float RainbowPopFlashIntensity { get; }

        /// <summary>Seconds the rainbow-pop flash is held before it cuts. 0 = no flash.</summary>
        float RainbowPopFlashSeconds { get; }

        float NewBalloonLinesTimeInterval { get; }

        /// <summary>Fallback spawn/balance travel speed (world units/sec) for entries whose own
        /// <see cref="BalloonPrefabEntry.MoveSpeed" /> is 0. Movement duration is distance ÷ speed, so
        /// every balloon travels at a constant speed regardless of how far it must go.</summary>
        float DefaultBalloonMoveSpeed { get; }

        /// <summary>Fractional ± spread applied once per balloon at spawn to its resolved move speed, so a
        /// wave of identical balloons drifts in at slightly different paces. 0 = every balloon identical.</summary>
        float MoveSpeedVariation { get; }
        int SpawnEntryRowOffset { get; }

        /// <summary>Vertical segment height (in lines) for initial-fill heavy layering; 0/1 disables it.</summary>
        int ToughLayerSpacing { get; }
        float TimeForBalloonsBalance { get; }

        /// <summary>Interval between board re-balances while a projectile is in flight. 0 disables.</summary>
        float FlightRebalanceInterval { get; }

        /// <summary>Chance a direct projectile pop spawns one extra balloon (flushed on the flight pulse); 0 = off.</summary>
        float PopSpawnChance { get; }

        /// <summary>Rows the automated pop-spawns leave unfilled — caps them at grid capacity minus columns × this.</summary>
        int PopSpawnFreeRows { get; }
        float NudgeDistance { get; }
        float NudgeDuration { get; }
        float NudgeFalloff { get; }
    }
}

using System.Collections.Generic;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Configuration.Balloons
{
    [CreateAssetMenu(menuName = "Configuration/Balloons Configuration", fileName = "BalloonsConfiguration")]
    public class BalloonsConfiguration : ScriptableObject, IBalloonsConfiguration
    {
        [SerializeField] private BalloonPrefabEntry[] _entries;

        [Header("Pop VFX")]
        [Tooltip("Default pop particle used for colored balloons. Tinted to the balloon's palette color at runtime.")]
        [SerializeField] private ParticleSystem _defaultPopVfxPrefab;

        [Header("Rainbow")]
        [Tooltip("Swapped onto a balloon's body renderer while it's in rainbow mode.")]
        [SerializeField] private Material _rainbowMaterial;

        [Tooltip("World-space radius of the neutral flash light emitted when a rainbow balloon pops. 0 = no flash.")]
        [SerializeField] [Min(0f)] private float _rainbowPopFlashRadius = 1.5f;

        [Tooltip("Peak intensity of the rainbow-pop flash light.")]
        [SerializeField] [Min(0f)] private float _rainbowPopFlashIntensity = 3f;

        [Tooltip("Seconds the rainbow-pop flash is held before it cuts. 0 = no flash.")]
        [SerializeField] [Min(0f)] private float _rainbowPopFlashSeconds = 0.2f;

        [Header("Spawning")]
        [SerializeField] private float _newBalloonLinesTimeInterval;

        [Tooltip("Fallback travel speed (world units/sec) for balloons whose per-type Move Speed is 0. " +
                 "Movement duration is distance ÷ speed, so every balloon moves at a constant speed " +
                 "instead of a fixed duration — no distance-driven speed jump on spawn.")]
        [SerializeField] [Min(0.01f)] private float _defaultBalloonMoveSpeed = 8f;

        [Tooltip("Per-balloon ± speed spread rolled once at spawn (0.15 = ±15%) so a wave of identical " +
                 "balloons drifts in at slightly different paces instead of in lockstep. 0 = uniform.")]
        [SerializeField] [Range(0f, 1f)] private float _moveSpeedVariation = 0.15f;
        [Tooltip(
            "How many rows below the target slot the balloon enters from. Can exceed the grid bounds — the world position is still computed correctly.")]
        [SerializeField] private int _spawnEntryRowOffset = 4;

        [Tooltip("Initial fill divides the board into vertical segments this many lines tall; the bottom " +
                 "line of each segment becomes a heavy (positive spawn-weight) layer, with lighter types " +
                 "filling the rest. 0 or 1 disables layering — heavy types pool at the bottom as one gradient.")]
        [SerializeField] private int _toughLayerSpacing = 3;

        [Header("Balancing")]
        [SerializeField] private float _timeForBalloonsBalance;

        [Tooltip("Seconds between board re-balances while a projectile is in flight — keeps the stack " +
                 "settling at intervals so a projectile looping wall-to-wall eventually finds a target. " +
                 "0 disables.")]
        [SerializeField] private float _flightRebalanceInterval = 1f;

        [Tooltip("Chance a DIRECT projectile pop (AOE items excluded) spawns one extra balloon, rolled " +
                 "per pop; spawns flush on the flight pulse. 0 = off.")]
        [Range(0f, 1f)]
        [SerializeField] private float _popSpawnChance;

        [Tooltip("Rows the automated pop-spawns never aim to fill: they stop at grid capacity minus " +
                 "columns × this. 0 = may fill the whole grid.")]
        [SerializeField] private int _popSpawnFreeRows = 2;

        [Header("Nudge")]
        [SerializeField] private float _nudgeDistance = 0.3f;
        [SerializeField] private float _nudgeDuration = 0.15f;
        [SerializeField] private float _nudgeFalloff = 1.5f;

        public IReadOnlyList<BalloonPrefabEntry> Entries => _entries;
        public ParticleSystem DefaultPopVfxPrefab => _defaultPopVfxPrefab;
        public Material RainbowMaterial => _rainbowMaterial;
        public float RainbowPopFlashRadius => _rainbowPopFlashRadius;
        public float RainbowPopFlashIntensity => _rainbowPopFlashIntensity;
        public float RainbowPopFlashSeconds => _rainbowPopFlashSeconds;
        public float NewBalloonLinesTimeInterval => _newBalloonLinesTimeInterval;
        public float DefaultBalloonMoveSpeed => _defaultBalloonMoveSpeed;
        public float MoveSpeedVariation => _moveSpeedVariation;
        public int SpawnEntryRowOffset => _spawnEntryRowOffset;
        public int ToughLayerSpacing => _toughLayerSpacing;
        public float TimeForBalloonsBalance => _timeForBalloonsBalance;
        public float FlightRebalanceInterval => _flightRebalanceInterval;
        public float PopSpawnChance => _popSpawnChance;
        public int PopSpawnFreeRows => _popSpawnFreeRows;
        public float NudgeDistance => _nudgeDistance;
        public float NudgeDuration => _nudgeDuration;
        public float NudgeFalloff => _nudgeFalloff;
    }
}

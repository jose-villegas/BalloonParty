using System;
using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>How the field fills and drains — the testing spawn-all toggle, initial count, per-source spawn
    /// profiles, and the reduction curve.</summary>
    internal interface ISpeckSpawnSettings
    {
        bool SpawnAllImmediately { get; }
        int InitialActiveCount { get; }
        IReadOnlyList<SpeckProfile> SpeckProfiles { get; }
        AnimationCurve ReductionCurve { get; }
    }

    [Serializable]
    internal class SpeckSpawnSettings : ISpeckSpawnSettings
    {
        [Tooltip("Testing toggle: fill the field to Count immediately and hold it there — no spawn-driven " +
                 "build-up and no reduction drain (the pre-activation behaviour). Leave off for gameplay.")]
        [SerializeField] private bool _spawnAllImmediately;

        [Tooltip("Specks active at the start. Count is the cap; each spawn enables more up to the current " +
                 "ceiling. 0 = the field starts empty and builds entirely from spawns.")]
        [SerializeField] private int _initialActiveCount;

        [Tooltip("Spawn presets keyed by source — the speck analogue of disturbance StampProfiles. A balloon " +
                 "pop uses the BalloonPop profile; other callers request a spawn by source via " +
                 "SpeckSpawnRequestMessage.")]
        [SerializeField] private SpeckProfile[] _speckProfiles =
        {
            new() { Sources = SpeckSource.BalloonPop, Count = 16, Spread = 0.1f },
            new() { Sources = SpeckSource.UnbreakableBurst, Count = 24, Spread = 0.35f },
            // CruiseVelocityCurve is left unauthored (empty) — until it's authored in the Inspector, every
            // cruise bounce spawns the base Count (4) unscaled (×1). See SpeckProfile.CruiseVelocityCurve.
            new() { Sources = SpeckSource.ProjectileCruise, Count = 4, Spread = 0.15f },
        };

        [Tooltip("The active ceiling follows this curve: X = seconds since the last burst (its last key is " +
                 "the effective duration; the ceiling holds past it), Y = fraction of Count allowed active " +
                 "(1 = full). Each burst restarts the curve at 0; between bursts the falling ceiling thins " +
                 "the field, so rapid pops read as chaos.")]
        [SerializeField] private AnimationCurve _reductionCurve = AnimationCurve.Linear(0f, 1f, 3f, 0.3f);

        public bool SpawnAllImmediately => _spawnAllImmediately;
        public int InitialActiveCount => _initialActiveCount;
        public IReadOnlyList<SpeckProfile> SpeckProfiles => _speckProfiles;
        public AnimationCurve ReductionCurve => _reductionCurve;
    }
}

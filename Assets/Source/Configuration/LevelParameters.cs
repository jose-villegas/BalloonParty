using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     The resolved, plain form of a level's difficulty mix — what
    ///     <c>LevelDifficultyResolver</c> caches per level and exposes via
    ///     <c>IActiveLevelParameters</c>. Also the form a custom (exact-level) entry authors
    ///     directly, since a single level has no min/max to resolve. Field initializers are a
    ///     minimal, always-valid baseline (Simple-only, one line per turn) — not a copy of any
    ///     specific catalog asset, since customs/ranges are expected to author their own mix.
    /// </summary>
    [Serializable]
    public class LevelParameters
    {
        [SerializeField] private int _spawnLines = 1;
        [SerializeField] private int _boardLines = 5;

        [SerializeField] private BalloonTypeWeight[] _balloonWeights =
        {
            new(BalloonType.Simple, 1f),
        };

        [SerializeField] private ItemTypeWeight[] _itemWeights = Array.Empty<ItemTypeWeight>();

        [SerializeField] private ResolvedGridActorGate[] _gridActorGates =
        {
            new(GridActorType.Puff, 4),
        };

        [Tooltip("All bits set (default) = every palette color allowed.")]
        [SerializeField] [PaletteColorMask] private int _allowedColorsMask = ~0;

        public LevelParameters()
        {
        }

        internal LevelParameters(
            int spawnLines, int boardLines, BalloonTypeWeight[] balloonWeights,
            ItemTypeWeight[] itemWeights, ResolvedGridActorGate[] gridActorGates, int allowedColorsMask)
        {
            _spawnLines = spawnLines;
            _boardLines = boardLines;
            _balloonWeights = balloonWeights;
            _itemWeights = itemWeights;
            _gridActorGates = gridActorGates;
            _allowedColorsMask = allowedColorsMask;
        }

        public int SpawnLines => _spawnLines;
        public int BoardLines => _boardLines;
        public BalloonTypeWeight[] BalloonWeights => _balloonWeights;
        public ItemTypeWeight[] ItemWeights => _itemWeights;
        public ResolvedGridActorGate[] GridActorGates => _gridActorGates;

        /// <summary>Same bit-per-color convention as <see cref="ColorableBalloonVariant" />'s mask — all bits set = every palette color.</summary>
        public int AllowedColorsMask => _allowedColorsMask;
    }
}

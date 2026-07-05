using System;
using BalloonParty.Balloon.Type;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     The range-authored form of a level's difficulty mix: scalars as <see cref="RangedInt" />
    ///     (fixed/linear/random), weighted sets static (no mode — the per-spawn weighted draw is
    ///     already the randomness for the mix). <see cref="Resolve" /> is a pure function of the
    ///     level position, so it's EditMode-testable with a seeded rng.
    /// </summary>
    [Serializable]
    public class RangedLevelParameters
    {
        [SerializeField] private RangedInt _spawnLines = new(1, 1);
        [SerializeField] private RangedInt _boardLines = new(5, 5);

        [SerializeField] private BalloonTypeWeight[] _balloonWeights =
        {
            new(BalloonType.Simple, 1f),
        };

        [SerializeField] private ItemTypeWeight[] _itemWeights = Array.Empty<ItemTypeWeight>();

        [Tooltip("All bits set (default) = every palette color allowed.")]
        [SerializeField] [PaletteColorMask] private int _allowedColorsMask = ~0;

        public RangedInt SpawnLines => _spawnLines;
        public RangedInt BoardLines => _boardLines;
        public BalloonTypeWeight[] BalloonWeights => _balloonWeights;
        public ItemTypeWeight[] ItemWeights => _itemWeights;
        public int AllowedColorsMask => _allowedColorsMask;

        public LevelParameters Resolve(float positionInRange, System.Random rng)
        {
            return new LevelParameters(
                _spawnLines.Resolve(positionInRange, rng),
                _boardLines.Resolve(positionInRange, rng),
                _balloonWeights,
                _itemWeights,
                _allowedColorsMask);
        }
    }
}

using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     A level's difficulty mix before its ranges are rolled: what <see cref="LevelGenerator" /> outputs
    ///     for every positive level, and what the authored debug presets hold. <see cref="Resolve" /> is
    ///     pure, so it's EditMode-testable with a seeded rng.
    /// </summary>
    [Serializable]
    public class RangedLevelParameters : IPaletteColorMasked
    {
        [SerializeField] private int _boardLines = 5;

        [Tooltip("How often, in spawn waves, an item-drop opportunity happens. 0 = never.")]
        [SerializeField] private RangedInt _itemCadence = new(5, 5);

        [Tooltip("Weighted distribution for how many items to seed on the initial board fill (level " +
                 "start / transition), rolled once. X = item count (0, 1, 2…), Y = weight. Empty = none.")]
        [SerializeField] private AnimationCurve _initialItemCountWeights = new();

        [Tooltip("Weighted distribution for how many items to grant on each cadence wave, rolled per " +
                 "wave. X = item count (0, 1, 2…), Y = weight. Capped by the eligible new balloons.")]
        [SerializeField] private AnimationCurve _itemCountWeights =
            new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

        [SerializeField] private BalloonTypeWeight[] _balloonWeights =
        {
            new(BalloonType.Simple, 1f),
        };

        [SerializeField] private ItemTypeWeight[] _itemWeights = Array.Empty<ItemTypeWeight>();

        [SerializeField] private GridActorTypeGate[] _gridActorGates =
        {
            new(GridActorType.Puff, new RangedInt(3, 6, RangeMode.Random), maxPerCluster: 3),
        };

        [Tooltip("All bits set (default) = every palette color allowed.")]
        [SerializeField] [PaletteColorMask] private int _allowedColorsMask = ~0;

        [Tooltip("The time between spawn lines, as a share of the clock's usual: 0.5 = lines twice as often.")]
        [SerializeField] [Range(0.1f, 1f)] private float _spawnIntervalScale = 1f;

        public int BoardLines => _boardLines;
        public RangedInt ItemCadence => _itemCadence;

        // Curves pass through Resolve unchanged — sampled per wave at runtime.
        public AnimationCurve InitialItemCountWeights => _initialItemCountWeights;
        public AnimationCurve ItemCountWeights => _itemCountWeights;
        public BalloonTypeWeight[] BalloonWeights => _balloonWeights;
        public ItemTypeWeight[] ItemWeights => _itemWeights;
        public GridActorTypeGate[] GridActorGates => _gridActorGates;
        public int AllowedColorsMask => _allowedColorsMask;
        public float SpawnIntervalScale => _spawnIntervalScale;

        // IPaletteColorMasked: a range maps its difficulty mix to the level's allowed palette colours.
        int IPaletteColorMasked.ColorMask => _allowedColorsMask;

        public RangedLevelParameters()
        {
        }

        internal RangedLevelParameters(int boardLines, RangedInt itemCadence, AnimationCurve initialItemCountWeights,
            AnimationCurve itemCountWeights, BalloonTypeWeight[] balloonWeights, ItemTypeWeight[] itemWeights,
            GridActorTypeGate[] gridActorGates, int allowedColorsMask, float spawnIntervalScale = 1f)
        {
            _spawnIntervalScale = spawnIntervalScale;
            _boardLines = boardLines;
            _itemCadence = itemCadence;
            _initialItemCountWeights = initialItemCountWeights ?? new AnimationCurve();
            _itemCountWeights = itemCountWeights ?? new AnimationCurve();
            _balloonWeights = balloonWeights;
            _itemWeights = itemWeights;
            _gridActorGates = gridActorGates;
            _allowedColorsMask = allowedColorsMask;
        }

        internal LevelParameters Resolve(float positionInRange, System.Random rng)
        {
            return new LevelParameters(
                _boardLines,
                _itemCadence.Resolve(positionInRange, rng),
                _initialItemCountWeights,
                _itemCountWeights,
                _balloonWeights,
                _itemWeights,
                ResolveGridActorGates(positionInRange, rng),
                _allowedColorsMask,
                _spawnIntervalScale);
        }

        private ResolvedGridActorGate[] ResolveGridActorGates(float positionInRange, System.Random rng)
        {
            var resolved = new ResolvedGridActorGate[_gridActorGates.Length];
            for (var i = 0; i < _gridActorGates.Length; i++)
            {
                var gate = _gridActorGates[i];
                resolved[i] = new ResolvedGridActorGate(
                    gate.Type, gate.Count.Resolve(positionInRange, rng), gate.MaxPerCluster);
            }

            return resolved;
        }
    }
}

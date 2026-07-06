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

        [Tooltip("How often (in turns) an item-drop opportunity happens. 0 = never.")]
        [SerializeField] private RangedInt _itemCadence = new(5, 5);

        [Tooltip("Weighted distribution for how many items to seed on the initial board fill (level " +
                 "start / transition), rolled once. X = item count (0, 1, 2…), Y = weight. Empty = none.")]
        [SerializeField] private AnimationCurve _initialItemCountWeights = new();

        [Tooltip("Weighted distribution for how many items to grant on each cadence turn, rolled per " +
                 "turn. X = item count (0, 1, 2…), Y = weight. Capped by the eligible new balloons.")]
        [SerializeField] private AnimationCurve _itemCountWeights =
            new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

        [SerializeField] private BalloonTypeWeight[] _balloonWeights =
        {
            new(BalloonType.Simple, 1f),
        };

        [SerializeField] private ItemTypeWeight[] _itemWeights = Array.Empty<ItemTypeWeight>();

        [SerializeField] private GridActorTypeGate[] _gridActorGates =
        {
            new(GridActorType.Puff, new RangedInt(3, 6, RangeMode.Random)),
        };

        [Tooltip("All bits set (default) = every palette color allowed.")]
        [SerializeField] [PaletteColorMask] private int _allowedColorsMask = ~0;

        public RangedInt SpawnLines => _spawnLines;
        public RangedInt BoardLines => _boardLines;
        public RangedInt ItemCadence => _itemCadence;

        // Curves are the distribution itself — sampled per turn at runtime, so no per-level range
        // resolution (like the weighted type sets, they pass through Resolve unchanged).
        public AnimationCurve InitialItemCountWeights => _initialItemCountWeights;
        public AnimationCurve ItemCountWeights => _itemCountWeights;
        public BalloonTypeWeight[] BalloonWeights => _balloonWeights;
        public ItemTypeWeight[] ItemWeights => _itemWeights;
        public GridActorTypeGate[] GridActorGates => _gridActorGates;
        public int AllowedColorsMask => _allowedColorsMask;

        internal LevelParameters Resolve(float positionInRange, System.Random rng)
        {
            return new LevelParameters(
                _spawnLines.Resolve(positionInRange, rng),
                _boardLines.Resolve(positionInRange, rng),
                _itemCadence.Resolve(positionInRange, rng),
                _initialItemCountWeights,
                _itemCountWeights,
                _balloonWeights,
                _itemWeights,
                ResolveGridActorGates(positionInRange, rng),
                _allowedColorsMask);
        }

        private ResolvedGridActorGate[] ResolveGridActorGates(float positionInRange, System.Random rng)
        {
            var resolved = new ResolvedGridActorGate[_gridActorGates.Length];
            for (var i = 0; i < _gridActorGates.Length; i++)
            {
                var gate = _gridActorGates[i];
                resolved[i] = new ResolvedGridActorGate(gate.Type, gate.Count.Resolve(positionInRange, rng));
            }

            return resolved;
        }
    }
}

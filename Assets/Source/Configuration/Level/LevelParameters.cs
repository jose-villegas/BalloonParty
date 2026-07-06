using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.GridActors;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     The resolved, plain form of a level's difficulty mix — the runtime output of
    ///     <see cref="RangedLevelParameters.Resolve" />, cached per level by
    ///     <c>LevelDifficultyResolver</c> and exposed via <c>IActiveLevelParameters</c>. Never
    ///     authored or serialized (ranges and customs both author <see cref="RangedLevelParameters" />);
    ///     it only ever exists as a constructed DTO. Field defaults are the parameterless fallback
    ///     (Simple-only, one line per turn) used when no authored range contains a level.
    /// </summary>
    public class LevelParameters
    {
        private readonly int _spawnLines = 1;
        private readonly int _boardLines = 5;
        private readonly int _itemCadence = 5;
        private readonly AnimationCurve _initialItemCountWeights = new();

        private readonly AnimationCurve _itemCountWeights =
            new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

        private readonly BalloonTypeWeight[] _balloonWeights =
        {
            new(BalloonType.Simple, 1f),
        };

        private readonly ItemTypeWeight[] _itemWeights = Array.Empty<ItemTypeWeight>();

        private readonly ResolvedGridActorGate[] _gridActorGates =
        {
            new(GridActorType.Puff, 4),
        };

        private readonly int _allowedColorsMask = ~0;

        public LevelParameters()
        {
        }

        internal LevelParameters(
            int spawnLines, int boardLines, int itemCadence, AnimationCurve initialItemCountWeights,
            AnimationCurve itemCountWeights, BalloonTypeWeight[] balloonWeights, ItemTypeWeight[] itemWeights,
            ResolvedGridActorGate[] gridActorGates, int allowedColorsMask)
        {
            _spawnLines = spawnLines;
            _boardLines = boardLines;
            _itemCadence = itemCadence;
            _initialItemCountWeights = initialItemCountWeights;
            _itemCountWeights = itemCountWeights;
            _balloonWeights = balloonWeights;
            _itemWeights = itemWeights;
            _gridActorGates = gridActorGates;
            _allowedColorsMask = allowedColorsMask;
        }

        public int SpawnLines => _spawnLines;
        public int BoardLines => _boardLines;
        public int ItemCadence => _itemCadence;
        public AnimationCurve InitialItemCountWeights => _initialItemCountWeights;
        public AnimationCurve ItemCountWeights => _itemCountWeights;
        public BalloonTypeWeight[] BalloonWeights => _balloonWeights;
        public ItemTypeWeight[] ItemWeights => _itemWeights;
        public ResolvedGridActorGate[] GridActorGates => _gridActorGates;

        /// <summary>Same bit-per-color convention as <see cref="ColorableBalloonVariant" />'s mask — all bits set = every palette color.</summary>
        public int AllowedColorsMask => _allowedColorsMask;
    }
}

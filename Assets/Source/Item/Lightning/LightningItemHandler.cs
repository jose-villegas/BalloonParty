using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Handles the Lightning item. Finds all same-color balloons, sorts them by
    ///     distance nearest-first, then plays the chain-lightning animation fire-and-forget —
    ///     hitting each target per-jump via <see cref="ChainLightningView.PrepareDisplay" />.
    /// </summary>
    internal class LightningItemHandler : IBalloonItem
    {
        private sealed class ByDistanceComparer : IComparer<(IBalloonModel model, Vector3 worldPos)>
        {
            internal Vector3 Origin;

            public int Compare((IBalloonModel model, Vector3 worldPos) a, (IBalloonModel model, Vector3 worldPos) b)
                => (Origin - a.worldPos).sqrMagnitude.CompareTo((Origin - b.worldPos).sqrMagnitude);
        }

        private readonly IPublisher<ActorHitMessage> _hitPublisher;
        private readonly IItemConfiguration _itemConfig;
        private readonly PoolManager _poolManager;
        private readonly SlotGrid _grid;

        private readonly List<(IBalloonModel model, Vector3 worldPos)> _targetsBuffer = new();
        private readonly List<Vector3> _positionsBuffer = new();
        private readonly ByDistanceComparer _distanceComparer = new();

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Lightning;

        [Inject]
        public LightningItemHandler(
            IItemConfiguration itemConfig,
            IPublisher<ActorHitMessage> hitPublisher,
            SlotGrid grid,
            PoolManager poolManager)
        {
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _grid = grid;
            _poolManager = poolManager;
        }

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
        }

        public UniTask Activate()
        {
            var settings = _itemConfig[ItemType.Lightning];
            CollectSortedTargets(_targetsBuffer);

            if (_targetsBuffer.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            var sourceColorId = _balloon.GetColorId();
            var context = new DamageContext(settings.Damage, settings.Flags, sourceColorId);

            if (settings.ActivationEffectPrefab == null)
            {
                foreach (var (model, pos) in _targetsBuffer)
                {
                    _hitPublisher.Publish(ActorHitMessage.From(model,
                        pos,
                        Vector3.zero,
                        context));
                }

                return UniTask.CompletedTask;
            }

            _positionsBuffer.Clear();
            _positionsBuffer.Add(_worldPosition);
            foreach (var (_, pos) in _targetsBuffer)
            {
                _positionsBuffer.Add(pos);
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key,
                () => new EffectPoolChannel(settings.ActivationEffectPrefab));

            var view = (ChainLightningView)effect;
            var targets = _targetsBuffer;

            view.PrepareDisplay(_positionsBuffer, settings, OnJump);
            view.Play(Vector3.zero, Color.white, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;

            void OnJump(int index)
            {
                if (index >= targets.Count)
                {
                    return;
                }

                var (model, pos) = targets[index];
                _hitPublisher.Publish(ActorHitMessage.From(model,
                    pos,
                    Vector3.zero,
                    context));
            }
        }

        private void CollectSortedTargets(List<(IBalloonModel model, Vector3 worldPos)> result)
        {
            result.Clear();

            if (_balloon is not IHasColor sourceColor)
            {
                return;
            }

            var color = sourceColor.Color.Value;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    if (_grid.At(slot) is not IBalloonModel model)
                    {
                        continue;
                    }

                    if (ReferenceEquals(model, _balloon))
                    {
                        continue;
                    }

                    if (model is not IHasColor modelColor || modelColor.Color.Value != color)
                    {
                        continue;
                    }

                    result.Add((model, _grid.IndexToWorldPosition(slot)));
                }
            }

            _distanceComparer.Origin = _worldPosition;
            result.Sort(_distanceComparer);
        }
    }
}

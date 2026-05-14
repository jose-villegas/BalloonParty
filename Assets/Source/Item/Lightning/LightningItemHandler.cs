using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
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
    public class LightningItemHandler : IBalloonItem
    {
        private readonly IPublisher<BalloonHitMessage> _hitPublisher;
        private readonly ItemConfiguration _itemConfig;
        private readonly PoolManager _poolManager;
        private readonly SlotGrid _grid;

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Lightning;

        [Inject]
        public LightningItemHandler(
            ItemConfiguration itemConfig,
            IPublisher<BalloonHitMessage> hitPublisher,
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
            var targets = CollectSortedTargets();

            if (targets.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            if (settings.ActivationEffectPrefab == null)
            {
                foreach (var (model, pos) in targets)
                {
                    _hitPublisher.Publish(new BalloonHitMessage(model, pos, Vector3.zero, settings.Damage));
                }

                return UniTask.CompletedTask;
            }

            var positions = new List<Vector3>(targets.Count + 1) { _worldPosition };
            foreach (var (_, pos) in targets)
            {
                positions.Add(pos);
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key,
                () => new EffectPoolChannel(settings.ActivationEffectPrefab));

            var view = (ChainLightningView)effect;

            view.PrepareDisplay(
                positions,
                settings.LightningSegmentsMultiplier,
                settings.LightningRandomness,
                settings.LightningJumpTime,
                index =>
                {
                    if (index < targets.Count)
                    {
                        var (model, pos) = targets[index];
                        _hitPublisher.Publish(new BalloonHitMessage(model, pos, Vector3.zero, settings.Damage));
                    }
                });

            view.Play(Vector3.zero, Color.white, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;
        }


        private List<(IBalloonModel model, Vector3 worldPos)> CollectSortedTargets()
        {
            var color = _balloon.Color.Value;
            var result = new List<(IBalloonModel, Vector3)>();

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    var model = _grid.At(slot);

                    if (model == null || model == _balloon)
                    {
                        continue;
                    }

                    if (model.Color.Value != color)
                    {
                        continue;
                    }

                    result.Add((model, _grid.IndexToWorldPosition(slot)));
                }
            }

            result.Sort((a, b) =>
                Vector3.Distance(_worldPosition, a.Item2)
                    .CompareTo(Vector3.Distance(_worldPosition, b.Item2)));

            return result;
        }
    }
}

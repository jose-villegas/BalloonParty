using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared;
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
    ///     distance, then plays the chain-lightning animation — hitting each target
    ///     as the bolt arrives (per-jump). Returns a <see cref="UniTask" /> that
    ///     completes after the full forward+reverse animation so <see cref="ItemActivator" />
    ///     can publish <c>ItemActivatedMessage</c> at the right time.
    /// </summary>
    public class LightningItemHandler : IBalloonItem
    {
        private const string PoolKey = "ChainLightning";

        private readonly ItemConfiguration _itemConfig;
        private readonly IPublisher<BalloonHitMessage> _hitPublisher;
        private readonly SlotGrid _grid;
        private readonly PoolManager _poolManager;

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

        public async UniTask Activate()
        {
            var settings = _itemConfig[ItemType.Lightning];
            var targets = CollectSortedTargets();

            if (targets.Count == 0)
            {
                return;
            }

            // No prefab configured — instant-hit all targets without animation
            if (settings.LightningPrefab == null)
            {
                foreach (var (model, pos) in targets)
                {
                    _hitPublisher.Publish(new BalloonHitMessage(model, pos));
                }

                return;
            }

            // Build the position chain: item balloon origin → target0 → target1 → …
            var positions = new List<Vector3>(targets.Count + 1) { _worldPosition };
            foreach (var (_, pos) in targets)
            {
                positions.Add(pos);
            }

            var view = _poolManager.GetOrRegister(PoolKey,
                () => new ChainLightningPoolChannel(settings.LightningPrefab));

            await view.Display(
                positions,
                settings.LightningSegmentsMultiplier,
                settings.LightningRandomness,
                settings.LightningJumpTime,
                index =>
                {
                    // index 0 = first target balloon (positions[1]) — item balloon is positions[0]
                    if (index < targets.Count)
                    {
                        var (model, pos) = targets[index];
                        _hitPublisher.Publish(new BalloonHitMessage(model, pos));
                    }
                },
                CancellationToken.None);

            _poolManager.Return(PoolKey, view);
        }

        // ── Target collection ─────────────────────────────────────────────────────

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

            // Sort nearest-first (matching legacy Comparison())
            result.Sort((a, b) =>
                Vector3.Distance(_worldPosition, a.Item2)
                    .CompareTo(Vector3.Distance(_worldPosition, b.Item2)));

            return result;
        }
    }
}



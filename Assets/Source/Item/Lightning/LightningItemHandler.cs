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
using BalloonParty.Configuration.Items;

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

        private readonly IHitDispatcher _hitDispatcher;
        private readonly IItemConfiguration _itemConfig;
        private readonly PoolManager _poolManager;
        private readonly SlotGrid _grid;

        // Shared across activations is safe here only because it is set and consumed
        // synchronously inside one CollectSortedTargets call.
        private readonly ByDistanceComparer _distanceComparer = new();

        public ItemType Type => ItemType.Lightning;

        [Inject]
        public LightningItemHandler(
            IItemConfiguration itemConfig,
            IHitDispatcher hitDispatcher,
            SlotGrid grid,
            PoolManager poolManager)
        {
            _itemConfig = itemConfig;
            _hitDispatcher = hitDispatcher;
            _grid = grid;
            _poolManager = poolManager;
        }

        public UniTask Activate(ItemActivationContext activation)
        {
            var balloon = activation.Balloon;
            var worldPosition = activation.WorldPosition;

            var settings = _itemConfig[ItemType.Lightning];

            // Per-activation lists, not shared buffers: the chain view keeps the positions
            // reference and OnJump reads the targets for seconds after this method returns,
            // so a second activation mid-chain must not touch either.
            var targets = new List<(IBalloonModel model, Vector3 worldPos)>();
            CollectSortedTargets(balloon, worldPosition, targets);

            if (targets.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            var sourceColorId = balloon.GetColorId();
            var context = new DamageContext(settings.Damage, settings.Flags, sourceColorId);

            if (settings.ActivationEffectPrefab == null)
            {
                foreach (var (model, pos) in targets)
                {
                    _hitDispatcher.Dispatch(ActorHitMessage.From(model,
                        pos,
                        Vector3.zero,
                        context));
                }

                return UniTask.CompletedTask;
            }

            var positions = new List<Vector3>(targets.Count + 1) { worldPosition };
            foreach (var (_, pos) in targets)
            {
                positions.Add(pos);
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key,
                () => new SimplePoolChannel<EffectView>(settings.ActivationEffectPrefab));

            if (effect is not IChainEffect chain)
            {
                Debug.LogError(
                    $"LightningItemHandler: pooled effect for \"{key}\" is not an IChainEffect — " +
                    "check the prefab's EffectView component.");
                _poolManager.Return(key, effect);
                return UniTask.CompletedTask;
            }

            chain.PrepareDisplay(positions, settings, OnJump);
            effect.Play(Vector3.zero, Color.white, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;

            void OnJump(int index)
            {
                if (index >= targets.Count)
                {
                    return;
                }

                var (model, pos) = targets[index];
                _hitDispatcher.Dispatch(ActorHitMessage.From(model,
                    pos,
                    Vector3.zero,
                    context));
            }
        }

        private void CollectSortedTargets(
            IBalloonModel balloon, Vector3 origin, List<(IBalloonModel model, Vector3 worldPos)> result)
        {
            result.Clear();

            if (balloon is not IHasColor sourceColor)
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

                    if (ReferenceEquals(model, balloon))
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

            _distanceComparer.Origin = origin;
            result.Sort(_distanceComparer);
        }
    }
}

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
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Handles the Lightning item: chains through same-color balloons nearest-first.
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
        private readonly IGamePalette _palette;
        private readonly PoolManager _poolManager;
        private readonly SlotGrid _grid;

        // Safe to share: set and consumed synchronously within one CollectSortedTargets call.
        private readonly ByDistanceComparer _distanceComparer = new();

        public ItemType Type => ItemType.Lightning;

        [Inject]
        public LightningItemHandler(
            IItemConfiguration itemConfig,
            IHitDispatcher hitDispatcher,
            IGamePalette palette,
            SlotGrid grid,
            PoolManager poolManager)
        {
            _itemConfig = itemConfig;
            _hitDispatcher = hitDispatcher;
            _palette = palette;
            _grid = grid;
            _poolManager = poolManager;
        }

        public UniTask Activate(ItemActivationContext activation)
        {
            var balloon = activation.Balloon;
            var worldPosition = activation.WorldPosition;

            var settings = _itemConfig[ItemType.Lightning];

            // Per-activation lists: the chain view holds this reference long after this method returns.
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

            var tint = string.IsNullOrEmpty(sourceColorId) ? Color.white : _palette.GetColor(sourceColorId);

            chain.PrepareDisplay(positions, settings, OnJump);
            effect.Play(Vector3.zero, tint, () => _poolManager.Return(key, effect));

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

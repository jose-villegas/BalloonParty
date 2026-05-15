using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Handles the Paint item. When activated, finds all neighboring balloons of the
    ///     popped balloon, launches a paint blob VFX toward each one, and converts their
    ///     color to the popped balloon's color on splash — Splatoon-style.
    /// </summary>
    public class PaintItemHandler : IBalloonItem
    {
        private readonly GamePalette _palette;
        private readonly ItemConfiguration _itemConfig;
        private readonly SlotGrid _grid;
        private readonly PoolManager _poolManager;

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Paint;

        [Inject]
        public PaintItemHandler(
            GamePalette palette,
            ItemConfiguration itemConfig,
            SlotGrid grid,
            PoolManager poolManager)
        {
            _palette = palette;
            _itemConfig = itemConfig;
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
            var settings = _itemConfig[ItemType.Paint];
            var paintColor = _balloon.Color.Value;

            if (string.IsNullOrEmpty(paintColor))
            {
                return UniTask.CompletedTask;
            }

            var slot = _balloon.SlotIndex.Value;
            var neighbors = _grid.GetNeighbors(slot.x, slot.y);

            if (neighbors.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            var tint = _palette.GetColor(paintColor);
            var targets = new List<(IWriteableBalloonModel model, Vector3 pos, bool shouldPaint)>();

            foreach (var neighbor in neighbors)
            {
                // Skip non-paintable balloons (e.g. tough, unbreakable)
                if (!neighbor.IsPaintable)
                {
                    continue;
                }

                var neighborSlot = neighbor.SlotIndex.Value;
                var targetPos = _grid.IndexToWorldPosition(neighborSlot);

                // All paintable neighbors get a blob for visual impact,
                // but only different-color ones actually change color.
                var shouldPaint = neighbor.Color.Value != paintColor;
                targets.Add((neighbor, targetPos, shouldPaint));
            }

            if (targets.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            if (settings.ActivationEffectPrefab == null)
            {
                // No effect prefab — change colors immediately
                foreach (var (model, _, shouldPaint) in targets)
                {
                    if (shouldPaint)
                    {
                        model.Color.Value = paintColor;
                    }
                }

                return UniTask.CompletedTask;
            }

            var flights = new List<(Vector3 from, Vector3 to)>(targets.Count);
            foreach (var (_, targetPos, _) in targets)
            {
                flights.Add((_worldPosition, targetPos));
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key,
                () => new EffectPoolChannel(settings.ActivationEffectPrefab));

            var view = (PaintSplashView)effect;

            view.PrepareDisplay(
                flights,
                settings.PaintBlobFlightDuration,
                settings.PaintBlobArcHeight,
                settings.PaintBlobStartScale,
                index =>
                {
                    if (index < targets.Count && targets[index].shouldPaint)
                    {
                        targets[index].model.Color.Value = paintColor;
                    }
                });

            view.Play(_worldPosition, tint, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;
        }
    }
}




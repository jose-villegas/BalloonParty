using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Handles the Paint item. When activated, finds all neighboring balloons of the
    ///     popped balloon, launches a paint blob VFX toward each one, and converts their
    ///     color to the popped balloon's color on splash — Splatoon-style.
    /// </summary>
    internal class PaintItemHandler : IBalloonItem
    {
        private const int NeighborCount = 6;

        private readonly IGamePalette _palette;
        private readonly IItemConfiguration _itemConfig;
        private readonly SlotGrid _grid;
        private readonly PoolManager _poolManager;
        private readonly DisturbanceFieldService _disturbanceField;

        public ItemType Type => ItemType.Paint;

        [Inject]
        public PaintItemHandler(
            IGamePalette palette,
            IItemConfiguration itemConfig,
            SlotGrid grid,
            PoolManager poolManager,
            DisturbanceFieldService disturbanceField)
        {
            _palette = palette;
            _itemConfig = itemConfig;
            _grid = grid;
            _poolManager = poolManager;
            _disturbanceField = disturbanceField;
        }

        public UniTask Activate(IBalloonModel balloon, Vector3 worldPosition)
        {
            var settings = _itemConfig[ItemType.Paint];
            if (balloon is not IHasColor sourceColor)
            {
                return UniTask.CompletedTask;
            }

            var paintColor = sourceColor.Color.Value;

            if (string.IsNullOrEmpty(paintColor))
            {
                return UniTask.CompletedTask;
            }

            var slot = balloon.SlotIndex.Value;
            var neighborIndices = HexCoordinates.HexNeighborIndices(slot.x, slot.y);
            var tint = _palette.GetColor(paintColor);

            var paintTargets = BuildPaintTargets(neighborIndices, paintColor);

            if (settings.ActivationEffectPrefab == null)
            {
                PaintImmediate(worldPosition, paintColor, neighborIndices, paintTargets);
                return UniTask.CompletedTask;
            }

            // Always launch all 6 blobs regardless of occupancy.
            var flights = new List<(Vector3 from, Vector3 to)>(NeighborCount);

            for (var i = 0; i < NeighborCount; i++)
            {
                flights.Add((worldPosition, _grid.IndexToWorldPosition(neighborIndices[i])));
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key,
                () => new SimplePoolChannel<EffectView>(settings.ActivationEffectPrefab));

            if (effect is not ISplashEffect splash)
            {
                Debug.LogError(
                    $"PaintItemHandler: pooled effect for \"{key}\" is not an ISplashEffect — " +
                    "check the prefab's EffectView component.");
                _poolManager.Return(key, effect);
                return UniTask.CompletedTask;
            }

            splash.PrepareDisplay(flights, settings, _poolManager, OnSplash);
            effect.Play(worldPosition, tint, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;

            // Captures only this activation's locals — splashes land over time and a second
            // Paint activation may run in between, so no handler field may be read here.
            void OnSplash(int index)
            {
                if (index < NeighborCount && paintTargets[index] != null)
                {
                    paintTargets[index].Color.Value = paintColor;
                }

                if (index < NeighborCount)
                {
                    var splashPos = _grid.IndexToWorldPosition(neighborIndices[index]);
                    var dir = ((Vector2)(splashPos - worldPosition)).normalized;
                    _disturbanceField.Stamp(StampSource.Paint, splashPos, dir);
                }
            }
        }

        // One paint target per neighbour index — null where the slot is empty, non-paintable, or
        // already the paint colour.
        private IPaintable[] BuildPaintTargets(Vector2Int[] neighborIndices, string paintColor)
        {
            var targets = new IPaintable[NeighborCount];
            for (var i = 0; i < NeighborCount; i++)
            {
                var idx = neighborIndices[i];
                var actor = _grid.IsEmpty(idx.x, idx.y) ? null : _grid.At(idx);

                if (actor is IPaintable colorable && colorable.Color.Value != paintColor)
                {
                    targets[i] = colorable;
                }
            }

            return targets;
        }

        // No activation effect: recolour the targets and stamp the disturbance field immediately.
        private void PaintImmediate(
            Vector3 worldPosition, string paintColor, Vector2Int[] neighborIndices, IPaintable[] paintTargets)
        {
            for (var i = 0; i < NeighborCount; i++)
            {
                if (paintTargets[i] != null)
                {
                    paintTargets[i].Color.Value = paintColor;
                }

                var neighborPos = _grid.IndexToWorldPosition(neighborIndices[i]);
                var dir = ((Vector2)(neighborPos - worldPosition)).normalized;
                _disturbanceField.Stamp(StampSource.Paint, neighborPos, dir);
            }
        }
    }
}

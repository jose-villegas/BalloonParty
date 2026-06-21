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

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

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

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
        }

        public UniTask Activate()
        {
            var settings = _itemConfig[ItemType.Paint];
            if (_balloon is not IHasColor sourceColor)
            {
                return UniTask.CompletedTask;
            }

            var paintColor = sourceColor.Color.Value;

            if (string.IsNullOrEmpty(paintColor))
            {
                return UniTask.CompletedTask;
            }

            var slot = _balloon.SlotIndex.Value;
            var neighborIndices = HexCoordinates.HexNeighborIndices(slot.x, slot.y);
            var tint = _palette.GetColor(paintColor);

            // Build a paint target per neighbor index — null when slot is empty or non-paintable.
            var paintTargets = new IPaintable[NeighborCount];

            for (var i = 0; i < NeighborCount; i++)
            {
                var idx = neighborIndices[i];
                var actor = _grid.IsEmpty(idx.x, idx.y) ? null : _grid.At(idx);

                if (actor is IPaintable colorable && colorable.Color.Value != paintColor)
                {
                    paintTargets[i] = colorable;
                }
            }

            if (settings.ActivationEffectPrefab == null)
            {
                for (var i = 0; i < NeighborCount; i++)
                {
                    if (paintTargets[i] != null)
                    {
                        paintTargets[i].Color.Value = paintColor;
                    }

                    var neighborPos = _grid.IndexToWorldPosition(neighborIndices[i]);
                    var dir = ((Vector2)(neighborPos - _worldPosition)).normalized;
                    _disturbanceField.Stamp(StampSource.Paint, neighborPos, dir);
                }

                return UniTask.CompletedTask;
            }

            // Always launch all 6 blobs regardless of occupancy.
            var flights = new List<(Vector3 from, Vector3 to)>(NeighborCount);

            for (var i = 0; i < NeighborCount; i++)
            {
                flights.Add((_worldPosition, _grid.IndexToWorldPosition(neighborIndices[i])));
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key,
                () => new SimplePoolChannel<EffectView>(settings.ActivationEffectPrefab));

            var view = (PaintSplashView)effect;

            view.PrepareDisplay(flights, settings, _poolManager, OnSplash);
            view.Play(_worldPosition, tint, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;

            void OnSplash(int index)
            {
                if (index < NeighborCount && paintTargets[index] != null)
                {
                    paintTargets[index].Color.Value = paintColor;
                }

                if (index < NeighborCount)
                {
                    var splashPos = _grid.IndexToWorldPosition(neighborIndices[index]);
                    var dir = ((Vector2)(splashPos - _worldPosition)).normalized;
                    _disturbanceField.Stamp(StampSource.Paint, splashPos, dir);
                }
            }
        }
    }
}

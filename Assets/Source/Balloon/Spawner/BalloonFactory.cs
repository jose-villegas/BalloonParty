using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using DG.Tweening;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>Assembles a single balloon's view, model, and controller, and places it on the grid.</summary>
    internal class BalloonFactory
    {
        private readonly IGamePalette _palette;
        private readonly PoolManager _poolManager;
        private readonly SlotGrid _grid;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IActiveLevelParameters _levelParams;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly BalloonControllerContext _controllerContext;

        [Inject]
        internal BalloonFactory(
            IGamePalette palette,
            PoolManager poolManager,
            SlotGrid grid,
            IBalloonsConfiguration balloonsConfig,
            IActiveLevelParameters levelParams,
            DisturbanceFieldService disturbanceField,
            BalloonControllerContext controllerContext)
        {
            _palette = palette;
            _poolManager = poolManager;
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _levelParams = levelParams;
            _disturbanceField = disturbanceField;
            _controllerContext = controllerContext;
        }

        /// <summary><paramref name="onReturned"/> runs when the balloon returns to the pool.</summary>
        public IWriteableBalloonModel Create(
            BalloonPrefabEntry entry,
            Vector2Int slot,
            IReadOnlyList<Vector3> spawnPath,
            Action onReturned)
        {
            var poolKey = entry.PoolKey;

            var view = _poolManager.Get<BalloonView>(poolKey);
            view.transform.position = spawnPath[0];

            var model = BalloonModelFactory.Create(entry, _palette, _levelParams.Current.AllowedColors);
            view.Variant.Initialize(model, _levelParams.Current.AllowedColorsMask);

            var controller = new BalloonController(
                model, view, poolKey, onReturned, entry.HitVfxOverrides, _controllerContext);
            controller.Start();

            _grid.Place(model, view, slot);
            AnimateSpawn(view, spawnPath, model);

            return model;
        }

        private void AnimateSpawn(BalloonView view, IReadOnlyList<Vector3> spawnPath, IWriteableBalloonModel model)
        {
            model.IsStable.Value = false;
            view.transform.localScale = Vector3.zero;

            var duration = UnityEngine.Random.Range(
                _balloonsConfig.BalloonSpawnAnimationDurationRange.x,
                _balloonsConfig.BalloonSpawnAnimationDurationRange.y);

            var waypointCount = spawnPath.Count - 1;

            if (waypointCount <= 1)
            {
                // Path too short for CatmullRom — scale in only
                view.transform.position = spawnPath[spawnPath.Count - 1];
                view.transform.DOScale(Vector3.one, duration * 0.5f)
                    .OnComplete(() => model.IsStable.Value = true);
                return;
            }

            var waypoints = new Vector3[waypointCount];
            for (var i = 0; i < waypointCount; i++)
            {
                waypoints[i] = spawnPath[i + 1];
            }

            var viewTransform = view.transform;

            viewTransform.DOPath(waypoints, duration, PathType.CatmullRom)
                .StampDisturbanceAlongPath(viewTransform, _disturbanceField, StampSource.BalloonPath)
                .OnComplete(() => model.IsStable.Value = true);

            view.transform.DOScale(Vector3.one, duration);
        }
    }
}

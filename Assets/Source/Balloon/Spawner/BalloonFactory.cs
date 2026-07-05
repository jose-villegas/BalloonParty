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

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Assembles a single balloon: pulls a view from the pool, builds the model + controller, places
    ///     it on the grid, and plays the entry animation. The spawner owns scheduling, per-type caps, and
    ///     path computation; this owns the object-graph wiring so that knowledge lives in one place.
    /// </summary>
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

        /// <summary>
        ///     Spawns the balloon for <paramref name="entry"/> at <paramref name="slot"/>, rising along
        ///     <paramref name="spawnPath"/>. <paramref name="onReturned"/> runs when the balloon returns
        ///     to the pool (the spawner uses it to decrement its active-count). Returns the model.
        /// </summary>
        public IWriteableBalloonModel Create(
            BalloonPrefabEntry entry,
            Vector2Int slot,
            IReadOnlyList<Vector3> spawnPath,
            Action onReturned)
        {
            var poolKey = entry.PoolKey;

            var view = _poolManager.Get<BalloonView>(poolKey);
            view.transform.position = spawnPath[0];

            var model = BalloonModelFactory.Create(entry, _palette, _levelParams.AllowedColors);
            view.Variant.Initialize(model, _levelParams.AllowedColorsMask);

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
                // Path too short for CatmullRom — place at target, scale in only
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

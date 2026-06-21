using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Spawner;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Spawner
{
    internal class BalloonSpawner : IStartable, IGridSpawner, IRunResettable
    {
        private readonly Dictionary<string, int> _activeCounts = new();
        private readonly BalloonBalancer _balancer;
        private readonly BalloonControllerContext _controllerContext;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IGamePalette _palette;
        private readonly CancellationTokenSource _cts = new();
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly SlotGrid _grid;
        private readonly IPublisher<ItemCheckMessage> _itemCheckPublisher;
        private readonly ISubscriber<SpawnBalloonLineMessage> _lineSubscriber;
        private readonly List<IBalloonModel> _newlySpawnedBalloons = new();
        private readonly IObjectResolver _resolver;
        private readonly PoolManager _poolManager;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly RejectedBalloonEffect _rejectedBalloon;
        private readonly BalloonPlacementResolver _placement;
        private readonly List<Vector3> _spawnPathBuffer = new();

        private int _turnCount;
        private int _generation;
        private bool _prewarmed;

        public SpawnStage SpawnPriority => SpawnStage.BalloonActors;
        public int ResetOrder => RunResetOrder.Counters;

        [Inject]
        internal BalloonSpawner(
            SlotGrid grid,
            IBalloonsConfiguration balloonsConfig,
            IGamePalette palette,
            IObjectResolver resolver,
            PoolManager poolManager,
            ISubscriber<SpawnBalloonLineMessage> lineSubscriber,
            BalloonBalancer balancer,
            BalloonControllerContext controllerContext,
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            IPublisher<ItemCheckMessage> itemCheckPublisher,
            RejectedBalloonEffect rejectedBalloon,
            BalloonPlacementResolver placement,
            DisturbanceFieldService disturbanceField)
        {
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _palette = palette;
            _resolver = resolver;
            _poolManager = poolManager;
            _lineSubscriber = lineSubscriber;
            _balancer = balancer;
            _controllerContext = controllerContext;
            _balancePublisher = balancePublisher;
            _destroyedSubscriber = destroyedSubscriber;
            _itemCheckPublisher = itemCheckPublisher;
            _rejectedBalloon = rejectedBalloon;
            _placement = placement;
            _disturbanceField = disturbanceField;
        }

        public void Start()
        {
            foreach (var entry in _balloonsConfig.Entries)
            {
                _poolManager.Register(entry.PoolKey,
                    new BalloonPoolChannel(_resolver, entry.Prefab));
            }

            _lineSubscriber.Subscribe(msg => OnSpawnLinesRequested(msg.LineCount));
            _destroyedSubscriber.Subscribe(_ => OnProjectileDestroyed());

            // Begin prewarm immediately so pools are ready before SpawnAsync is called.
            PrewarmThenFlagAsync(_cts.Token).Forget();
        }

        public async UniTask SpawnAsync(CancellationToken ct)
        {
            // WaitUntil is re-awaitable (unlike a stored UniTask), so a restart re-spawn can wait on
            // the same prewarm flag without an "await twice" error; once warm it returns immediately.
            await UniTask.WaitUntil(() => _prewarmed, cancellationToken: ct);
            PopulateInitialGrid();
            _newlySpawnedBalloons.Clear();
        }

        public void ResetRun(int generation)
        {
            // Adopt the new run's generation to drop any in-flight delayed line spawns, then clear
            // counters. Active balloons have already returned themselves via the board-clear broadcast.
            _generation = generation;
            _activeCounts.Clear();
            _turnCount = 0;
            _newlySpawnedBalloons.Clear();
        }

        private async UniTaskVoid PrewarmThenFlagAsync(CancellationToken ct)
        {
            await PrewarmAsync(ct);
            _prewarmed = true;
        }

        private async UniTask PrewarmAsync(CancellationToken ct)
        {
            var totalSlots = _grid.Columns * _balloonsConfig.GameStartedBalloonLines;

            foreach (var entry in _balloonsConfig.Entries)
            {
                var count = entry.MaxCount > 0
                    ? Mathf.Min(entry.MaxCount, totalSlots)
                    : totalSlots;

                await _poolManager.PrewarmAsync(entry.PoolKey, count, ct);
            }
        }

        private void AnimateSpawn(BalloonView view, List<Vector3> spawnPath, IWriteableBalloonModel model)
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
            spawnPath.CopyTo(1, waypoints, 0, waypointCount);

            var viewTransform = view.transform;

            viewTransform.DOPath(waypoints, duration, PathType.CatmullRom)
                .StampDisturbanceAlongPath(viewTransform, _disturbanceField, StampSource.BalloonPath)
                .OnComplete(() => model.IsStable.Value = true);

            view.transform.DOScale(Vector3.one, duration);
        }

        private void OnProjectileDestroyed()
        {
            _turnCount++;
            if (_turnCount <= 1)
            {
                return;
            }

            SpawnLinesWithDelayAsync(_balloonsConfig.NewProjectileBalloonLines, _cts.Token, _generation).Forget();
        }

        private void OnSpawnLinesRequested(int lineCount)
        {
            if (lineCount <= 1)
            {
                SpawnLine();
                return;
            }

            SpawnLinesWithDelayAsync(lineCount, _cts.Token, _generation).Forget();
        }

        private void PopulateInitialGrid()
        {
            // The initial grid starts empty and is sized to fit, so it never rejects a balloon.
            for (var i = 0; i < _balloonsConfig.GameStartedBalloonLines; i++)
            {
                SpawnLineInternal(allowReject: false);
            }
        }

        private void PublishItemCheck()
        {
            if (_newlySpawnedBalloons.Count > 0)
            {
                _itemCheckPublisher.Publish(
                    new ItemCheckMessage(_newlySpawnedBalloons, _turnCount));
                _newlySpawnedBalloons.Clear();
            }
        }

        private void SpawnBalloon(Vector2Int slot)
        {
            var entry = _balloonsConfig.Entries.PickRandom(_activeCounts);
            if (entry == null)
            {
                Debug.LogWarning(
                    $"BalloonSpawner.SpawnBalloon: PickRandom returned null for slot ({slot.x},{slot.y}) " +
                    "— all balloon types are at their max count.");
                return;
            }

            var source = new Vector2Int(slot.x, slot.y + _balloonsConfig.SpawnEntryRowOffset);
            _grid.ComputePath(source, slot, _spawnPathBuffer);
            var poolKey = entry.PoolKey;

            _activeCounts[poolKey] = _activeCounts.GetValueOrDefault(poolKey) + 1;

            var view = _poolManager.Get<BalloonView>(poolKey);
            view.transform.position = _spawnPathBuffer[0];

            var model = BalloonModelFactory.Create(entry, _palette);

            var variant = view.Variant;
            variant.Initialize(model);

            var controller = new BalloonController(model,
                view,
                poolKey,
                () => _activeCounts[poolKey]--,
                entry.HitVfxOverrides,
                _controllerContext);
            controller.Start();

            _grid.Place(model, view, slot);
            AnimateSpawn(view, _spawnPathBuffer, model);

            _newlySpawnedBalloons.Add(model);
        }

        private void SpawnLine()
        {
            _balancer.Balance();
            SpawnLineInternal(allowReject: true);
            PublishItemCheck();
            _balancePublisher.Publish(default);
        }

        private void SpawnLineInternal(bool allowReject)
        {
            var rejectIndex = 0;

            for (var col = 0; col < _grid.Columns; col++)
            {
                var slot = _placement.Resolve(col, allowReject);
                if (slot.HasValue)
                {
                    SpawnBalloon(slot.Value);
                    continue;
                }

                if (allowReject)
                {
                    // No room anywhere and pressure couldn't open this column — pop the would-be
                    // balloon below the grid and cost the player one hit point.
                    _rejectedBalloon.Play(col, rejectIndex++, _activeCounts);
                }
            }
        }

        private async UniTaskVoid SpawnLinesWithDelayAsync(int lineCount, CancellationToken ct, int generation)
        {
            // Bracket the whole multi-line sequence so the overflow hold (thrower lock) spans the gaps
            // between lines and only releases once every line's pops are done.
            _rejectedBalloon.BeginSpawnSequence();
            try
            {
                _balancer.Balance();

                for (var i = 0; i < lineCount; i++)
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    SpawnLineInternal(allowReject: true);
                    await UniTask.Delay(
                        (int)(_balloonsConfig.NewBalloonLinesTimeInterval * 1000),
                        cancellationToken: ct);
                }

                if (generation != _generation)
                {
                    return;
                }

                PublishItemCheck();
                _balancePublisher.Publish(default);
            }
            finally
            {
                _rejectedBalloon.EndSpawnSequence();
            }
        }
    }
}

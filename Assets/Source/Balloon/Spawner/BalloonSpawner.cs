using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Nudge;
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
        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IGamePalette _palette;
        private readonly CancellationTokenSource _cts = new();
        private readonly IPublisher<BalloonDeflectedMessage> _deflectedPublisher;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly IPublisher<ItemCheckMessage> _itemCheckPublisher;
        private readonly ISubscriber<SpawnBalloonLineMessage> _lineSubscriber;
        private readonly List<IBalloonModel> _newlySpawnedBalloons = new();
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly IObjectResolver _resolver;
        private readonly PoolManager _poolManager;
        private readonly IPublisher<TransformCapturedMessage> _transformCapturedPublisher;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly List<Vector3> _spawnPathBuffer = new();

        private int _turnCount;
        private int _generation;
        private UniTask _prewarmTask;

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
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            IPublisher<ItemCheckMessage> itemCheckPublisher,
            IPublisher<TransformCapturedMessage> transformCapturedPublisher,
            IPublisher<BalloonDeflectedMessage> deflectedPublisher,
            IPublisher<NudgeMessage> nudgePublisher,
            DisturbanceFieldService disturbanceField)
        {
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _palette = palette;
            _resolver = resolver;
            _poolManager = poolManager;
            _lineSubscriber = lineSubscriber;
            _balancer = balancer;
            _balancePublisher = balancePublisher;
            _hitSubscriber = hitSubscriber;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _boardClearSubscriber = boardClearSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _itemCheckPublisher = itemCheckPublisher;
            _transformCapturedPublisher = transformCapturedPublisher;
            _deflectedPublisher = deflectedPublisher;
            _nudgePublisher = nudgePublisher;
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
            _prewarmTask = PrewarmAsync(_cts.Token);
        }

        public async UniTask SpawnAsync(CancellationToken ct)
        {
            await _prewarmTask;
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

            var duration = Random.Range(
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

        private int? FindFirstEmptyRowFromTop(int col)
        {
            for (var row = 0; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row))
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the topmost empty row reachable from the spawn entry (bottom of grid).
        /// Balloons enter from below and travel upward. A non-traversable static actor
        /// (e.g. bush) blocks vertical passage — the balloon can only reach slots below
        /// the lowest blocker. This causes balloons to accumulate under bushes.
        /// </summary>
        private int? FindFirstReachableEmptyRow(int col)
        {
            // Walk from bottom of grid upward — the first non-traversable blocker is
            // the ceiling for this column. Balloons can't pass through it.
            var ceilingRow = -1;
            for (var row = _grid.Rows - 1; row >= 0; row--)
            {
                if (!_grid.IsEmpty(col, row) && !_grid.IsTraversable(col, row))
                {
                    ceilingRow = row;
                    break;
                }
            }

            if (ceilingRow < 0)
            {
                return FindFirstEmptyRowFromTop(col);
            }

            // Search for the topmost empty row below the blocker
            for (var row = ceilingRow + 1; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row))
                {
                    return row;
                }
            }

            return null;
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
            for (var i = 0; i < _balloonsConfig.GameStartedBalloonLines; i++)
            {
                SpawnLineInternal();
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

            var config = BalloonModelConfig.From(entry);

            IWriteableBalloonModel model = entry.BalloonType switch
            {
                BalloonType.Simple => new BalloonModel(config),
                BalloonType.BubbleCluster => new BubbleClusterModel(config, _palette),
                BalloonType.Tough => new ToughBalloonModel(config, _palette),
                BalloonType.Unbreakable => new UnbreakableBalloonModel(config),
                _ => throw new System.ArgumentOutOfRangeException(nameof(entry.BalloonType), entry.BalloonType, null)
            };

            var variant = view.Variant;
            variant.Initialize(model);

            var controller = new BalloonController(model,
                view,
                poolKey,
                () => _activeCounts[poolKey]--,
                entry.HitVfxOverrides,
                _hitSubscriber,
                _itemActivatedSubscriber,
                _boardClearSubscriber,
                _transformCapturedPublisher,
                _deflectedPublisher,
                _nudgePublisher,
                _grid,
                _poolManager,
                _disturbanceField);
            controller.Start();

            _grid.Place(model, view, slot);
            AnimateSpawn(view, _spawnPathBuffer, model);

            _newlySpawnedBalloons.Add(model);
        }

        private void SpawnLine()
        {
            _balancer.Balance();
            SpawnLineInternal();
            PublishItemCheck();
            _balancePublisher.Publish(default);
        }

        private void SpawnLineInternal()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                var firstEmptyRow = FindFirstReachableEmptyRow(col);
                if (!firstEmptyRow.HasValue)
                {
                    continue;
                }

                SpawnBalloon(new Vector2Int(col, firstEmptyRow.Value));
            }
        }

        private async UniTaskVoid SpawnLinesWithDelayAsync(int lineCount, CancellationToken ct, int generation)
        {
            _balancer.Balance();

            for (var i = 0; i < lineCount; i++)
            {
                if (generation != _generation)
                {
                    return;
                }

                SpawnLineInternal();
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
    }
}

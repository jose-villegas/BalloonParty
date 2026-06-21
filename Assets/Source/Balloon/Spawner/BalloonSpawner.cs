using System;
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
        private readonly List<Vector3> _spawnPathBuffer = new();
        private readonly Func<int, Vector2Int?> _resolveOpenEntry;
        private readonly Func<int, Vector2Int?> _resolvePressureOpen;

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
            _disturbanceField = disturbanceField;

            // Cached so the nearest-column scan doesn't allocate a delegate per blocked column.
            _resolveOpenEntry = ResolveOpenEntry;
            _resolvePressureOpen = ResolvePressureOpen;
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
                if (TrySpawnForColumn(col, allowReject))
                {
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

        /// <summary>
        ///     Places this line's balloon for <paramref name="col"/>. It first takes its own column's
        ///     entry; under pressure (turn-driven spawns) a blocked balloon then looks past its column
        ///     — re-homing into the nearest other column that can still accept it, then shoving stable
        ///     balloons aside to open the nearest column it can (using gaps anywhere on the board).
        ///     Only when nothing frees a slot does it fail. Returns whether a balloon was spawned.
        /// </summary>
        private bool TrySpawnForColumn(int col, bool allowReject)
        {
            var ownRow = FindFirstReachableEmptyRow(col);
            if (ownRow.HasValue)
            {
                SpawnBalloon(new Vector2Int(col, ownRow.Value));
                return true;
            }

            // The initial fill never saturates, so only turn spawns search beyond the column.
            if (!allowReject)
            {
                return false;
            }

            if (TryNearestColumn(col, startDistance: 1, _resolveOpenEntry, out var rehome))
            {
                SpawnBalloon(rehome);
                return true;
            }

            if (TryNearestColumn(col, startDistance: 0, _resolvePressureOpen, out var pressured))
            {
                SpawnBalloon(pressured);
                return true;
            }

            return false;
        }

        // Scans columns nearest-first from <paramref name="fromCol"/> (left then right at each
        // distance) and returns the first slot <paramref name="resolve"/> yields. startDistance 0
        // includes the column itself; 1 skips it.
        private bool TryNearestColumn(
            int fromCol,
            int startDistance,
            Func<int, Vector2Int?> resolve,
            out Vector2Int target)
        {
            for (var distance = startDistance; distance < _grid.Columns; distance++)
            {
                if (distance == 0)
                {
                    if (resolve(fromCol) is { } own)
                    {
                        target = own;
                        return true;
                    }

                    continue;
                }

                var left = fromCol - distance;
                if (left >= 0 && resolve(left) is { } leftHit)
                {
                    target = leftHit;
                    return true;
                }

                var right = fromCol + distance;
                if (right < _grid.Columns && resolve(right) is { } rightHit)
                {
                    target = rightHit;
                    return true;
                }
            }

            target = default;
            return false;
        }

        // A column the new balloon can rise straight into.
        private Vector2Int? ResolveOpenEntry(int col)
        {
            var row = FindFirstReachableEmptyRow(col);
            return row.HasValue ? new Vector2Int(col, row.Value) : null;
        }

        // A column pressure balance can shove open by pulling a balloon into a gap anywhere on the board.
        private Vector2Int? ResolvePressureOpen(int col)
        {
            if (!_balancer.TryRelievePressure(col))
            {
                return null;
            }

            var row = FindFirstReachableEmptyRow(col);
            return row.HasValue ? new Vector2Int(col, row.Value) : null;
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

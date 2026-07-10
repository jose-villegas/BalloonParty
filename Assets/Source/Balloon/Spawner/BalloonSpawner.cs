using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Spawner;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Balloon.Spawner
{
    internal class BalloonSpawner : IStartable, IGridSpawner, IRunResettable, IDisposable
    {
        private readonly Dictionary<string, int> _activeCounts = new();
        private readonly BalloonBalancer _balancer;
        private readonly BalloonFactory _factory;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ILevelPacingConfiguration _pacing;
        private readonly CancellationTokenSource _cts = new();
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly SlotGrid _grid;
        private readonly IPublisher<ItemCheckMessage> _itemCheckPublisher;
        private readonly ISubscriber<SpawnBalloonLineMessage> _lineSubscriber;
        private readonly List<IBalloonModel> _newlySpawnedBalloons = new();
        private readonly IObjectResolver _resolver;
        private readonly PoolManager _poolManager;
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
            IActiveLevelParameters levelParams,
            ILevelPacingConfiguration pacing,
            IObjectResolver resolver,
            PoolManager poolManager,
            ISubscriber<SpawnBalloonLineMessage> lineSubscriber,
            BalloonBalancer balancer,
            BalloonFactory factory,
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            IPublisher<ItemCheckMessage> itemCheckPublisher,
            RejectedBalloonEffect rejectedBalloon,
            BalloonPlacementResolver placement)
        {
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _levelParams = levelParams;
            _pacing = pacing;
            _resolver = resolver;
            _poolManager = poolManager;
            _lineSubscriber = lineSubscriber;
            _balancer = balancer;
            _factory = factory;
            _balancePublisher = balancePublisher;
            _destroyedSubscriber = destroyedSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _itemCheckPublisher = itemCheckPublisher;
            _rejectedBalloon = rejectedBalloon;
            _placement = placement;
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

            // Reset per level-up so FirstSpawnTurn is a fresh per-level grace period.
            _levelUpSubscriber.Subscribe(_ => _turnCount = 0);

            PrewarmThenFlagAsync(_cts.Token).Forget();
        }

        public async UniTask SpawnAsync(CancellationToken ct)
        {
            // Re-awaitable, unlike a stored UniTask, so a restart re-spawn can wait on it again.
            await UniTask.WaitUntil(() => _prewarmed, cancellationToken: ct);
            PopulateInitialGrid();
            PublishItemCheck(isInitial: true);
        }

        public void ResetRun(int generation)
        {
            // New generation drops any in-flight delayed line spawns.
            _generation = generation;
            _activeCounts.Clear();
            _turnCount = 0;
            _newlySpawnedBalloons.Clear();
        }

        public void Dispose()
        {
            // The generation guard only covers run resets, not scope teardown.
            _cts.Cancel();
            _cts.Dispose();
        }

        private async UniTaskVoid PrewarmThenFlagAsync(CancellationToken ct)
        {
            await PrewarmAsync(ct);
            _prewarmed = true;
        }

        private async UniTask PrewarmAsync(CancellationToken ct)
        {
            foreach (var entry in _balloonsConfig.Entries)
            {
                // Prewarm to the most this type can reach across all ranges; 0 = no range spawns it.
                var count = _pacing.MaxConcurrentBalloons(entry.BalloonType, _grid.Columns);
                if (count <= 0)
                {
                    continue;
                }

                await _poolManager.PrewarmAsync(entry.PoolKey, count, ct);
            }
        }

        private void OnProjectileDestroyed()
        {
            _turnCount++;
            if (_turnCount < _levelParams.Current.FirstSpawnTurn)
            {
                return;
            }

            SpawnLinesWithDelayAsync(_levelParams.Current.SpawnLines, _cts.Token, _generation).Forget();
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
            // Starts empty and is sized to fit, so it never rejects a balloon.
            for (var i = 0; i < _levelParams.Current.BoardLines; i++)
            {
                SpawnLineInternal(allowReject: false);
            }
        }

        private void PublishItemCheck(bool isInitial)
        {
            if (_newlySpawnedBalloons.Count > 0)
            {
                // Copy — the live buffer is cleared right after, before a deferred subscriber could read it.
                _itemCheckPublisher.Publish(
                    new ItemCheckMessage(new List<IBalloonModel>(_newlySpawnedBalloons), _turnCount, isInitial));
                _newlySpawnedBalloons.Clear();
            }
        }

        private void SpawnBalloon(Vector2Int slot)
        {
            var entry = _levelParams.Current.PickBalloonEntry(_activeCounts);
            if (entry == null)
            {
                Debug.LogWarning(
                    $"BalloonSpawner.SpawnBalloon: PickBalloonEntry returned null for slot ({slot.x},{slot.y}) " +
                    "— every active balloon type is at its max count.");
                return;
            }

            var source = new Vector2Int(slot.x, slot.y + _balloonsConfig.SpawnEntryRowOffset);
            _grid.ComputePath(source, slot, _spawnPathBuffer);
            var poolKey = entry.PoolKey;

            _activeCounts[poolKey] = _activeCounts.GetValueOrDefault(poolKey) + 1;

            var model = _factory.Create(entry, slot, _spawnPathBuffer, () => ReleaseActiveCount(poolKey));
            _newlySpawnedBalloons.Add(model);
        }

        // A balloon can return after a run reset cleared the counts (e.g. the loss→restart outgoing group),
        // so guard the key — its decrement no longer applies to the fresh run.
        private void ReleaseActiveCount(string poolKey)
        {
            if (_activeCounts.TryGetValue(poolKey, out var count))
            {
                _activeCounts[poolKey] = count - 1;
            }
        }

        private void SpawnLine()
        {
            _balancer.Balance();
            SpawnLineInternal(allowReject: true);
            PublishItemCheck(isInitial: false);
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
                    // No room anywhere — queue an overflow balloon below the grid.
                    _rejectedBalloon.Play(col, rejectIndex++, _activeCounts);
                }
            }
        }

        private async UniTaskVoid SpawnLinesWithDelayAsync(int lineCount, CancellationToken ct, int generation)
        {
            // Keeps the overflow hold (thrower lock) across the gaps between lines.
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

                PublishItemCheck(isInitial: false);
                _balancePublisher.Publish(default);
            }
            finally
            {
                _rejectedBalloon.EndSpawnSequence();
            }
        }
    }
}

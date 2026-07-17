using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Spawner;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Balloon.Spawner
{
    internal class BalloonSpawner : IStartable, IGridSpawner, IRunResettable, IDisposable
    {
        private static readonly Comparison<BalloonPrefabEntry> BySpawnWeightAscending =
            (a, b) => a.SpawnWeight - b.SpawnWeight;

        private readonly Dictionary<string, int> _activeCounts = new();
        private readonly Dictionary<string, int> _waveQuotas = new();
        private readonly BalloonBalancer _balancer;
        private readonly BalloonFactory _factory;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ILevelPacingConfiguration _pacing;
        private readonly CancellationTokenSource _cts = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedStartedSubscriber;
        private readonly ISubscriber<ProjectileDoomedEndedMessage> _doomedEndedSubscriber;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
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
        private readonly List<BalloonPrefabEntry> _spawnBatch = new();
        private readonly List<int> _lineColumns = new();
        private readonly List<int> _popSpawnColumns = new();
        private readonly Comparison<int> _byColumnKey;

        private int[] _columnSortKeys;
        private int _turnCount;
        private int _generation;
        private bool _doomedActive;
        private int _batchCursor;
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
            ISubscriber<ProjectileDoomedStartedMessage> doomedStartedSubscriber,
            ISubscriber<ProjectileDoomedEndedMessage> doomedEndedSubscriber,
            ISubscriber<ActorHitMessage> hitSubscriber,
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
            _doomedStartedSubscriber = doomedStartedSubscriber;
            _doomedEndedSubscriber = doomedEndedSubscriber;
            _hitSubscriber = hitSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _itemCheckPublisher = itemCheckPublisher;
            _rejectedBalloon = rejectedBalloon;
            _placement = placement;

            // Cached to avoid a per-sort delegate allocation.
            _byColumnKey = CompareColumnKeys;
        }

        public void Start()
        {
            foreach (var entry in _balloonsConfig.Entries)
            {
                _poolManager.Register(entry.PoolKey,
                    new BalloonPoolChannel(_resolver, entry.Prefab));
            }

            _lineSubscriber.Subscribe(msg => OnSpawnLinesRequested(msg.LineCount)).AddTo(_subscriptions);
            _destroyedSubscriber.Subscribe(_ => OnProjectileDestroyed()).AddTo(_subscriptions);
            _doomedStartedSubscriber.Subscribe(_ => _doomedActive = true).AddTo(_subscriptions);
            _doomedEndedSubscriber.Subscribe(_ => _doomedActive = false).AddTo(_subscriptions);
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);

            // Reset per level-up so FirstSpawnTurn is a fresh per-level grace period.
            _levelUpSubscriber.Subscribe(_ => _turnCount = 0).AddTo(_subscriptions);

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
            _spawnBatch.Clear();
            _batchCursor = 0;
        }

        public void Dispose()
        {
            // The generation guard only covers run resets, not scope teardown.
            _cts.Cancel();
            _cts.Dispose();
            _subscriptions.Dispose();
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

        // Direct projectile pops (never AOE items — DamageFlags.DirectHit) can each earn one extra
        // balloon, spawned immediately when the chance roll passes and the board budget allows it.
        private void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Outcome != HitOutcome.Pop || !msg.Context.Flags.HasFlag(DamageFlags.DirectHit))
            {
                return;
            }

            if (UnityEngine.Random.value < _balloonsConfig.PopSpawnChance && PopSpawnBudget() > 0)
            {
                SpawnLooseBalloons(1);
            }
        }

        private int PopSpawnBudget()
        {
            var capacity = _grid.Columns * (_grid.Rows - _balloonsConfig.PopSpawnFreeRows);
            var occupied = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row))
                    {
                        occupied++;
                    }
                }
            }

            // Also never outpace the pacing itself: at most one turn-wave's worth of headroom counts.
            var waveCap = _levelParams.Current.SpawnLines * _grid.Columns;
            return Mathf.Max(0, Mathf.Min(capacity - occupied, waveCap));
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
            var lines = _levelParams.Current.BoardLines;
            PrepareSpawnBatch(lines, isInitial: true);

            for (var i = 0; i < lines; i++)
            {
                SpawnLineInternal(allowReject: false);
            }

            ReleaseUnspawnedBatch();
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

        // The entry comes from the wave's pre-picked batch; its active count was taken at batch time.
        private void SpawnBalloon(Vector2Int slot, BalloonPrefabEntry entry)
        {
            var source = new Vector2Int(slot.x, slot.y + _balloonsConfig.SpawnEntryRowOffset);
            _grid.ComputePath(source, slot, _spawnPathBuffer);
            var poolKey = entry.PoolKey;

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
            _balancer.Balance(relocateRoamers: true);
            PrepareSpawnBatch(lineCount: 1);
            SpawnLineInternal(allowReject: true);
            ReleaseUnspawnedBatch();
            PublishItemCheck(isInitial: false);
            _balancePublisher.Publish(default);
        }

        private void SpawnLineInternal(bool allowReject)
        {
            // Shallowest columns first: consuming the ascending (light→heavy) batch in depth order pairs
            // the heaviest entries with the lowest slots. Resolve + spawn stay sequential, so each placed
            // balloon is visible to the next column's resolution.
            OrderColumnsByDepth();
            var rejectIndex = 0;

            for (var i = 0; i < _lineColumns.Count; i++)
            {
                var slot = _placement.Resolve(_lineColumns[i], allowReject);
                if (slot.HasValue)
                {
                    var entry = NextBatchEntry();
                    if (entry != null)
                    {
                        SpawnBalloon(slot.Value, entry);
                    }

                    continue;
                }

                if (allowReject)
                {
                    // No room anywhere — queue an overflow balloon below the grid.
                    _rejectedBalloon.Play(_lineColumns[i], rejectIndex++, _activeCounts);
                }
            }
        }

        // Picks the whole wave's entries upfront and orders them lightest-first: earlier (higher) lines
        // spawn the light types and heavier ones enter below — spawn-weight ordering, never a restriction
        // on what spawns. Active counts are taken here so MaxCount holds across the wave, and each type's
        // rolled wave quota (its count-weights curve) caps how many this wave may add.
        private void PrepareSpawnBatch(int lineCount, bool isInitial = false)
        {
            ReleaseUnspawnedBatch();
            _levelParams.Current.RollWaveQuotas(_waveQuotas, isInitial);

            var target = lineCount * _grid.Columns;
            for (var i = 0; i < target; i++)
            {
                var entry = _levelParams.Current.PickBalloonEntry(_activeCounts, _waveQuotas);
                if (entry == null)
                {
                    break;
                }

                _activeCounts[entry.PoolKey] = _activeCounts.GetValueOrDefault(entry.PoolKey) + 1;
                if (_waveQuotas.TryGetValue(entry.PoolKey, out var quota))
                {
                    _waveQuotas[entry.PoolKey] = quota - 1;
                }

                _spawnBatch.Add(entry);
            }

            _spawnBatch.Sort(BySpawnWeightAscending);
        }

        private BalloonPrefabEntry NextBatchEntry()
        {
            return _batchCursor < _spawnBatch.Count ? _spawnBatch[_batchCursor++] : null;
        }

        // Orders the line's columns by their entry-slot depth (topmost first), with a small random
        // tie-break so flat lines still place types horizontally at random. Full columns sort last.
        private void OrderColumnsByDepth()
        {
            _columnSortKeys ??= new int[_grid.Columns];
            _lineColumns.Clear();

            for (var col = 0; col < _grid.Columns; col++)
            {
                var row = _placement.ProbeEntryRow(col) ?? _grid.Rows;
                _columnSortKeys[col] = row * _grid.Columns + UnityEngine.Random.Range(0, _grid.Columns);
                _lineColumns.Add(col);
            }

            _lineColumns.Sort(_byColumnKey);
        }

        private int CompareColumnKeys(int a, int b)
        {
            return _columnSortKeys[a] - _columnSortKeys[b];
        }

        // Hands back the counts of entries the wave never placed (unresolvable columns, aborts).
        private void ReleaseUnspawnedBatch()
        {
            for (var i = _batchCursor; i < _spawnBatch.Count; i++)
            {
                ReleaseActiveCount(_spawnBatch[i].PoolKey);
            }

            _spawnBatch.Clear();
            _batchCursor = 0;
        }

        // Spawns up to count balloons at their columns' own entries — no rehoming, no pressure, no
        // overflow charge; columns without room are skipped. Kept out of the item check: these are
        // per-pop extras, not a wave.
        private void SpawnLooseBalloons(int count)
        {
            _popSpawnColumns.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                _popSpawnColumns.Add(col);
            }

            for (var i = 0; i < _popSpawnColumns.Count - 1; i++)
            {
                var j = UnityEngine.Random.Range(i, _popSpawnColumns.Count);
                (_popSpawnColumns[i], _popSpawnColumns[j]) = (_popSpawnColumns[j], _popSpawnColumns[i]);
            }

            var itemCheckStart = _newlySpawnedBalloons.Count;
            var spawned = 0;
            for (var i = 0; i < _popSpawnColumns.Count && spawned < count; i++)
            {
                var slot = _placement.Resolve(_popSpawnColumns[i], allowReject: false);
                if (!slot.HasValue)
                {
                    continue;
                }

                var entry = _levelParams.Current.PickBalloonEntry(_activeCounts);
                if (entry == null)
                {
                    break;
                }

                _activeCounts[entry.PoolKey] = _activeCounts.GetValueOrDefault(entry.PoolKey) + 1;
                SpawnBalloon(slot.Value, entry);
                spawned++;
            }

            _newlySpawnedBalloons.RemoveRange(itemCheckStart, _newlySpawnedBalloons.Count - itemCheckStart);
        }

        private async UniTaskVoid SpawnLinesWithDelayAsync(int lineCount, CancellationToken ct, int generation)
        {
            // Keeps the overflow hold (thrower lock) across the gaps between lines.
            _rejectedBalloon.BeginSpawnSequence();
            try
            {
                _balancer.Balance(relocateRoamers: true);
                PrepareSpawnBatch(lineCount);

                for (var i = 0; i < lineCount; i++)
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    // Hold new lines out of a shot's doomed 'last breath' — spawning balloons into
                    // the frozen death moment reads wrong. Resume once it ends (or the run moves on).
                    while (_doomedActive && generation == _generation)
                    {
                        await UniTask.Yield(ct);
                    }

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
                // A stale wave must not touch the batch — the reset wiped the counts, and a newer run may
                // already own it.
                if (generation == _generation)
                {
                    ReleaseUnspawnedBatch();
                }

                _rejectedBalloon.EndSpawnSequence();
            }
        }
    }
}

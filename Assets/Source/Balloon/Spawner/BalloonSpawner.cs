using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Diagnostics;
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
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly SlotGrid _grid;
        private readonly IPublisher<ItemCheckMessage> _itemCheckPublisher;
        private readonly IPublisher<WaveDamageMessage> _waveDamagePublisher;
        private readonly ISubscriber<SpawnBalloonLineMessage> _lineSubscriber;
        private readonly List<IBalloonModel> _newlySpawnedBalloons = new();
        private readonly IObjectResolver _resolver;
        private readonly PoolManager _poolManager;
        private readonly RejectedBalloonEffect _rejectedBalloon;
        private readonly BalloonPlacementResolver _placement;
        private readonly ILevelProgress _levelProgress;
        private readonly ISpawnClock _spawnClock;
        private readonly IRunSeed _runSeed;
        private readonly IFormationReinforcements _reinforcements;
        private readonly List<Vector3> _spawnPathBuffer = new();
        private readonly List<BalloonPrefabEntry> _spawnBatch = new();
        private readonly List<int> _lineColumns = new();
        private readonly List<int> _popSpawnColumns = new();
        private readonly List<int> _fillCapacityDiag = new();
        private readonly List<BalloonPrefabEntry> _layerHeavies = new();
        private readonly List<BalloonPrefabEntry> _layerLights = new();
        private readonly Comparison<int> _byColumnKey;
        private readonly Action _releaseReinforcement;
        // The catalogue's bubble, which water lines bring whatever the level generator withholds; null without one.
        private readonly BalloonPrefabEntry _bubbleEntry;
        // The catalogue's unbreakable, which a formation's reinforcements bring whatever the level's mix.
        private readonly BalloonPrefabEntry _unbreakableEntry;

        private int[] _columnSortKeys;
        // Lines spawned this level; waves (ISpawnClockSettings.LinesPerWave) are counted from it.
        private int _linesThisLevel;
        private int _batchCursor;
        private bool _prewarmed;
        // Whether the batch being dealt is a water level's clock line, whose bubbles start small.
        private bool _waterBatch;
        // Called-in unbreakables on the board. They sit outside _activeCounts, so the level's cap still
        // holds for its own, and IUnbreakableFormationSettings.ReinforcementCeiling holds for these.
        private int _reinforcementsAlive;
        // Unbreakables in the batch still to deal that are reinforcements. A counter, not a tag on an entry:
        // the batch is sorted (unstably) after they go in, so the first unbreakables dealt carry the tag,
        // and the totals still come out right because every unbreakable in the batch is the same
        // _unbreakableEntry. Give reinforcements an entry of their own and this has to track entries.
        private int _batchReinforcements;

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
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            IPublisher<ItemCheckMessage> itemCheckPublisher,
            IPublisher<WaveDamageMessage> waveDamagePublisher,
            RejectedBalloonEffect rejectedBalloon,
            BalloonPlacementResolver placement,
            ILevelProgress levelProgress,
            ISpawnClock spawnClock,
            IRunSeed runSeed,
            IFormationReinforcements reinforcements)
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
            _hitSubscriber = hitSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _itemCheckPublisher = itemCheckPublisher;
            _waveDamagePublisher = waveDamagePublisher;
            _rejectedBalloon = rejectedBalloon;
            _placement = placement;
            _levelProgress = levelProgress;
            _spawnClock = spawnClock;
            _runSeed = runSeed;
            _reinforcements = reinforcements;
            _bubbleEntry = FindEntry(balloonsConfig, BalloonType.BubbleCluster);
            _unbreakableEntry = FindEntry(balloonsConfig, BalloonType.Unbreakable);

            // Cached to avoid a per-sort delegate allocation.
            _byColumnKey = CompareColumnKeys;
            _releaseReinforcement = ReleaseReinforcement;
        }

        public void Start()
        {
            foreach (var entry in _balloonsConfig.Entries)
            {
                _poolManager.Register(entry.PoolKey,
                    new BalloonPoolChannel(_resolver, entry.Prefab));
            }

            _lineSubscriber.Subscribe(msg => OnSpawnLinesRequested(msg.LineCount)).AddTo(_subscriptions);
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);

            // Each level's waves, and so its item cadence, count from its own first line.
            _levelUpSubscriber.Subscribe(_ => _linesThisLevel = 0).AddTo(_subscriptions);

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
            _activeCounts.Clear();
            _reinforcementsAlive = 0;
            _batchReinforcements = 0;
            _linesThisLevel = 0;
            _newlySpawnedBalloons.Clear();
            _spawnBatch.Clear();
            _batchCursor = 0;
        }

        public void Dispose()
        {
            // Stops a prewarm still running when the scope tears down.
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
                if (entry == _bubbleEntry)
                {
                    // Water lines bring bubbles even when no level's mix includes them.
                    var waterCap = _pacing.BubbleRise.MaxBubbles;
                    count = Math.Max(count, waterCap > 0 ? waterCap : _grid.Columns);
                }

                if (entry == _unbreakableEntry)
                {
                    count += _balloonsConfig.UnbreakableFormation.ReinforcementCeiling;
                }

                if (count <= 0)
                {
                    continue;
                }

                await _poolManager.PrewarmAsync(entry.PoolKey, count, ct);
            }
        }

        // Direct projectile pops (never AOE items — DamageFlags.DirectHit) can each earn one extra
        // balloon, spawned immediately when the chance roll passes and the board budget allows it.
        private void OnActorHit(ActorHitMessage msg)
        {
            if (_levelProgress.Phase.Value != LevelUpPhase.Playing)
            {
                return;
            }

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

            // Also never outpace the pacing itself: at most one line's worth of headroom counts.
            return Mathf.Max(0, Mathf.Min(capacity - occupied, _grid.Columns));
        }

        // The spawn clock asks for one line per tick; the cheat console may ask for more at once. The pools
        // must be warm first, which the clock's grace normally covers; a zero grace must not race it.
        private void OnSpawnLinesRequested(int lineCount)
        {
            if (!_prewarmed)
            {
                return;
            }

            for (var i = 0; i < lineCount; i++)
            {
                SpawnLine();
            }
        }

        private void PopulateInitialGrid()
        {
            // Static actors are placed in an earlier spawn stage, so the board is NOT empty here on tight
            // boards. Rehoming redistributes a blocked column's allotment into columns that still have
            // reachable room, so the total reaches lines*Columns whenever the board's reachable capacity
            // allows. LogInitialFillDiagnostics reports any remaining shortfall (editor/dev builds only).
            var lines = _levelParams.Current.BoardLines;
            PrepareSpawnBatch(lines, isInitial: true);

            CaptureFillCapacity();

            for (var i = 0; i < lines; i++)
            {
                SpawnLineInternal(PlacementReach.Rehome);
            }

            LogInitialFillDiagnostics(lines);
            ReleaseUnspawnedBatch();
        }

        // Snapshot each column's reachable capacity BEFORE the fill — afterwards the board is full and the
        // probe reads ~0. Stripped from release with LogInitialFillDiagnostics.
        [System.Diagnostics.Conditional("UNITY_ENABLE_CHECKS")]
        private void CaptureFillCapacity()
        {
            _fillCapacityDiag.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                _fillCapacityDiag.Add(_placement.ReachableCapacity(col));
            }
        }

        [System.Diagnostics.Conditional("UNITY_ENABLE_CHECKS")]
        private void LogInitialFillDiagnostics(int lines)
        {
            var expected = lines * _grid.Columns;
            var spawned = _newlySpawnedBalloons.Count;

            var achievable = 0;
            var breakdown = new System.Text.StringBuilder();
            for (var col = 0; col < _fillCapacityDiag.Count; col++)
            {
                var capacity = _fillCapacityDiag[col];
                var placeable = Mathf.Min(lines, capacity);
                achievable += placeable;
                breakdown.Append($"\n  col {col}: reachable={capacity}, placeable={placeable}");
            }

            var summary =
                $"initial fill: expected={expected} (lines={lines}×cols={_grid.Columns}), " +
                $"achievable={achievable}, spawned={spawned}{breakdown}";

            if (spawned < expected)
            {
                Log.Warn(nameof(BalloonSpawner), $"under-filled — {summary}");
            }
            else
            {
                Log.Info(nameof(BalloonSpawner), summary);
            }
        }

        private void PublishItemCheck(bool isInitial, int wave = 0)
        {
            if (_newlySpawnedBalloons.Count > 0)
            {
                // Copy — the live buffer is cleared right after, before a deferred subscriber could read it.
                _itemCheckPublisher.Publish(
                    new ItemCheckMessage(new List<IBalloonModel>(_newlySpawnedBalloons), wave, isInitial));
                _newlySpawnedBalloons.Clear();
            }
        }

        // The entry comes from the batch picked for this fill or line; its active count was taken at batch
        // time. A reinforcement counts toward _reinforcementsAlive instead.
        private void SpawnBalloon(Vector2Int slot, BalloonPrefabEntry entry, bool reinforcement = false)
        {
            var source = new Vector2Int(slot.x, slot.y + _balloonsConfig.SpawnEntryRowOffset);
            _grid.ComputePath(source, slot, _spawnPathBuffer);
            var poolKey = entry.PoolKey;

            if (reinforcement)
            {
                _reinforcementsAlive++;
            }

            var onReturned = reinforcement ? _releaseReinforcement : () => ReleaseActiveCount(poolKey);
            var model = _factory.Create(entry, slot, _spawnPathBuffer, onReturned);
            if (_waterBatch && model is BubbleClusterModel bubble)
            {
                var bubbles = _pacing.BubbleRise.StartBubblesForLevel(_levelProgress.Level.Value);
                bubble.SetBubbles(Mathf.Min(bubbles, _balloonsConfig.BubbleMergeMax));
            }

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

        // Like ReleaseActiveCount, a balloon from before a run reset may return after the count was zeroed.
        private void ReleaseReinforcement()
        {
            _reinforcementsAlive = Mathf.Max(0, _reinforcementsAlive - 1);
        }

        private void SpawnLine()
        {
            // A wave's first line rolls its quotas and moves the roamers; its last line rolls for items,
            // over that line's balloons (earlier lines' may already be popped).
            var linesPerWave = _pacing.SpawnClock.LinesPerWave;
            var waveStart = _linesThisLevel % linesPerWave == 0;
            _linesThisLevel++;
            var waveEnd = _linesThisLevel % linesPerWave == 0;

            _balancer.Balance(atWaveStart: waveStart);

            var deficit = WaveDeficitCalculator.Calculate(
                _grid.CountEmpty(), _grid.Columns, _grid.Columns);

            // A line that costs hearts still spawns, tagged doomed, so the overflow pile has balloons
            // for the strikethrough; the damage goes out after them for the same reason.
            var doomed = deficit.HeartsLost > 0;
            PrepareSpawnBatch(lineCount: 1, rollQuotas: waveStart);
            if (doomed)
            {
                _rejectedBalloon.BeginDoomedLine(0);
            }

            SpawnLineInternal(PlacementReach.Pressure);
            if (doomed)
            {
                _rejectedBalloon.EndDoomedLine();
            }

            ReleaseUnspawnedBatch();
            if (doomed)
            {
                _waveDamagePublisher.Publish(
                    new WaveDamageMessage(deficit.HeartsLost, deficit.UnspawnedSlots, _grid.Columns));
            }

            if (waveEnd)
            {
                PublishItemCheck(isInitial: false, _linesThisLevel / linesPerWave);
            }
            else
            {
                _newlySpawnedBalloons.Clear();
            }

            _balancePublisher.Publish(default);
        }

        private void SpawnLineInternal(PlacementReach reach)
        {
            // Shallowest columns first: consuming the ascending (light→heavy) batch in depth order pairs
            // the heaviest entries with the lowest slots. Resolve + spawn stay sequential, so each placed
            // balloon is visible to the next column's resolution.
            OrderColumnsByDepth();
            var rejectIndex = 0;

            for (var i = 0; i < _lineColumns.Count; i++)
            {
                var slot = _placement.Resolve(_lineColumns[i], reach);
                if (slot.HasValue)
                {
                    var entry = NextBatchEntry();
                    if (entry != null)
                    {
                        var reinforcement = entry == _unbreakableEntry && _batchReinforcements > 0;
                        if (reinforcement)
                        {
                            _batchReinforcements--;
                        }

                        SpawnBalloon(slot.Value, entry, reinforcement);
                    }

                    continue;
                }

                // Only clock lines overflow below the grid; initial fill just stops at the board's capacity.
                if (reach == PlacementReach.Pressure)
                {
                    _rejectedBalloon.Play(_lineColumns[i], rejectIndex++, _activeCounts);
                }
            }
        }

        // Picks every entry of the batch (the initial fill's lines, or one clock line) upfront, then orders
        // them for depth. The plain order is lightest-first (earlier/higher lines take light types, heavier
        // ones enter below); the initial fill instead arranges heavy types into repeating vertical layers
        // (see ArrangeInitialLayers). Neither reorders which types spawn: active counts are taken here so
        // MaxCount holds across the batch, and each type's rolled quota (its count-weights curve) caps how
        // many the batch adds.
        // rollQuotas: false carries the wave's remaining quotas into its next line.
        private void PrepareSpawnBatch(int lineCount, bool isInitial = false, bool rollQuotas = true)
        {
            ReleaseUnspawnedBatch();
            if (isInitial || rollQuotas)
            {
                _levelParams.Current.RollWaveQuotas(_waveQuotas, isInitial);
            }

            var target = lineCount * _grid.Columns;
            var bubbleShare = isInitial ? 0f : WaterBubbleShare();
            _waterBatch = bubbleShare > 0f;

            // Guaranteed entries go first — they bypass the weighted pick and wave quotas. So do the
            // unbreakables a formation called in.
            if (isInitial)
            {
                _levelParams.Current.FillGuaranteedBalloons(_spawnBatch, _activeCounts);
            }
            else
            {
                AddReinforcements(target);
            }

            for (var i = _spawnBatch.Count; i < target; i++)
            {
                var entry = RollWaterBubble(bubbleShare)
                            ?? _levelParams.Current.PickBalloonEntry(_activeCounts, _waveQuotas);
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

            if (isInitial && _balloonsConfig.ToughLayerSpacing >= 2)
            {
                ArrangeInitialLayers(lineCount, _balloonsConfig.ToughLayerSpacing);
            }
            else
            {
                _spawnBatch.Sort(BySpawnWeightAscending);
            }
        }

        // The unbreakables the formations called in at this wave's start. They are the formation's doing, not
        // the level's mix, so they bypass its cap and wave quota and are held to the ceiling instead. An
        // extra-colour level has no threats, so a formation left from the level before calls in nothing there.
        private void AddReinforcements(int target)
        {
            var calls = _reinforcements?.TakeReinforcementCalls() ?? 0;
            if (calls <= 0 || _unbreakableEntry == null || _levelParams.Current.IsExtraColorLevel)
            {
                return;
            }

            var count = ReinforcementPlan.ToAdd(calls, _reinforcementsAlive,
                _balloonsConfig.UnbreakableFormation.ReinforcementCeiling, target - _spawnBatch.Count);
            for (var i = 0; i < count; i++)
            {
                _spawnBatch.Add(_unbreakableEntry);
            }

            _batchReinforcements = count;
        }

        // Reorders the freshly-picked initial batch into vertical layers instead of one bottom-heavy
        // gradient: the board splits into segments `spacing` lines tall, and the bottom line of each
        // segment collects the heavy (positive spawn-weight) types while lighter types fill the rest.
        // Heavy counts are unchanged — only their depth. Approximate on tight boards, where a pass no
        // longer maps cleanly to a single row. Falls back to the plain gradient with no heavies or no
        // full segment. Deal order runs top→bottom, so lights keep their own lightest-first sub-gradient.
        private void ArrangeInitialLayers(int lineCount, int spacing)
        {
            _layerHeavies.Clear();
            _layerLights.Clear();
            foreach (var entry in _spawnBatch)
            {
                (entry.SpawnWeight > 0 ? _layerHeavies : _layerLights).Add(entry);
            }

            var heavyPerLine = InitialLayerPlan.HeavyPerLine(
                _layerHeavies.Count, lineCount, _grid.Columns, spacing);

            var banded = 0;
            foreach (var count in heavyPerLine)
            {
                banded += count;
            }

            if (banded == 0)
            {
                _spawnBatch.Sort(BySpawnWeightAscending);
                return;
            }

            _layerLights.Sort(BySpawnWeightAscending);

            _spawnBatch.Clear();
            var heavyCursor = 0;
            var lightCursor = 0;
            for (var line = 0; line < lineCount; line++)
            {
                var heavyHere = heavyPerLine[line];
                for (var i = 0; i < heavyHere; i++)
                {
                    _spawnBatch.Add(_layerHeavies[heavyCursor++]);
                }

                for (var i = heavyHere; i < _grid.Columns && lightCursor < _layerLights.Count; i++)
                {
                    _spawnBatch.Add(_layerLights[lightCursor++]);
                }
            }

            // Heavies past the layers' capacity sink to the bottom, as the plain gradient would place them.
            while (heavyCursor < _layerHeavies.Count)
            {
                _spawnBatch.Add(_layerHeavies[heavyCursor++]);
            }
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

        // Hands back the counts of entries the batch never placed (unresolvable columns, aborts). A
        // reinforcement never placed took no count, so it has none to hand back.
        private void ReleaseUnspawnedBatch()
        {
            for (var i = _batchCursor; i < _spawnBatch.Count; i++)
            {
                var entry = _spawnBatch[i];
                if (entry == _unbreakableEntry && _batchReinforcements > 0)
                {
                    _batchReinforcements--;
                    continue;
                }

                ReleaseActiveCount(entry.PoolKey);
            }

            _spawnBatch.Clear();
            _batchCursor = 0;
            _batchReinforcements = 0;
        }

        // Spawns up to count balloons at their columns' own entries — no rehoming, no pressure, no
        // overflow charge; columns without room are skipped. Kept out of the item check: these are
        // per-pop extras, not a line, and only ever the simple family so a pop can't gift a tough type.
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
                var slot = _placement.Resolve(_popSpawnColumns[i], PlacementReach.OwnColumn);
                if (!slot.HasValue)
                {
                    continue;
                }

                var entry = _levelParams.Current.PickSimpleBalloonEntry(_activeCounts);
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

        private static BalloonPrefabEntry FindEntry(IBalloonsConfiguration config, BalloonType type)
        {
            foreach (var entry in config.Entries)
            {
                if (entry.BalloonType == type)
                {
                    return entry;
                }
            }

            return null;
        }

        // The share of this line's places that are bubbles: 0 off water, else BubbleRiseCurve at the level's
        // playing time so far.
        private float WaterBubbleShare()
        {
            if (_bubbleEntry == null
                || _pacing.ScenarioForLevel(_levelProgress.Level.Value, _runSeed.Value) != ScenarioKind.Water)
            {
                return 0f;
            }

            return BubbleRiseCurve.ShareAt(_pacing.BubbleRise, _spawnClock.SecondsOnLevel);
        }

        // A bubble for this place, or null for the level's usual pick: rolled against the share, held back once
        // the board has its most bubbles (the count already includes this batch's).
        private BalloonPrefabEntry RollWaterBubble(float share)
        {
            if (share <= 0f)
            {
                return null;
            }

            var cap = _pacing.BubbleRise.MaxBubbles;
            if (cap > 0 && _activeCounts.GetValueOrDefault(_bubbleEntry.PoolKey) >= cap)
            {
                return null;
            }

            return UnityEngine.Random.value < share ? _bubbleEntry : null;
        }
    }
}

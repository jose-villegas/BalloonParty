using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Spawner
{
    public class BalloonSpawner : IStartable
    {
        private readonly Dictionary<string, int> _activeCounts = new();
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly BalloonsConfiguration _balloonsConfig;
        private readonly CancellationTokenSource _cts = new();
        private readonly IPublisher<BalloonDeflectedMessage> _deflectedPublisher;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly IPublisher<ItemCheckMessage> _itemCheckPublisher;
        private readonly ISubscriber<SpawnBalloonLineMessage> _lineSubscriber;
        private readonly List<IBalloonModel> _newlySpawnedBalloons = new();
        private readonly IPublisher<BalloonNudgeMessage> _nudgePublisher;
        private readonly LifetimeScope _parentScope;
        private readonly PoolManager _poolManager;
        private readonly IPublisher<ItemRotationCapturedMessage> _rotationPublisher;

        private int _turnCount;

        [Inject]
        public BalloonSpawner(
            SlotGrid grid,
            BalloonsConfiguration balloonsConfig,
            LifetimeScope parentScope,
            PoolManager poolManager,
            ISubscriber<SpawnBalloonLineMessage> lineSubscriber,
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            IPublisher<ItemCheckMessage> itemCheckPublisher,
            IPublisher<ItemRotationCapturedMessage> rotationPublisher,
            IPublisher<BalloonDeflectedMessage> deflectedPublisher,
            IPublisher<BalloonNudgeMessage> nudgePublisher)
        {
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _parentScope = parentScope;
            _poolManager = poolManager;
            _lineSubscriber = lineSubscriber;
            _balancePublisher = balancePublisher;
            _hitSubscriber = hitSubscriber;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _itemCheckPublisher = itemCheckPublisher;
            _rotationPublisher = rotationPublisher;
            _deflectedPublisher = deflectedPublisher;
            _nudgePublisher = nudgePublisher;
        }

        public void Start()
        {
            foreach (var entry in _balloonsConfig.Entries)
            {
                _poolManager.Register(entry.PoolKey,
                    new BalloonPoolChannel(_parentScope, entry.Prefab));
            }

            _lineSubscriber.Subscribe(msg => OnSpawnLinesRequested(msg.LineCount));
            _destroyedSubscriber.Subscribe(_ => OnProjectileDestroyed());

            PopulateInitialGrid();
            _newlySpawnedBalloons.Clear();
        }

        private void AnimateSpawn(BalloonView view, Vector3 targetPosition, IWriteableBalloonModel model)
        {
            model.IsStable.Value = false;
            view.transform.localScale = Vector3.zero;

            var duration = Random.Range(
                _balloonsConfig.BalloonSpawnAnimationDurationRange.x,
                _balloonsConfig.BalloonSpawnAnimationDurationRange.y);

            view.transform.DOMove(targetPosition, duration)
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

        private void OnProjectileDestroyed()
        {
            _turnCount++;
            if (_turnCount <= 1)
            {
                return;
            }

            SpawnLinesWithDelayAsync(_balloonsConfig.NewProjectileBalloonLines, _cts.Token).Forget();
        }

        private void OnSpawnLinesRequested(int lineCount)
        {
            if (lineCount <= 1)
            {
                SpawnLine();
                return;
            }

            SpawnLinesWithDelayAsync(lineCount, _cts.Token).Forget();
        }

        private void PopulateInitialGrid()
        {
            for (var row = 0; row < _balloonsConfig.GameStartedBalloonLines; row++)
            {
                for (var col = 0; col < _grid.Columns; col++)
                {
                    SpawnBalloon(new Vector2Int(col, row));
                }
            }
        }

        private void PublishItemCheck()
        {
            if (_newlySpawnedBalloons.Count > 0)
            {
                _itemCheckPublisher.Publish(
                    new ItemCheckMessage(_newlySpawnedBalloons.ToArray(), _turnCount));
                _newlySpawnedBalloons.Clear();
            }
        }

        private void SpawnBalloon(Vector2Int slot)
        {
            var entry = _balloonsConfig.PickRandom(_activeCounts);
            if (entry == null)
            {
                // All balloon types are at their max count — skip this slot
                return;
            }

            var targetPosition = _grid.IndexToWorldPosition(slot);
            var spawnPosition = _grid.IndexToWorldPosition(slot + (Vector2Int.up * 4));
            var poolKey = entry.PoolKey;

            _activeCounts[poolKey] = _activeCounts.GetValueOrDefault(poolKey) + 1;

            var view = _poolManager.Get<BalloonView>(poolKey);
            view.transform.position = spawnPosition;

            var model = new BalloonModel();
            model.SlotIndex.Value = slot;
            model.CanHoldItem = entry.CanHoldItem;
            model.HitsRemaining.Value = entry.HitsToPop;

            var variant = view.GetComponentInParent<IBalloonVariant>();
            variant.Initialize(model);

            var controller = new BalloonController(model,
                view,
                poolKey,
                () => _activeCounts[poolKey]--,
                entry.NudgeOverrides,
                entry.PopVfxPrefab,
                _hitSubscriber,
                _itemActivatedSubscriber,
                _rotationPublisher,
                _deflectedPublisher,
                _nudgePublisher,
                _grid,
                _poolManager);
            controller.Start();

            _grid.Place(model, view, slot);
            AnimateSpawn(view, targetPosition, model);

            _newlySpawnedBalloons.Add(model);
        }

        private void SpawnLine()
        {
            SpawnLineInternal();
            PublishItemCheck();
            _balancePublisher.Publish(default);
        }

        private void SpawnLineInternal()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                var firstEmptyRow = FindFirstEmptyRowFromTop(col);
                if (!firstEmptyRow.HasValue)
                {
                    continue;
                }

                SpawnBalloon(new Vector2Int(col, firstEmptyRow.Value));
            }
        }

        private async UniTaskVoid SpawnLinesWithDelayAsync(int lineCount, CancellationToken ct)
        {
            for (var i = 0; i < lineCount; i++)
            {
                SpawnLineInternal();
                await UniTask.Delay(
                    (int)(_balloonsConfig.NewBalloonLinesTimeInterval * 1000),
                    cancellationToken: ct);
            }

            PublishItemCheck();
            _balancePublisher.Publish(default);
        }
    }
}

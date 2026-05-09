using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Shared;
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
        private const string BalloonPoolKey = "Balloon";
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly IGameConfiguration _config;
        private readonly CancellationTokenSource _cts = new();
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly ISubscriber<SpawnBalloonLineMessage> _lineSubscriber;
        private readonly PoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly BalloonSpawnerSettings _settings;
        private int _turnCount;

        [Inject]
        public BalloonSpawner(
            SlotGrid grid,
            BalloonSpawnerSettings settings,
            IGameConfiguration config,
            IObjectResolver resolver,
            ISubscriber<SpawnBalloonLineMessage> lineSubscriber,
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            PoolManager poolManager)
        {
            _grid = grid;
            _settings = settings;
            _config = config;
            _resolver = resolver;
            _lineSubscriber = lineSubscriber;
            _balancePublisher = balancePublisher;
            _hitSubscriber = hitSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _poolManager = poolManager;
        }

        public void Start()
        {
            _poolManager.Register(BalloonPoolKey,
                new BalloonPoolChannel(_settings.BalloonPrefab, _resolver));

            _lineSubscriber.Subscribe(msg => OnSpawnLinesRequested(msg.LineCount));
            _destroyedSubscriber.Subscribe(_ => OnProjectileDestroyed());
        }

        private void SpawnLine()
        {
            SpawnLineInternal();
            _balancePublisher.Publish(default);
        }

        public BalloonController SpawnBalloon(string colorName, Vector2Int slot)
        {
            var targetPosition = _grid.IndexToWorldPosition(slot);
            // grid rows increase downward on screen, so +up*4 is 4 rows below the target
            var spawnPosition = _grid.IndexToWorldPosition(slot + Vector2Int.up * 4);

            var view = _poolManager.Get<BalloonView>(BalloonPoolKey);
            view.transform.position = spawnPosition;

            var model = new BalloonModel();
            model.Color.Value = colorName;
            model.SlotIndex.Value = slot;

            var controller = new BalloonController(model, view, _hitSubscriber, _grid, _config, _poolManager);
            controller.Start();

            _grid.Place(model, slot);
            AnimateSpawn(view, targetPosition, model);

            return controller;
        }

        private void AnimateSpawn(BalloonView view, Vector3 targetPosition, BalloonModel model)
        {
            model.IsStable.Value = false;
            view.transform.localScale = Vector3.zero;

            var duration = Random.Range(
                _config.BalloonSpawnAnimationDurationRange.x,
                _config.BalloonSpawnAnimationDurationRange.y);

            view.transform.DOMove(targetPosition, duration)
                .OnComplete(() => model.IsStable.Value = true);

            view.transform.DOScale(Vector3.one, duration);
        }

        private int? FindFirstEmptyRowFromTop(int col)
        {
            for (var row = 0; row < _grid.Rows; row++)
                if (_grid.IsEmpty(col, row))
                    return row;
            return null;
        }

        private void SpawnLineInternal()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                var firstEmptyRow = FindFirstEmptyRowFromTop(col);
                if (!firstEmptyRow.HasValue) continue;

                SpawnBalloon(_grid.RandomColorName(), new Vector2Int(col, firstEmptyRow.Value));
            }
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

        private void OnProjectileDestroyed()
        {
            _turnCount++;
            if (_turnCount <= 1) return;

            SpawnLinesWithDelayAsync(_config.NewProjectileBalloonLines, _cts.Token).Forget();
        }

        private async UniTaskVoid SpawnLinesWithDelayAsync(int lineCount, CancellationToken ct)
        {
            for (var i = 0; i < lineCount; i++)
            {
                SpawnLineInternal();
                await UniTask.Delay(
                    (int)(_config.NewBalloonLinesTimeInterval * 1000),
                    cancellationToken: ct);
            }

            _balancePublisher.Publish(default);
        }
    }
}
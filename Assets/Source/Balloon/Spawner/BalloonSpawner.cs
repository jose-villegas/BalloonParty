using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;

namespace BalloonParty.Balloon.Spawner
{
    public class BalloonSpawner : IStartable
    {
        private readonly SlotGrid _grid;
        private readonly BalloonSpawnerSettings _settings;
        private readonly IGameConfiguration _config;
        private readonly IObjectResolver _resolver;
        private readonly ISubscriber<SpawnBalloonLineMessage> _lineSubscriber;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;

        [Inject]
        public BalloonSpawner(
            SlotGrid grid,
            BalloonSpawnerSettings settings,
            IGameConfiguration config,
            IObjectResolver resolver,
            ISubscriber<SpawnBalloonLineMessage> lineSubscriber,
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            ISubscriber<BalloonHitMessage> hitSubscriber)
        {
            _grid = grid;
            _settings = settings;
            _config = config;
            _resolver = resolver;
            _lineSubscriber = lineSubscriber;
            _balancePublisher = balancePublisher;
            _hitSubscriber = hitSubscriber;
        }

        public void Start()
        {
            _lineSubscriber.Subscribe(_ => SpawnLine());
        }

        public void SpawnLine()
        {
            for (int col = 0; col < _grid.Columns; col++)
            {
                var firstEmptyRow = FindFirstEmptyRowFromTop(col);
                if (!firstEmptyRow.HasValue) continue;

                SpawnBalloon(_grid.RandomColorName(), new Vector2Int(col, firstEmptyRow.Value));
            }

            _balancePublisher.Publish(default);
        }

        public BalloonController SpawnBalloon(string colorName, Vector2Int slot)
        {
            var targetPosition = _grid.IndexToWorldPosition(slot);
            // grid rows increase downward on screen, so +up*4 is 4 rows below the target
            var spawnPosition = _grid.IndexToWorldPosition(slot + Vector2Int.up * 4);

            var instance = _resolver.Instantiate(_settings.BalloonPrefab, spawnPosition, Quaternion.identity);
            var view = instance.GetComponent<BalloonView>();

            var model = new BalloonModel();
            model.Color.Value = colorName;
            model.SlotIndex.Value = slot;

            var controller = new BalloonController(model, view, _hitSubscriber, _balancePublisher, _grid, _config);
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

            view.transform.DOMove(targetPosition, duration);
            view.transform.DOScale(Vector3.one, duration)
                .OnComplete(() => model.IsStable.Value = true);
        }

        private int? FindFirstEmptyRowFromTop(int col)
        {
            for (int row = 0; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row))
                    return row;
            }
            return null;
        }
    }
}


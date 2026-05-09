using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    public class BalloonController
    {
        public BalloonModel Model { get; }

        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly IGameConfiguration _config;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly BalloonView _view;
        private readonly PoolManager _poolManager;

        private IDisposable _hitSubscription;

        public BalloonController(BalloonModel model, BalloonView view,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            SlotGrid grid,
            IGameConfiguration config,
            PoolManager poolManager)
        {
            Model = model;
            _view = view;
            _hitSubscriber = hitSubscriber;
            _balancePublisher = balancePublisher;
            _grid = grid;
            _config = config;
            _poolManager = poolManager;
        }

        public void Start()
        {
            Model.View = _view;
            _view.Bind(Model);

            _hitSubscription = _hitSubscriber.Subscribe(msg =>
            {
                if (msg.Balloon != Model) return;

                _hitSubscription?.Dispose();
                _hitSubscription = null;

                _view.PlayPopEffect(_config.BalloonColor(Model.Color.Value));
                _grid.Remove(Model.SlotIndex.Value);
                _poolManager.Return("Balloon", _view);
                _balancePublisher.Publish(default);
            });

            _view.RegisterDisposeOnDespawn(_hitSubscription);
        }
    }
}

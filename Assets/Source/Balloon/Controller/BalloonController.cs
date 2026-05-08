using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;

namespace BalloonParty.Balloon.Controller
{
    public class BalloonController : IStartable
    {
        private readonly BalloonModel _model;
        private readonly BalloonView _view;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly SlotGrid _grid;
        private readonly IGameConfiguration _config;

        [Inject]
        public BalloonController(BalloonModel model, BalloonView view,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            SlotGrid grid,
            IGameConfiguration config)
        {
            _model = model;
            _view = view;
            _hitSubscriber = hitSubscriber;
            _balancePublisher = balancePublisher;
            _grid = grid;
            _config = config;
        }

        public void Start()
        {
            _model.View = _view;
            _view.Bind(_model);

            _hitSubscriber.Subscribe(msg =>
            {
                if (msg.Balloon != _model) return;
                _view.PlayPopEffect(_config.BalloonColor(_model.Color.Value));
                _grid.Remove(_model.SlotIndex.Value);
                Object.Destroy(_view.gameObject);
                _balancePublisher.Publish(default);
            });
        }

        public BalloonModel Model => _model;
    }
}
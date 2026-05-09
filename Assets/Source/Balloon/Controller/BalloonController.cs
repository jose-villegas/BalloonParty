using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    public class BalloonController : IStartable
    {
        public BalloonModel Model { get; }

        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly IGameConfiguration _config;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly BalloonView _view;

        [Inject]
        public BalloonController(BalloonModel model, BalloonView view,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            IPublisher<BalanceBalloonsMessage> balancePublisher,
            SlotGrid grid,
            IGameConfiguration config)
        {
            Model = model;
            _view = view;
            _hitSubscriber = hitSubscriber;
            _balancePublisher = balancePublisher;
            _grid = grid;
            _config = config;
        }

        public void Start()
        {
            Model.View = _view;
            _view.Bind(Model);

            _hitSubscriber.Subscribe(msg =>
            {
                if (msg.Balloon != Model) return;
                _view.PlayPopEffect(_config.BalloonColor(Model.Color.Value));
                _grid.Remove(Model.SlotIndex.Value);
                Object.Destroy(_view.gameObject);
                _balancePublisher.Publish(default);
            });
        }
    }
}

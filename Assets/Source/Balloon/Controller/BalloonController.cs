#region

using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;

#endregion

namespace BalloonParty.Balloon.Controller
{
    public class BalloonController
    {
        private readonly IGameConfiguration _config;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly IWriteableBalloonModel _model;
        private readonly PoolManager _poolManager;
        private readonly BalloonView _view;

        private IDisposable _hitSubscription;

        public IBalloonModel Model => _model;

        public BalloonController(
            IWriteableBalloonModel model,
            BalloonView view,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            SlotGrid grid,
            IGameConfiguration config,
            PoolManager poolManager)
        {
            _model = model;
            _view = view;
            _hitSubscriber = hitSubscriber;
            _grid = grid;
            _config = config;
            _poolManager = poolManager;
        }


        public void Start()
        {
            _view.Bind(_model);

            _hitSubscription = _hitSubscriber.Subscribe(msg =>
            {
                if (msg.Balloon != Model)
                {
                    return;
                }

                _hitSubscription?.Dispose();
                _hitSubscription = null;

                _view.PlayPopEffect(_config.BalloonColor(Model.Color.Value));
                _grid.Remove(Model.SlotIndex.Value);
                _poolManager.Return("Balloon", _view);
            });

            _view.RegisterDisposeOnDespawn(_hitSubscription);
        }
    }
}

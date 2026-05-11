#region

using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
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
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly IPublisher<ItemRotationCapturedMessage> _rotationPublisher;
        private readonly IWriteableBalloonModel _model;
        private readonly PoolManager _poolManager;
        private readonly BalloonView _view;

        private IDisposable _hitSubscription;
        private IDisposable _itemActivatedSubscription;

        public BalloonController(
            IWriteableBalloonModel model,
            BalloonView view,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            IPublisher<ItemRotationCapturedMessage> rotationPublisher,
            SlotGrid grid,
            IGameConfiguration config,
            PoolManager poolManager)
        {
            _model = model;
            _view = view;
            _hitSubscriber = hitSubscriber;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _rotationPublisher = rotationPublisher;
            _grid = grid;
            _config = config;
            _poolManager = poolManager;
        }

        public void Start()
        {
            _view.Bind(_model);

            _hitSubscription = _hitSubscriber.Subscribe(msg =>
            {
                if (msg.Balloon != _model)
                {
                    return;
                }

                _hitSubscription?.Dispose();
                _hitSubscription = null;

                _view.PlayPopEffect(_config.BalloonColor(_model.Color.Value));
                _grid.Remove(_model.SlotIndex.Value);

                if (_model.Item.Value == ItemType.None)
                {
                    _poolManager.Return("Balloon", _view);
                }
                else
                {
                    // Snapshot item visual rotation before hiding — the laser handler
                    // reads this from the model after the visual is gone.
                    var laserRotation = _view.GetComponentInChildren<Item.LaserItemRotation>(true);
                    if (laserRotation != null)
                    {
                        laserRotation.Stop();
                        _rotationPublisher.Publish(new ItemRotationCapturedMessage(laserRotation.transform.rotation));
                    }

                    // Hide immediately — item effect plays world-space; balloon visual
                    // and collider must not persist while we wait for activation to finish.
                    _view.Hide();

                    _itemActivatedSubscription = _itemActivatedSubscriber.Subscribe(activatedMsg =>
                    {
                        if (activatedMsg.Balloon != _model)
                        {
                            return;
                        }

                        _itemActivatedSubscription?.Dispose();
                        _itemActivatedSubscription = null;
                        _poolManager.Return("Balloon", _view);
                    });

                    _view.RegisterDisposeOnDespawn(_itemActivatedSubscription);
                }
            });

            _view.RegisterDisposeOnDespawn(_hitSubscription);
        }
    }
}

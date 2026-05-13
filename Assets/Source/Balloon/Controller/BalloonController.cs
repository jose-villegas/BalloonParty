using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;

namespace BalloonParty.Balloon.Controller
{
    public class BalloonController
    {
        private readonly IGameConfiguration _config;
        private readonly GamePalette _palette;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly IWriteableBalloonModel _model;
        private readonly PoolManager _poolManager;
        private readonly IPublisher<ItemRotationCapturedMessage> _rotationPublisher;
        private readonly IPublisher<BalloonDeflectedMessage> _deflectedPublisher;
        private readonly IPublisher<BalloonNudgeMessage> _nudgePublisher;
        private readonly BalloonView _view;

        private IDisposable _hitSubscription;
        private IDisposable _itemActivatedSubscription;

        public BalloonController(
            IWriteableBalloonModel model,
            BalloonView view,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            IPublisher<ItemRotationCapturedMessage> rotationPublisher,
            IPublisher<BalloonDeflectedMessage> deflectedPublisher,
            IPublisher<BalloonNudgeMessage> nudgePublisher,
            SlotGrid grid,
            IGameConfiguration config,
            GamePalette palette,
            PoolManager poolManager)
        {
            _model = model;
            _view = view;
            _hitSubscriber = hitSubscriber;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _rotationPublisher = rotationPublisher;
            _deflectedPublisher = deflectedPublisher;
            _nudgePublisher = nudgePublisher;
            _grid = grid;
            _config = config;
            _palette = palette;
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

                var hitsRemaining = _model.HitsRemaining.Value;

                // Unbreakable — deflect, never pop
                if (hitsRemaining == -1)
                {
                    Deflect(msg);
                    return;
                }

                // Tough — decrement and deflect, not yet popping
                if (hitsRemaining > 1)
                {
                    _model.HitsRemaining.Value--;
                    Deflect(msg);
                    return;
                }

                // Normal / last hit — pop
                Pop();
            });

            _view.RegisterDisposeOnDespawn(_hitSubscription);
        }

        private void Deflect(BalloonHitMessage msg)
        {
            var balloonWorldPos = _grid.IndexToWorldPosition(_model.SlotIndex.Value);
            _deflectedPublisher.Publish(new BalloonDeflectedMessage(_model, balloonWorldPos, msg.ProjectileDirection));

            // Pushback nudge — balloon moves in the direction the projectile was traveling
            _nudgePublisher.Publish(new BalloonNudgeMessage(_model, balloonWorldPos - (Vector3)msg.ProjectileDirection.normalized));
        }

        private void Pop()
        {
            _hitSubscription?.Dispose();
            _hitSubscription = null;

            if (!string.IsNullOrEmpty(_model.Color.Value))
            {
                _view.PlayPopEffect(_palette.GetColor(_model.Color.Value));
            }

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
        }
    }
}

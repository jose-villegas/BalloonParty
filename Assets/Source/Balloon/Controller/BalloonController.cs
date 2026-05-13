using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    public class BalloonController
    {
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly IWriteableBalloonModel _model;
        private readonly PoolManager _poolManager;
        private readonly IPublisher<ItemRotationCapturedMessage> _rotationPublisher;
        private readonly IPublisher<BalloonDeflectedMessage> _deflectedPublisher;
        private readonly IPublisher<BalloonNudgeMessage> _nudgePublisher;
        private readonly BalloonView _view;
        private readonly string _poolKey;
        private readonly Action _onReturned;
        private readonly NudgeOverride[] _nudgeOverrides;
        private readonly ParticleSystem _popVfxOverride;

        private IDisposable _hitSubscription;
        private IDisposable _itemActivatedSubscription;

        public BalloonController(
            IWriteableBalloonModel model,
            BalloonView view,
            string poolKey,
            Action onReturned,
            NudgeOverride[] nudgeOverrides,
            ParticleSystem popVfxOverride,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            IPublisher<ItemRotationCapturedMessage> rotationPublisher,
            IPublisher<BalloonDeflectedMessage> deflectedPublisher,
            IPublisher<BalloonNudgeMessage> nudgePublisher,
            SlotGrid grid,
            PoolManager poolManager)
        {
            _model = model;
            _view = view;
            _poolKey = poolKey;
            _onReturned = onReturned;
            _nudgeOverrides = nudgeOverrides;
            _popVfxOverride = popVfxOverride;
            _hitSubscriber = hitSubscriber;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _rotationPublisher = rotationPublisher;
            _deflectedPublisher = deflectedPublisher;
            _nudgePublisher = nudgePublisher;
            _grid = grid;
            _poolManager = poolManager;
        }

        public void Start()
        {
            _model.NudgeOverrides = _nudgeOverrides;

            if (_popVfxOverride != null)
            {
                _view.SetPopVfxOverride(_popVfxOverride);
            }

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

            // Pushback nudge — BalloonView will apply model-level overrides automatically
            _nudgePublisher.Publish(new BalloonNudgeMessage(
                _model,
                balloonWorldPos - msg.ProjectileDirection.normalized,
                NudgeType.Deflect));
        }

        private void Pop()
        {
            _hitSubscription?.Dispose();
            _hitSubscription = null;

            _view.PlayPopEffect();

            _grid.Remove(_model.SlotIndex.Value);

            if (_model.Item.Value == ItemType.None)
            {
                _onReturned?.Invoke();
                _poolManager.Return(_poolKey, _view);
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
                    _onReturned?.Invoke();
                    _poolManager.Return(_poolKey, _view);
                });

                _view.RegisterDisposeOnDespawn(_itemActivatedSubscription);
            }
        }
    }
}

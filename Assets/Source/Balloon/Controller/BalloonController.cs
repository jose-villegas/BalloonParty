using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    internal class BalloonController
    {
        private readonly IPublisher<BalloonDeflectedMessage> _deflectedPublisher;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly IWriteableBalloonModel _model;
        private readonly IPublisher<BalloonNudgeMessage> _nudgePublisher;
        private readonly Action _onReturned;
        private readonly string _poolKey;
        private readonly PoolManager _poolManager;
        private readonly ParticleSystem _popVfxOverride;
        private readonly IPublisher<TransformCapturedMessage> _transformCapturedPublisher;
        private readonly BalloonView _view;

        private IDisposable _hitSubscription;
        private IDisposable _itemActivatedSubscription;

        public BalloonController(
            IWriteableBalloonModel model,
            BalloonView view,
            string poolKey,
            Action onReturned,
            ParticleSystem popVfxOverride,
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            IPublisher<TransformCapturedMessage> transformCapturedPublisher,
            IPublisher<BalloonDeflectedMessage> deflectedPublisher,
            IPublisher<BalloonNudgeMessage> nudgePublisher,
            SlotGrid grid,
            PoolManager poolManager)
        {
            _model = model;
            _view = view;
            _poolKey = poolKey;
            _onReturned = onReturned;
            _popVfxOverride = popVfxOverride;
            _hitSubscriber = hitSubscriber;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _transformCapturedPublisher = transformCapturedPublisher;
            _deflectedPublisher = deflectedPublisher;
            _nudgePublisher = nudgePublisher;
            _grid = grid;
            _poolManager = poolManager;
        }

        public void Start()
        {
            if (_popVfxOverride != null)
            {
                _view.SetPopVfxOverride(_popVfxOverride);
            }

            _view.Bind(_model);

            _hitSubscription = _hitSubscriber.Subscribe(OnActorHit);
            _view.RegisterDisposeOnDespawn(_hitSubscription);
        }

        private void Deflect(ActorHitMessage msg)
        {
            var balloonWorldPos = _grid.IndexToWorldPosition(_model.SlotIndex.Value);
            _deflectedPublisher.Publish(new BalloonDeflectedMessage(_model, balloonWorldPos, msg.ProjectileDirection));

            _nudgePublisher.Publish(new BalloonNudgeMessage(
                _model,
                balloonWorldPos - msg.ProjectileDirection.normalized,
                NudgeType.Deflect));
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Actor is not IBalloonModel balloon || !ReferenceEquals(balloon, _model))
            {
                return;
            }

            switch (msg.Outcome)
            {
                case HitOutcome.Pop:
                    Pop();
                    break;
                case HitOutcome.PassThrough:
                    // Projectile continues; balloon stays with reduced HitsRemaining.
                    // Crack animation is driven reactively by HitsRemaining subscription.
                    break;
                case HitOutcome.Deflect:
                    Deflect(msg);
                    break;
                // HitOutcome.Absorb: projectile force-kill routed through ProjectileView — Phase 9.
            }
        }

        private void OnItemActivated(ItemActivatedMessage msg)
        {
            if (msg.Balloon != _model)
            {
                return;
            }

            _itemActivatedSubscription?.Dispose();
            _itemActivatedSubscription = null;
            _onReturned?.Invoke();
            _poolManager.Return(_poolKey, _view);
        }

        private void Pop()
        {
            _hitSubscription?.Dispose();
            _hitSubscription = null;

            _view.PlayPopEffect();
            _grid.Remove(_model.SlotIndex.Value);

            if (_model is not IHasItemSlot itemSlot || itemSlot.Item.Value == ItemType.None)
            {
                _onReturned?.Invoke();
                _poolManager.Return(_poolKey, _view);
            }
            else
            {
                var transformCapture = _view.TransformCapture;
                if (transformCapture != null)
                {
                    var snapshot = transformCapture.CaptureSnapshot();
                    _transformCapturedPublisher.Publish(new TransformCapturedMessage(_model, snapshot));
                }

                // Hide immediately — item effect plays world-space; balloon visual
                // and collider must not persist while we wait for activation to finish.
                _view.Hide();

                _itemActivatedSubscription = _itemActivatedSubscriber.Subscribe(OnItemActivated);
                _view.RegisterDisposeOnDespawn(_itemActivatedSubscription);
            }
        }
    }
}

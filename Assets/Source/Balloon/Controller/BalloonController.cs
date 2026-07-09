using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Balloon.Controller
{
    internal class BalloonController
    {
        private readonly IPublisher<BalloonDeflectedMessage> _deflectedPublisher;
        private readonly SlotGrid _grid;
        private readonly HitVfxOverride[] _hitVfxOverrides;
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly IWriteableBalloonModel _model;
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly Action _onReturned;
        private readonly string _poolKey;
        private readonly PoolManager _poolManager;
        private readonly BalloonControllerRegistry _registry;
        private readonly IPublisher<TransformCapturedMessage> _transformCapturedPublisher;
        private readonly BalloonView _view;
        private readonly DisturbanceFieldService _disturbanceField;

        private IDisposable _itemActivatedSubscription;
        private bool _popped;

        public BalloonController(
            IWriteableBalloonModel model,
            BalloonView view,
            string poolKey,
            Action onReturned,
            HitVfxOverride[] hitVfxOverrides,
            BalloonControllerContext context)
        {
            _model = model;
            _view = view;
            _poolKey = poolKey;
            _onReturned = onReturned;
            _hitVfxOverrides = hitVfxOverrides;
            _itemActivatedSubscriber = context.ItemActivatedSubscriber;
            _registry = context.Registry;
            _transformCapturedPublisher = context.TransformCapturedPublisher;
            _deflectedPublisher = context.DeflectedPublisher;
            _nudgePublisher = context.NudgePublisher;
            _grid = context.Grid;
            _poolManager = context.PoolManager;
            _disturbanceField = context.DisturbanceField;
        }

        public void Start()
        {
            _view.SetHitVfxOverrides(_hitVfxOverrides);
            _view.Bind(_model);

            _registry.Register(_model, this);
        }

        // Invoked by BalloonControllerRegistry.Route; no self-filtering needed here.
        internal void HandleHit(ActorHitMessage msg)
        {
            if (_popped)
            {
                return;
            }

            switch (msg.Outcome)
            {
                case HitOutcome.Pop:
                    Pop();
                    break;
                case HitOutcome.Deflect:
                    Deflect(msg);
                    break;
                case HitOutcome.PassThrough:
                    _view.PlayHitVfxForOutcome(HitOutcome.PassThrough);
                    break;
                case HitOutcome.Absorb:
                    break;
            }
        }

        // playPopVfx is true for the level-transition Ascent, false for a silent run-restart clear.
        internal void HandleBoardClear(bool playPopVfx)
        {
            _itemActivatedSubscription?.Dispose();
            _itemActivatedSubscription = null;

            if (playPopVfx)
            {
                _disturbanceField.Stamp(StampSource.BalloonPop, _view.transform.position, Vector2.zero);

                // Parent the pop VFX under whatever the view rides, so it moves with the level transition.
                _view.PlayHitVfxForOutcome(HitOutcome.Pop, _view.transform.parent);
            }

            var slot = _model.SlotIndex.Value;
            if (ReferenceEquals(_grid.At(slot), _model))
            {
                _grid.Remove(slot);
            }

            _onReturned?.Invoke();
            _poolManager.Return(_poolKey, _view);
        }

        // Detaches this balloon into a level transition's outgoing "old level" group: vacates its grid slot
        // and reparents the view under the outgoing root so it travels with the transition. The caller
        // animates the returned view, then hands it back to the pool via ReturnToPool.
        internal ISlotActorView DetachForOutgoing(Transform outgoingRoot, float exitDrop)
        {
            _itemActivatedSubscription?.Dispose();
            _itemActivatedSubscription = null;

            var slot = _model.SlotIndex.Value;
            if (ReferenceEquals(_grid.At(slot), _model))
            {
                _grid.Remove(slot);
            }

            // Stop the idle animator so the float-away's tilt tween owns the balloon's rotation.
            _view.SuspendAnimator();

            // Keep world position, then drop local by the root's lift height, so the balloon holds its
            // original spot once the descent lifts the root to that height (same trick the statics use).
            _view.transform.SetParent(outgoingRoot, worldPositionStays: true);
            var local = _view.transform.localPosition;
            local.y -= exitDrop;
            _view.transform.localPosition = local;
            return _view;
        }

        internal void ReturnToPool()
        {
            _onReturned?.Invoke();
            _poolManager.Return(_poolKey, _view);
        }

        private void Deflect(ActorHitMessage msg)
        {
            var balloonWorldPos = _grid.IndexToWorldPosition(_model.SlotIndex.Value);
            _deflectedPublisher.Publish(new BalloonDeflectedMessage(_model, balloonWorldPos, msg.ProjectileDirection));

            _nudgePublisher.Publish(new NudgeMessage(
                _model,
                balloonWorldPos - msg.ProjectileDirection.normalized,
                NudgeType.Deflect));
        }

        private void OnItemActivated(ItemActivatedMessage msg)
        {
            if (msg.Balloon != _model)
            {
                return;
            }

            _itemActivatedSubscription?.Dispose();
            _itemActivatedSubscription = null;
            _registry.Unregister(_model);
            _onReturned?.Invoke();
            _poolManager.Return(_poolKey, _view);
        }

        private void Pop()
        {
            _popped = true;

            var popWorldPos = _view.transform.position;
            _disturbanceField.Stamp(StampSource.BalloonPop, popWorldPos, Vector2.zero);

            _view.PlayHitVfxForOutcome(HitOutcome.Pop);
            _grid.Remove(_model.SlotIndex.Value);

            if (_model is not IHasItemSlot itemSlot || itemSlot.Item.Value == ItemType.None)
            {
                _registry.Unregister(_model);
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

                // Hide immediately; stay registered so a board clear during the wait still tears us down.
                _view.Hide();

                _itemActivatedSubscription = _itemActivatedSubscriber.Subscribe(OnItemActivated);
                _view.RegisterDisposeOnDespawn(_itemActivatedSubscription);
            }
        }
    }
}

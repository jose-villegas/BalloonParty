using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Disturbance;
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

        // Invoked by BalloonControllerRegistry.Route — the registry resolves the owning
        // controller by model, so no self-filtering is needed here.
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

        // Invoked by the registry's single board-clear pass. Popped-but-waiting item balloons
        // are still registered, so their pending activation subscription is cleaned up here too.
        // playPopVfx is true for the level-transition Ascent (visible burst), false for a silent
        // run-restart clear.
        internal void HandleBoardClear(bool playPopVfx)
        {
            _itemActivatedSubscription?.Dispose();
            _itemActivatedSubscription = null;

            if (playPopVfx)
            {
                _disturbanceField.Stamp(StampSource.BalloonPop, _view.transform.position, Vector2.zero);
                _view.PlayHitVfxForOutcome(HitOutcome.Pop);
            }

            var slot = _model.SlotIndex.Value;
            if (ReferenceEquals(_grid.At(slot), _model))
            {
                _grid.Remove(slot);
            }

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

                // Hide immediately — item effect plays world-space; balloon visual
                // and collider must not persist while we wait for activation to finish.
                // Stay registered so a board clear during the wait still tears us down.
                _view.Hide();

                _itemActivatedSubscription = _itemActivatedSubscriber.Subscribe(OnItemActivated);
                _view.RegisterDisposeOnDespawn(_itemActivatedSubscription);
            }
        }
    }
}

using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Scenario;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Configuration.Balloons;

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
        private readonly string _poolKey;
        private readonly PoolManager _poolManager;
        private readonly BalloonControllerRegistry _registry;
        private readonly IPublisher<TransformCapturedMessage> _transformCapturedPublisher;
        private readonly BalloonView _view;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly SmokeFieldService _smokeField;
        private readonly IGamePalette _palette;
        private readonly BalloonPopPresenter _popPresenter;

        private Action _onReturned;
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
            _smokeField = context.SmokeField;
            _palette = context.Palette;
            _popPresenter = context.PopPresenter;
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
            LifecycleHelper.DisposeAndClear(ref _itemActivatedSubscription);

            if (playPopVfx)
            {
                // Parent the pop VFX under whatever the view rides, so it moves with the level transition.
                _popPresenter.Present(
                    _model, _view.transform.position, _view, _view.transform.parent);
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
            LifecycleHelper.DisposeAndClear(ref _itemActivatedSubscription);

            // A detached balloon is out of play — free its type count NOW (nulled so the eventual pool
            // return can't double-release), or the next level's MaxCount picks see phantom occupants.
            _onReturned?.Invoke();
            _onReturned = null;

            var slot = _model.SlotIndex.Value;
            if (ReferenceEquals(_grid.At(slot), _model))
            {
                _grid.Remove(slot);
            }

            // A detached balloon must carry no live tweens into the cinematic: a balance sequence still
            // playing on the tracker would be mutated by the float-away's Append (corrupting DOTween's
            // active-tween array), and stray transform tweens would fight the float animation.
            _view.TweenTracker.Kill();
            _view.transform.DOKill();

            // Stop the idle animator so the float-away's tilt tween owns the balloon's rotation.
            _view.SuspendAnimator();

            // Freeze visual state — stop reacting to level-up colour changes so outgoing rainbow
            // balloons keep their old-level band colours instead of popping to the new palette.
            _view.FreezeVisualState();

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
            var viewPos = _view.ContactCenter;
            var slotWorldPos = _grid.IndexToWorldPosition(_model.SlotIndex.Value);

            // Projectile reflection needs the visual position — the ball bounced off the view, not
            // the (potentially different) model-slot position during a mid-balance move. The
            // collider's centre specifically, so an offset one would still reflect where it is drawn.
            _deflectedPublisher.Publish(
                new BalloonDeflectedMessage(_model, viewPos, msg.ProjectileDirection, _view.ContactRadius));

            if (_model is IHasDeflectStamp stamper && stamper.DeflectStampScale > 0f)
            {
                var deflectPaletteIndex = _palette.PaletteIndexOf(_model.GetImpactColorId());

                _disturbanceField.Stamp(
                    StampSource.BalloonDeflect, viewPos, Vector2.zero, stamper.DeflectStampScale,
                    deflectPaletteIndex);

                if (_model is ToughBalloonModel)
                {
                    _smokeField.Paint(PaintSource.ToughDeflect, viewPos);
                }
            }

            // NudgeMessage origin uses the slot world position: NudgeService computes direction as
            // slotPos − origin, which yields the projectile direction only when origin = slotPos − dir.
            _nudgePublisher.Publish(new NudgeMessage(
                _model,
                slotWorldPos - msg.ProjectileDirection.normalized,
                NudgeType.Deflect));
        }

        private void OnItemActivated(ItemActivatedMessage msg)
        {
            if (msg.Balloon != _model)
            {
                return;
            }

            LifecycleHelper.DisposeAndClear(ref _itemActivatedSubscription);
            _registry.Unregister(_model);
            _onReturned?.Invoke();
            _poolManager.Return(_poolKey, _view);
        }

        private void Pop()
        {
            _popped = true;

            _popPresenter.Present(_model, _view.transform.position, _view);
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

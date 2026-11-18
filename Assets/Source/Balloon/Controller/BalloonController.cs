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
        private readonly PopPulse _popPulse;
        private readonly Action _presentPop;

        private Action _onReturned;
        private IDisposable _itemActivatedSubscription;
        private bool _popped;
        private Vector3 _popDirection;

        public BalloonController(
            IWriteableBalloonModel model,
            BalloonView view,
            string poolKey,
            Action onReturned,
            BalloonPrefabEntry entry,
            BalloonControllerContext context)
        {
            _model = model;
            _view = view;
            _poolKey = poolKey;
            _onReturned = onReturned;
            _hitVfxOverrides = entry?.HitVfxOverrides;
            _popPulse = entry?.PopPulse;
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
            // Cached, so a pop allocates no callback for a prelude it may not have.
            _presentPop = PresentPop;
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
                    Pop(msg.ProjectileDirection);
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

        // Leaves the board into a neighbouring bubble cluster: out of play at once (no score, no pop, its slot and
        // its type count free), while the view flows into the survivor and then goes back to the pool.
        internal void AbsorbInto(Vector3 target, float seconds)
        {
            LeavePlay();

            var view = _view.transform;
            var restScale = view.localScale;
            _view.TweenTracker.Kill();
            view.DOKill();
            view.DOScale(restScale * 0.5f, seconds).SetEase(Ease.InQuad);
            view.DOMove(target, seconds).SetEase(Ease.InQuad).OnComplete(() =>
            {
                view.localScale = restScale;
                _poolManager.Return(_poolKey, _view);
            });
        }

        // Leaves the board as AbsorbInto does, out of play at once, but stays where it is: its look plays out for
        // lingerSeconds (a ripe cluster handing its bubbles out), then the view goes back to the pool. The wait is
        // a tween on the view's transform, so a view taken back to the pool sooner drops it. It runs on game time,
        // as the look it waits for does: on real time a pause or slow motion would take the view away mid-look.
        internal void Leave(float lingerSeconds)
        {
            LeavePlay();
            _view.DisableContact();
            if (lingerSeconds <= 0f)
            {
                _poolManager.Return(_poolKey, _view);
                return;
            }

            DOVirtual.DelayedCall(lingerSeconds, () => _poolManager.Return(_poolKey, _view), false)
                .SetTarget(_view.transform)
                .SetLink(_view.gameObject);
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
                new BalloonDeflectedMessage(
                    _model, msg.Context.Projectile, viewPos, msg.ProjectileDirection, _view.ContactRadius));

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

        // A pop prelude's end: the pop the player sees, the balloon already out of play.
        private void PresentPop()
        {
            _popPresenter.Present(_model, _view.transform.position, _view, hitDirection: _popDirection);
            EndPop();
        }

        // Only its look lingers while it shrinks, or straight back to the pool.
        private void EndPop()
        {
            if (_popPulse is { Enabled: true })
            {
                _view.PlayPopPulse(_popPulse, () => _poolManager.Return(_poolKey, _view));
            }
            else
            {
                _poolManager.Return(_poolKey, _view);
            }
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

        // Out of play at once, with no score, no pop and nothing published: off the grid and the registry, its
        // type count freed.
        private void LeavePlay()
        {
            _popped = true;
            LifecycleHelper.DisposeAndClear(ref _itemActivatedSubscription);
            _registry.Unregister(_model);

            var slot = _model.SlotIndex.Value;
            if (ReferenceEquals(_grid.At(slot), _model))
            {
                _grid.Remove(slot);
            }

            _onReturned?.Invoke();
            _onReturned = null;
        }

        private void Pop(Vector3 hitDirection)
        {
            _popped = true;

            if (_model is not IHasItemSlot itemSlot || itemSlot.Item.Value == ItemType.None)
            {
                // A view with a prelude (a ripe cluster folding into one bubble) plays it first and shows the pop
                // as it ends; out of play already, only its look lingers meanwhile.
                _popDirection = hitDirection;
                var prelude = _view.TryPlayPopPrelude(_presentPop);
                if (!prelude)
                {
                    _popPresenter.Present(_model, _view.transform.position, _view, hitDirection: hitDirection);
                }

                _grid.Remove(_model.SlotIndex.Value);
                _registry.Unregister(_model);
                _onReturned?.Invoke();
                if (!prelude)
                {
                    EndPop();
                }
            }
            else
            {
                _popPresenter.Present(_model, _view.transform.position, _view, hitDirection: hitDirection);
                _grid.Remove(_model.SlotIndex.Value);

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

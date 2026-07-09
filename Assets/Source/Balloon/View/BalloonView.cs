using System;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Nudge;
using BalloonParty.Shared;
using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using DG.Tweening;
using UniRx;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Balloon.View
{
    public class BalloonView : MonoBehaviour, IPoolable, ISlotActorView, INudgeable, IBalloonMotionView
    {
        private static readonly int IsStableParam = Animator.StringToHash("IsStable");

        [Header("References")] [SerializeField]
        private ColorableRenderer[] _colorableRenderers;

        [SerializeField] private Animator _animator;
        [SerializeField] private Renderer[] _spriteLayerRenderers;
        [SerializeField] private Collider2D _collider;
        [SerializeField] private TweenTracker _tweenTracker;
        [SerializeField] private ItemDisplayService _itemService;

        [Tooltip("The colourable body sprite — the only renderer swapped to the rainbow material.")]
        [SerializeField] private SpriteRenderer _bodyRenderer;

        [Tooltip("Renderers that must sort above a hosted item (e.g. a foreground overlay); ordered above " +
                 "the item when one is present, just above the body when not.")]
        [SerializeField] private Renderer[] _aboveItemRenderers;

        [Tooltip("Child the float-away tilts. Keep baked specular-fake sprites parented outside it so their " +
                 "light direction stays fixed when the body swings. Falls back to the root if unset.")]
        [SerializeField] private Transform _swayPivot;

        [Header("Sorting")] [SerializeField] private int _baseSortingLayer;

        [Inject] private IBalloonsConfiguration _balloonsConfig;
        [Inject] private IGamePalette _palette;
        [Inject] private IGameConfiguration _config;
        [Inject] private IItemConfiguration _itemConfig;
        [Inject] private PoolManager _poolManager;
        [Inject] private BalloonMotionTicker _motionTicker;

        private readonly CompositeDisposable _bindDisposables = new();

        private IBalloonViewBinding[] _viewBindings;
        private IBalloonVariant _variant;
        private HitVfxOverride[] _hitVfxOverrides;
        private Material _originalBodyMaterial;
        private bool _isNudging;

        public IBalloonModel Model { get; private set; }
        public IBalloonVariant Variant => _variant;
        public TweenTracker TweenTracker => _tweenTracker;
        public SlotActorKind ActorKind => SlotActorKind.Dynamic;
        public Transform RotationPivot => _swayPivot != null ? _swayPivot : transform;

        internal ITransformCapture TransformCapture => _itemService?.TransformCapture;

        private void Awake()
        {
            _viewBindings = GetComponentsInChildren<IBalloonViewBinding>();
            _variant = GetComponentInParent<IBalloonVariant>();

            if (_bodyRenderer != null)
            {
                _originalBodyMaterial = _bodyRenderer.sharedMaterial;
            }
        }

        public void OnSpawned()
        {
            transform.localScale = Vector3.one;
            RotationPivot.localRotation = Quaternion.identity;
            transform.position = Vector3.one * -1000f;

            // Re-enable in case this balloon was graduated into a float-away, which suspends the idle bob.
            if (_animator != null)
            {
                _animator.enabled = true;
            }

            foreach (var r in _spriteLayerRenderers)
            {
                r.enabled = true;
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }
        }

        public void OnDespawned()
        {
            TweenTracker.Kill();
            transform.DOKill();
            _motionTicker?.CancelNudge(this);
            _bindDisposables.Clear();
            _itemService?.Unbind();
            SetBodyMaterial(_originalBodyMaterial);
            Model = null;
            _hitVfxOverrides = null;
            _isNudging = false;
        }

        // Stops the idle animator so a board effect's tween owns the balloon's rotation without the idle
        // clip clobbering it each frame. OnSpawned re-enables it for the next reuse.
        internal void SuspendAnimator()
        {
            if (_animator != null)
            {
                _animator.enabled = false;
            }
        }

        public void Bind(IBalloonModel model)
        {
            _bindDisposables.Clear();
            Model = model;

            if (model is IHasColor colorable)
            {
                // Colour is the single source of truth: the rainbow wildcard id drives the material
                // swap, any other id drives the per-colour tint — so a mid-life Paint convert re-derives
                // correctly from one subscription.
                colorable.Color
                    .Subscribe(colorId => ApplyColorMode(colorId))
                    .AddTo(_bindDisposables);
            }

            model.SlotIndex
                .Subscribe(ApplySortingOrder)
                .AddTo(_bindDisposables);

            model.IsStable
                .Subscribe(stable => _animator.SetBool(IsStableParam, stable))
                .AddTo(_bindDisposables);

            foreach (var binding in _viewBindings)
            {
                binding.Bind(model, _bindDisposables);
            }

            if (_itemService != null && model is IHasItemSlot itemSlot)
            {
                _itemService.Bind(itemSlot.Item,
                    itemSlot.Color,
                    model.SlotIndex,
                    _config,
                    _itemConfig,
                    _palette,
                    _baseSortingLayer,
                    _spriteLayerRenderers.Length,
                    _poolManager,
                    // The item's sorting footprint just changed (added/removed) — re-layer our
                    // above-item renderers over the item's new top order (or the body, if it's gone).
                    () => ApplyAboveItemSorting(Model.SlotIndex.Value));
            }
        }

        /// <summary>Hides all visuals and disables the collider immediately, before the balloon returns to pool.</summary>
        public void Hide()
        {
            foreach (var r in _spriteLayerRenderers)
            {
                r.enabled = false;
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            _itemService?.Unbind();
        }

        public void Nudge(
            Vector3 slotPosition,
            Vector3 direction,
            float nudgeDistance,
            float nudgeDuration,
            Action onComplete)
        {
            // Skip if mid-spawn/balance and we didn't cause the instability.
            if (Model is IWriteableDynamicSlotActor stableChecker
                && !stableChecker.IsStable.Value
                && !_isNudging)
            {
                return;
            }

            var currentScale = transform.localScale;
            transform.DOKill();

            if (Model is IWriteableDynamicSlotActor writable)
            {
                writable.IsStable.Value = false;
            }

            _isNudging = true;

            // Silently replaces any nudge already running for this view.
            _motionTicker.StartNudge(
                this, slotPosition, direction.normalized * nudgeDistance, nudgeDuration, onComplete);

            if (currentScale != Vector3.one)
            {
                transform.DOScale(Vector3.one, nudgeDuration);
            }
        }

        public void ApplyNudgePosition(Vector3 position)
        {
            transform.position = position;
        }

        public void CompleteNudge(Action onComplete)
        {
            if (Model is IWriteableDynamicSlotActor w)
            {
                w.IsStable.Value = true;
            }

            _isNudging = false;
            onComplete?.Invoke();
        }

        public void RegisterDisposeOnDespawn(IDisposable disposable)
        {
            _bindDisposables.Add(disposable);
        }

        public void SetHitVfxOverrides(HitVfxOverride[] overrides)
        {
            _hitVfxOverrides = overrides;
        }

        // parent lets the VFX ride a moving transform (e.g. level transition) instead of firing in place.
        public void PlayHitVfxForOutcome(HitOutcome outcome, Transform parent = null)
        {
            var prefab = FindHitVfxPrefab(outcome);
            if (prefab != null)
            {
                Reparent(PlayHitEffect(prefab), parent);
                return;
            }

            // Pop falls back to a palette-colored default VFX.
            if (outcome != HitOutcome.Pop)
            {
                return;
            }

            var defaultPrefab = _balloonsConfig.DefaultPopVfxPrefab;
            if (defaultPrefab == null)
            {
                Debug.LogWarning(
                    "BalloonView.PlayHitVfxForOutcome: DefaultPopVfxPrefab is null in IBalloonsConfiguration.",
                    this);
                return;
            }

            if (Model is not IHasColor modelColor || string.IsNullOrEmpty(modelColor.Color.Value))
            {
                return;
            }

            Reparent(
                _poolManager.PlayParticle(defaultPrefab, transform.position,
                    _palette.GetColor(modelColor.Color.Value)),
                parent);
        }

        private ParticleSystem FindHitVfxPrefab(HitOutcome outcome)
        {
            if (_hitVfxOverrides == null)
            {
                return null;
            }

            foreach (var o in _hitVfxOverrides)
            {
                if (o.AppliesTo.HasFlag(outcome))
                {
                    return o.Prefab;
                }
            }

            return null;
        }

        private PoolableParticle PlayHitEffect(ParticleSystem prefab)
        {
            if (Model is IHasColor c && !string.IsNullOrEmpty(c.Color.Value))
            {
                return _poolManager.PlayParticle(prefab, transform.position,
                    _palette.GetColor(c.Color.Value));
            }

            return _poolManager.PlayParticle(prefab, transform.position);
        }

        // worldPositionStays keeps the spawn point; the effect detaches back to its pool on its own.
        private static void Reparent(PoolableParticle effect, Transform parent)
        {
            if (parent != null)
            {
                effect.transform.SetParent(parent, worldPositionStays: true);
            }
        }

        private void ApplySortingOrder(Vector2Int slotIndex)
        {
            var baseOrder = SortingHelper.SlotBaseSortingOrder(slotIndex, _config.SlotsSize, _baseSortingLayer);
            SortingHelper.ApplySortingOrder(_spriteLayerRenderers, baseOrder);
            ApplyAboveItemSorting(slotIndex);
        }

        // Orders the above-item renderers over the hosted item's top slot (or just above the body when
        // no item is present). Re-run on slot moves (here) and on item add/remove (via ItemDisplayService's
        // footprint callback) — ItemDisplayService is the only component that knows the item's slot count.
        private void ApplyAboveItemSorting(Vector2Int slotIndex)
        {
            if (_aboveItemRenderers == null || _aboveItemRenderers.Length == 0)
            {
                return;
            }

            var baseOrder = SortingHelper.SlotBaseSortingOrder(slotIndex, _config.SlotsSize, _baseSortingLayer);
            var itemCount = _itemService != null ? _itemService.ActiveItemSortingCount : 0;
            SortingHelper.ApplySortingOrder(_aboveItemRenderers, baseOrder + _spriteLayerRenderers.Length + itemCount);
        }

        // Rainbow mode replaces the normal per-colour tint with the banded material — the tint would
        // otherwise multiply into the shader's band colour (see RainbowBalloon.shader).
        private void ApplyColorMode(string colorId)
        {
            if (_palette.IsRainbow(colorId))
            {
                SetBodyMaterial(_balloonsConfig.RainbowMaterial);

                foreach (var renderer in _colorableRenderers)
                {
                    renderer.SetColor(Color.white);
                }

                return;
            }

            SetBodyMaterial(_originalBodyMaterial);

            if (string.IsNullOrEmpty(colorId))
            {
                return;
            }

            var color = _palette.GetColor(colorId);
            foreach (var renderer in _colorableRenderers)
            {
                renderer.SetColor(color);
            }
        }

        private void SetBodyMaterial(Material material)
        {
            if (_bodyRenderer != null && material != null)
            {
                _bodyRenderer.sharedMaterial = material;
            }
        }
    }
}

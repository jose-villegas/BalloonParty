using System;
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

namespace BalloonParty.Balloon.View
{
    public class BalloonView : MonoBehaviour, IPoolable, ISlotActorView, INudgeable
    {
        private static readonly int IsStableParam = Animator.StringToHash("IsStable");

        [Header("References")] [SerializeField]
        private ColorableRenderer[] _colorableRenderers;

        [SerializeField] private Animator _animator;
        [SerializeField] private Renderer[] _spriteLayerRenderers;
        [SerializeField] private Collider2D _collider;
        [SerializeField] private TweenTracker _tweenTracker;
        [SerializeField] private ItemDisplayService _itemService;

        [Header("Sorting")] [SerializeField] private int _baseSortingLayer;

        [Inject] private BalloonsConfiguration _balloonsConfig;
        [Inject] private GamePalette _palette;
        [Inject] private IGameConfiguration _config;
        [Inject] private ItemConfiguration _itemConfig;
        [Inject] private PoolManager _poolManager;

        private readonly CompositeDisposable _bindDisposables = new();

        private IBalloonViewBinding[] _viewBindings;
        private IBalloonVariant _variant;
        private HitVfxOverride[] _hitVfxOverrides;
        private bool _isNudging;

        public IBalloonModel Model { get; private set; }
        public IBalloonVariant Variant => _variant;
        public TweenTracker TweenTracker => _tweenTracker;
        public SlotActorKind ActorKind => SlotActorKind.Dynamic;

        internal ITransformCapture TransformCapture => _itemService?.TransformCapture;

        private void Awake()
        {
            _viewBindings = GetComponentsInChildren<IBalloonViewBinding>();
            _variant = GetComponentInParent<IBalloonVariant>();
        }

        public void OnSpawned()
        {
            transform.localScale = Vector3.one;
            transform.position = Vector3.one * -1000f;

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
            _bindDisposables.Clear();
            _itemService?.Unbind();
            Model = null;
            _hitVfxOverrides = null;
            _isNudging = false;
        }

        public void Bind(IBalloonModel model)
        {
            _bindDisposables.Clear();
            Model = model;

            if (model is IHasColor colorable)
            {
                _colorableRenderers
                    .BindColor(colorable.Color, _palette.GetColor)
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
                    _poolManager);
            }
        }

        /// <summary>
        ///     Hides all visuals and disables the collider immediately. Called when an item balloon
        ///     is hit but before the item effect completes and the balloon returns to pool.
        /// </summary>
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
            // Skip nudge if the balloon is mid-spawn/balance and we didn't cause the instability.
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

            var sequence = DOTween.Sequence();
            sequence.Append(transform.DOMove(
                slotPosition + (direction.normalized * nudgeDistance),
                nudgeDuration / 2f));
            sequence.Append(transform.DOMove(slotPosition, nudgeDuration / 2f));
            sequence.OnComplete(() => OnNudgeComplete(onComplete));

            TweenTracker.Replace(sequence);

            if (currentScale != Vector3.one)
            {
                transform.DOScale(Vector3.one, nudgeDuration);
            }
        }

        private void OnNudgeComplete(Action onComplete)
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

        public void PlayHitVfxForOutcome(HitOutcome outcome)
        {
            var prefab = FindHitVfxPrefab(outcome);
            if (prefab != null)
            {
                PlayHitEffect(prefab);
                return;
            }

            // Pop has a palette-colored default VFX when no override is configured.
            if (outcome != HitOutcome.Pop)
            {
                return;
            }

            var defaultPrefab = _balloonsConfig.DefaultPopVfxPrefab;
            if (defaultPrefab == null)
            {
                Debug.LogWarning(
                    "BalloonView.PlayHitVfxForOutcome: DefaultPopVfxPrefab is null in BalloonsConfiguration.",
                    this);
                return;
            }

            if (Model is not IHasColor modelColor || string.IsNullOrEmpty(modelColor.Color.Value))
            {
                return;
            }

            var defaultKey = defaultPrefab.name;
            var defaultEffect =
                _poolManager.GetOrRegister(defaultKey, () => new ParticlePoolChannel(defaultPrefab.gameObject));
            defaultEffect.Play(transform.position,
                _palette.GetColor(modelColor.Color.Value),
                () => _poolManager.Return(defaultKey, defaultEffect));
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

        private void PlayHitEffect(ParticleSystem prefab)
        {
            var key = prefab.name;
            var effect = _poolManager.GetOrRegister(key, () => new ParticlePoolChannel(prefab.gameObject));
            if (Model is IHasColor c && !string.IsNullOrEmpty(c.Color.Value))
            {
                effect.Play(transform.position,
                    _palette.GetColor(c.Color.Value),
                    () => _poolManager.Return(key, effect));
            }
            else
            {
                effect.Play(transform.position, () => _poolManager.Return(key, effect));
            }
        }

        private void ApplySortingOrder(Vector2Int slotIndex)
        {
            var baseOrder = SortingHelper.SlotBaseSortingOrder(slotIndex, _config.SlotsSize, _baseSortingLayer);
            SortingHelper.ApplySortingOrder(_spriteLayerRenderers, baseOrder);
        }
    }
}

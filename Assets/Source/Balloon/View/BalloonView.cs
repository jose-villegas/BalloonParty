using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using DG.Tweening;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.View
{
    public class BalloonView : MonoBehaviour, IPoolable
    {
        private static readonly int IsStableParam = Animator.StringToHash("IsStable");

        [Header("References")] [SerializeField]
        private ColorableRenderer[] _colorableRenderers;

        [SerializeField] private Animator _animator;
        [SerializeField] private Renderer[] _spriteLayerRenderers;
        [SerializeField] private Collider2D _collider;

        [Header("Sorting")] [SerializeField] private int _baseSortingLayer;

        [Inject] private BalloonsConfiguration _balloonsConfig;
        [Inject] private GamePalette _palette;
        [Inject] private IGameConfiguration _config;
        [Inject] private ItemConfiguration _itemConfig;
        [Inject] private PoolManager _poolManager;

        private readonly CompositeDisposable _bindDisposables = new();

        private ItemDisplayService _itemService;
        private ParticleSystem _popVfxOverride;

        public IBalloonModel Model { get; private set; }
        public TweenTracker TweenTracker { get; private set; }

        private void Awake()
        {
            TweenTracker = GetComponent<TweenTracker>();
            _itemService = GetComponentInChildren<ItemDisplayService>();
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
            _popVfxOverride = null;
        }

        public void Bind(IBalloonModel model)
        {
            _bindDisposables.Clear();
            Model = model;

            _colorableRenderers
                .BindColor(model.Color, _palette.GetColor)
                .AddTo(_bindDisposables);

            model.SlotIndex
                .Subscribe(ApplySortingOrder)
                .AddTo(_bindDisposables);

            model.IsStable
                .Subscribe(stable => _animator.SetBool(IsStableParam, stable))
                .AddTo(_bindDisposables);

            foreach (var binding in GetComponentsInChildren<IBalloonViewBinding>())
            {
                binding.Bind(model, _bindDisposables);
            }

            if (_itemService != null)
            {
                _itemService.Bind(model.Item,
                    model.Color,
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
            var currentScale = transform.localScale;
            transform.DOKill();

            var sequence = DOTween.Sequence();
            sequence.Append(transform.DOMove(
                slotPosition + (direction.normalized * nudgeDistance),
                nudgeDuration / 2f));
            sequence.Append(transform.DOMove(slotPosition, nudgeDuration / 2f));
            sequence.OnComplete(() => onComplete?.Invoke());

            TweenTracker.Replace(sequence);

            if (currentScale != Vector3.one)
            {
                transform.DOScale(Vector3.one, nudgeDuration);
            }
        }

        public void PlayPopEffect()
        {
            if (_popVfxOverride != null)
            {
                // Override VFX — plays with its own baked color, no tinting
                var key = _popVfxOverride.name;
                var effect = _poolManager.GetOrRegister(key, () => new ParticlePoolChannel(_popVfxOverride.gameObject));
                effect.Play(transform.position, () => _poolManager.Return(key, effect));
                return;
            }

            var defaultPrefab = _balloonsConfig.DefaultPopVfxPrefab;
            if (defaultPrefab == null)
            {
                Debug.LogWarning(
                    "BalloonView.PlayPopEffect: DefaultPopVfxPrefab is null in BalloonsConfiguration.",
                    this);
                return;
            }

            if (string.IsNullOrEmpty(Model?.Color.Value))
            {
                return;
            }

            var defaultKey = defaultPrefab.name;
            var defaultEffect =
                _poolManager.GetOrRegister(defaultKey, () => new ParticlePoolChannel(defaultPrefab.gameObject));
            defaultEffect.Play(transform.position,
                _palette.GetColor(Model.Color.Value),
                () => _poolManager.Return(defaultKey, defaultEffect));
        }

        public void RegisterDisposeOnDespawn(IDisposable disposable)
        {
            _bindDisposables.Add(disposable);
        }

        public void SetPopVfxOverride(ParticleSystem prefab)
        {
            _popVfxOverride = prefab;
        }


        private void ApplySortingOrder(Vector2Int slotIndex)
        {
            var baseOrder = SortingHelper.SlotBaseSortingOrder(slotIndex, _config.SlotsSize, _baseSortingLayer);
            SortingHelper.ApplySortingOrder(_spriteLayerRenderers, baseOrder);
        }
    }
}

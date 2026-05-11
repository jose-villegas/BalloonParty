#region

using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

#endregion

namespace BalloonParty.Balloon.View
{
    public class BalloonView : MonoBehaviour, IPoolable
    {
        private static readonly int IsStableParam = Animator.StringToHash("IsStable");

        [Header("References")] [SerializeField]
        private SpriteRenderer _renderer;

        [SerializeField] private Animator _animator;
        [SerializeField] private Renderer[] _spriteLayerRenderers;
        [SerializeField] private ParticleSystem _popVfxPrefab;
        [SerializeField] private Collider2D _collider;

        [Header("Sorting")] [SerializeField] private int _baseSortingLayer;

        [Inject] private IGameConfiguration _config;
        [Inject] private ItemConfiguration _itemConfig;
        [Inject] private SlotGrid _grid;
        [Inject] private ISubscriber<BalloonNudgeMessage> _nudgeSubscriber;
        [Inject] private PoolManager _poolManager;

        private readonly CompositeDisposable _bindDisposables = new();

        private ItemDisplayService _itemService;

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
        }

        public void Bind(IBalloonModel model)
        {
            _bindDisposables.Clear();
            Model = model;

            model.Color
                .Subscribe(ApplyColor)
                .AddTo(_bindDisposables);

            model.SlotIndex
                .Subscribe(ApplySortingOrder)
                .AddTo(_bindDisposables);

            model.IsStable
                .Subscribe(stable => _animator.SetBool(IsStableParam, stable))
                .AddTo(_bindDisposables);

            _nudgeSubscriber.Subscribe(OnNudge).AddTo(_bindDisposables);

            if (_itemService != null)
            {
                _itemService.Bind(model.Item,
                    model.Color,
                    model.SlotIndex,
                    _config,
                    _itemConfig,
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

        public void PlayPopEffect(Color color)
        {
            var key = _popVfxPrefab.name;
            var vfx = _poolManager.GetOrRegister(key, () => new VfxPoolChannel(_popVfxPrefab));
            vfx.Play(transform.position, color, () => _poolManager.Return(key, vfx));
        }

        public void RegisterDisposeOnDespawn(IDisposable disposable)
        {
            _bindDisposables.Add(disposable);
        }

        private void ApplyColor(string colorName)
        {
            var color = _config.BalloonColor(colorName);

            if (_renderer != null)
            {
                _renderer.color = color;
            }
        }

        private void ApplySortingOrder(Vector2Int slotIndex)
        {
            var baseOrder = SortingHelper.SlotBaseSortingOrder(slotIndex, _config.SlotsSize, _baseSortingLayer);
            SortingHelper.ApplySortingOrder(_spriteLayerRenderers, baseOrder);
        }

        private void Nudge(
            Vector3 slotPosition,
            Vector3 direction,
            float nudgeDistance,
            float nudgeDuration,
            Action onComplete)
        {
            // Standalone spawn tweens would compete with the nudge sequence
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

        private void OnNudge(BalloonNudgeMessage msg)
        {
            if (msg.Balloon != Model)
            {
                return;
            }

            var writeable = _grid.At(Model.SlotIndex.Value);
            var slotPos = _grid.IndexToWorldPosition(Model.SlotIndex.Value);
            var direction = slotPos - msg.HitSlotPosition;

            var nudgeDistance = msg.NudgeDistance ?? _config.NudgeDistance;
            var nudgeDuration = msg.NudgeDuration ?? _config.NudgeDuration;

            writeable.IsStable.Value = false;
            Nudge(slotPos,
                direction,
                nudgeDistance,
                nudgeDuration,
                () => writeable.IsStable.Value = true);
        }
    }
}

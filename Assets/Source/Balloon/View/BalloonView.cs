using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using DG.Tweening;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.View
{
    public class BalloonView : MonoBehaviour, IPoolable
    {
        [Header("References")] [SerializeField]
        private SpriteRenderer _renderer;

        [SerializeField] private SpriteRenderer _shadowRenderer;
        [SerializeField] private Animator _animator;
        [SerializeField] private Renderer[] _spriteLayerRenderers;
        [SerializeField] private ParticleSystem _popVfxPrefab;

        [Header("Shadow")] [SerializeField] [Range(0f, 1f)]
        private float _shadowAlpha;

        [SerializeField] [Range(0f, 5f)] private float _shadowIntensity;

        [Header("Sorting")] [SerializeField] private int _baseSortingLayer;

        private readonly CompositeDisposable _bindDisposables = new();

        [Inject] private IGameConfiguration _config;
        [Inject] private PoolManager _poolManager;
        public BalloonModel Model { get; private set; }
        public TweenTracker TweenTracker { get; private set; }


        private void Awake()
        {
            TweenTracker = GetComponent<TweenTracker>();
        }

        public void OnSpawned()
        {
            transform.localScale = Vector3.one;
            transform.position = Vector3.one * -1000f;
        }

        public void OnDespawned()
        {
            TweenTracker.Kill();
            transform.DOKill();
            _bindDisposables.Clear();
            Model = null;
        }

        public void RegisterDisposeOnDespawn(IDisposable disposable)
        {
            _bindDisposables.Add(disposable);
        }

        public void Bind(BalloonModel model)
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
                .Subscribe(stable => _animator.SetBool("IsStable", stable))
                .AddTo(_bindDisposables);
        }

        public void PlayPopEffect(Color color)
        {
            _poolManager.GetOrRegister(_popVfxPrefab.name, () => new VfxPoolChannel(_popVfxPrefab))
                .Play(transform.position, color);
        }

        private void ApplyColor(string colorName)
        {
            var color = _config.BalloonColor(colorName);

            if (_renderer != null)
            {
                _renderer.color = color;
            }

            if (_shadowRenderer != null)
            {
                _shadowRenderer.color = new Color(
                    color.r * _shadowIntensity,
                    color.g * _shadowIntensity,
                    color.b * _shadowIntensity,
                    _shadowAlpha);
            }
        }

        private void ApplySortingOrder(Vector2Int slotIndex)
        {
            var maxRow = _config.SlotsSize.y - 1;
            var baseOrder = (slotIndex.x + ((maxRow - slotIndex.y) * _config.SlotsSize.x)) * _baseSortingLayer;

            for (var i = 0; i < _spriteLayerRenderers.Length; i++)
            {
                _spriteLayerRenderers[i].sortingOrder = baseOrder + i + 1;
            }
        }
    }
}

#region

using System;
using BalloonParty.Balloon.Model;
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

        [Inject] private IGameConfiguration _config;
        [Inject] private SlotGrid _grid;
        [Inject] private ISubscriber<BalloonNudgeMessage> _nudgeSubscriber;
        [Inject] private PoolManager _poolManager;

        private readonly CompositeDisposable _bindDisposables = new();

        public IBalloonModel Model { get; private set; }
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
                .Subscribe(stable => _animator.SetBool("IsStable", stable))
                .AddTo(_bindDisposables);

            _nudgeSubscriber.Subscribe(OnNudge).AddTo(_bindDisposables);
        }

        public void PlayPopEffect(Color color)
        {
            _poolManager.GetOrRegister(_popVfxPrefab.name, () => new VfxPoolChannel(_popVfxPrefab))
                .Play(transform.position, color);
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

            writeable.IsStable.Value = false;
            Nudge(slotPos, direction, () => writeable.IsStable.Value = true);
        }

        private void Nudge(Vector3 slotPosition, Vector3 direction, System.Action onComplete)
        {
            // Standalone spawn tweens would compete with the nudge sequence
            var currentScale = transform.localScale;
            transform.DOKill();

            var nudgeDuration = _config.NudgeDuration;

            var sequence = DOTween.Sequence();
            sequence.Append(transform.DOMove(
                slotPosition + (direction.normalized * _config.NudgeDistance),
                nudgeDuration / 2f));
            sequence.Append(transform.DOMove(slotPosition, nudgeDuration / 2f));
            sequence.OnComplete(() => onComplete?.Invoke());

            TweenTracker.Replace(sequence);

            if (currentScale != Vector3.one)
            {
                transform.DOScale(Vector3.one, nudgeDuration);
            }
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

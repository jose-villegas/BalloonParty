using UniRx;
using UnityEngine;
using VContainer;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;

namespace BalloonParty.Balloon.View
{
    public class BalloonView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private SpriteRenderer _shadowRenderer;
        [SerializeField] private Animator _animator;
        [SerializeField] private Renderer[] _spriteLayerRenderers;

        [SerializeField] [Range(0f, 1f)] private float _shadowAlpha;
        [SerializeField] [Range(0f, 5f)] private float _shadowIntensity;
        [SerializeField] private int _baseSortingLayer;

        [Inject] private IGameConfiguration _config;

        public void Bind(BalloonModel model)
        {
            model.Color
                .Subscribe(ApplyColor)
                .AddTo(this);

            model.SlotIndex
                .Subscribe(ApplySortingOrder)
                .AddTo(this);

            model.IsStable
                .Subscribe(stable => _animator.SetBool("IsStable", stable))
                .AddTo(this);
        }

        private void ApplyColor(string colorName)
        {
            var color = _config.BalloonColor(colorName);

            if (_renderer != null)
                _renderer.color = color;

            if (_shadowRenderer != null)
                _shadowRenderer.color = new Color(
                    color.r * _shadowIntensity,
                    color.g * _shadowIntensity,
                    color.b * _shadowIntensity,
                    _shadowAlpha);
        }

        private void ApplySortingOrder(Vector2Int slotIndex)
        {
            var baseOrder = (slotIndex.x + slotIndex.y * _config.SlotsSize.x) * _baseSortingLayer;

            for (var i = 0; i < _spriteLayerRenderers.Length; i++)
                _spriteLayerRenderers[i].sortingOrder = baseOrder + i + 1;
        }
    }
}
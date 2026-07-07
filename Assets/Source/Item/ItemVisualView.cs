using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.Pool;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item
{
    public class ItemVisualView : MonoBehaviour, IItemView, IPoolable
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private SpriteRenderer[] _spritesToSetColor;
        [SerializeField] private Renderer[] _sortingRenderers;
        [SerializeField] [Range(0f, 1f)] private float _spritesAlpha;

        private Animator _animator;

        public ItemType Type => _type;

        /// <summary>How many sorting slots this visual occupies — lets a host layer other renderers above it.</summary>
        public int SortingRendererCount => _sortingRenderers.Length;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
        }

        public void Activate(Color balloonColor)
        {
            SetVisible(true);
            SetColor(balloonColor);
        }

        public void SetColor(Color balloonColor)
        {
            foreach (var sr in _spritesToSetColor)
            {
                sr.color = balloonColor.WithAlpha(_spritesAlpha);
            }
        }

        public void ApplySortingOrder(int startOrder)
        {
            SortingHelper.ApplySortingOrder(_sortingRenderers, startOrder);
        }

        public void Deactivate()
        {
            SetVisible(false);
        }

        public void OnDespawned()
        {
            Deactivate();
        }

        public void OnSpawned()
        {
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (_animator != null)
            {
                _animator.Play(0, -1, 0f);
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in _sortingRenderers)
            {
                r.enabled = visible;
            }
        }
    }
}

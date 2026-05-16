using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Item
{
    public class ItemVisualView : MonoBehaviour, IItemView, IPoolable
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private SpriteRenderer[] _spritesToSetColor;
        [SerializeField] private Renderer[] _sortingRenderers;
        [SerializeField] [Range(0f, 1f)] private float _spritesAlpha;

        public ItemType Type => _type;

        public void Activate(Color balloonColor)
        {
            SetVisible(true);
            SetColor(balloonColor);
        }

        public void SetColor(Color balloonColor)
        {
            foreach (var sr in _spritesToSetColor)
            {
                sr.color = new Color(balloonColor.r, balloonColor.g, balloonColor.b, _spritesAlpha);
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

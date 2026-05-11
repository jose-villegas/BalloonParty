#region

using BalloonParty.Configuration;
using BalloonParty.Shared;
using UnityEngine;

#endregion

namespace BalloonParty.Item
{
    public class ItemVisualView : MonoBehaviour, IItemView
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private SpriteRenderer[] _spritesToSetColor;
        [SerializeField] private Renderer[] _sortingRenderers;
        [SerializeField] [Range(0f, 1f)] private float _spritesAlpha;

        public ItemType Type => _type;


        public void Activate(Color balloonColor)
        {
            SetVisible(true);

            foreach (var sr in _spritesToSetColor)
            {
                sr.color = new Color(balloonColor.r, balloonColor.g, balloonColor.b, _spritesAlpha);
            }
        }

        public void Deactivate()
        {
            SetVisible(false);
        }

        public void ApplySortingOrder(int startOrder)
        {
            SortingHelper.ApplySortingOrder(_sortingRenderers, startOrder);
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

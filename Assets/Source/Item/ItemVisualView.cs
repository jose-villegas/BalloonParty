#region

using BalloonParty.Configuration;
using BalloonParty.Shared;
using UniRx;
using UnityEngine;
using VContainer;

#endregion

namespace BalloonParty.Item
{
    public class ItemVisualView : MonoBehaviour, IItemView
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private SpriteRenderer[] _spritesToSetColor;
        [SerializeField] private Renderer[] _sortingRenderers;
        [SerializeField] [Range(0f, 1f)] private float _spritesAlpha;

        [Inject] private ItemDisplayService _display;

        private readonly CompositeDisposable _disposables = new();

        public ItemType Type => _type;

        private void Awake()
        {
            SetVisible(false);
        }

        private void Start()
        {
            if (_display == null)
            {
                return;
            }

            _display.ActiveItem
                .Subscribe(OnActiveItemChanged)
                .AddTo(_disposables);

            _display.SortingStartOrder
                .Subscribe(ApplySortingOrder)
                .AddTo(_disposables);
        }

        private void OnDestroy()
        {
            _disposables.Clear();
        }

        private void OnActiveItemChanged(ItemType activeType)
        {
            if (activeType == _type)
            {
                Activate(_display.ActiveColor.Value);
            }
            else
            {
                Deactivate();
            }
        }

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

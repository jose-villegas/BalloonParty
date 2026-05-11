#region

using BalloonParty.Configuration;
using BalloonParty.Shared;
using UniRx;
using UnityEngine;

#endregion

namespace BalloonParty.Item
{
    public class ItemDisplayService : MonoBehaviour
    {
        private readonly CompositeDisposable _disposables = new();

        private string _activePoolKey;
        private ItemVisualView _activeView;
        private int _balloonRendererCount;
        private int _baseSortingOffset;
        private IGameConfiguration _config;
        private ItemConfiguration _itemConfig;
        private PoolManager _poolManager;
        private IReadOnlyReactiveProperty<Vector2Int> _slotIndex;

        public void Bind(
            IReadOnlyReactiveProperty<ItemType> item,
            IReadOnlyReactiveProperty<string> colorName,
            IReadOnlyReactiveProperty<Vector2Int> slotIndex,
            IGameConfiguration config,
            ItemConfiguration itemConfig,
            int baseSortingOffset,
            int balloonRendererCount,
            PoolManager poolManager)
        {
            Unbind();

            _config = config;
            _itemConfig = itemConfig;
            _baseSortingOffset = baseSortingOffset;
            _balloonRendererCount = balloonRendererCount;
            _slotIndex = slotIndex;
            _poolManager = poolManager;

            item
                .Subscribe(type => OnItemChanged(type, colorName.Value))
                .AddTo(_disposables);

            slotIndex
                .Subscribe(slot => ApplySorting(slot))
                .AddTo(_disposables);
        }

        public void Unbind()
        {
            _disposables.Clear();
            ReturnActiveVisual();
        }


        private void ApplySorting(Vector2Int slot)
        {
            if (_activeView == null || _config == null)
            {
                return;
            }

            var baseOrder = SortingHelper.SlotBaseSortingOrder(slot, _config.SlotsSize, _baseSortingOffset);
            _activeView.ApplySortingOrder(baseOrder + _balloonRendererCount);
        }

        private void OnItemChanged(ItemType type, string colorName)
        {
            ReturnActiveVisual();

            if (type == ItemType.None || _config == null || _poolManager == null)
            {
                return;
            }

            var settings = _itemConfig[type];
            if (settings.VisualPrefab == null)
            {
                return;
            }

            var key = settings.VisualPrefab.name;
            _activePoolKey = key;
            _activeView = _poolManager.GetOrRegister(key, () => new ItemVisualPoolChannel(settings.VisualPrefab));

            _activeView.transform.SetParent(transform, false);
            _activeView.transform.localPosition = Vector3.zero;
            _activeView.transform.localScale = Vector3.one;

            var color = _config.BalloonColor(colorName);
            _activeView.Activate(color);
            ApplySorting(_slotIndex.Value);
        }


        private void ReturnActiveVisual()
        {
            if (_activeView != null && _poolManager != null && _activePoolKey != null)
            {
                _poolManager.Return(_activePoolKey, _activeView);
            }

            _activeView = null;
            _activePoolKey = null;
        }
    }
}

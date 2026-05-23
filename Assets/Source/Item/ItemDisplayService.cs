using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.Pool;
using UniRx;
using UnityEngine;

namespace BalloonParty.Item
{
    public class ItemDisplayService : MonoBehaviour
    {
        private readonly CompositeDisposable _disposables = new();

        private string _activePoolKey;
        private ItemVisualView _activeView;
        private ITransformCapture _activeCapture;
        private int _balloonRendererCount;
        private int _baseSortingOffset;
        private IGameConfiguration _config;
        private GamePalette _palette;
        private ItemConfiguration _itemConfig;
        private PoolManager _poolManager;
        private IReadOnlyReactiveProperty<Vector2Int> _slotIndex;

        internal ITransformCapture TransformCapture => _activeCapture;

        internal void Bind(
            IReadOnlyReactiveProperty<ItemType> item,
            IReadOnlyReactiveProperty<string> colorName,
            IReadOnlyReactiveProperty<Vector2Int> slotIndex,
            IGameConfiguration config,
            ItemConfiguration itemConfig,
            GamePalette palette,
            int baseSortingOffset,
            int balloonRendererCount,
            PoolManager poolManager)
        {
            Unbind();

            _config = config;
            _itemConfig = itemConfig;
            _palette = palette;
            _baseSortingOffset = baseSortingOffset;
            _balloonRendererCount = balloonRendererCount;
            _slotIndex = slotIndex;
            _poolManager = poolManager;

            item
                .Subscribe(type => OnItemChanged(type, colorName.Value))
                .AddTo(_disposables);

            colorName
                .Subscribe(RecolorActiveVisual)
                .AddTo(_disposables);

            slotIndex
                .Subscribe(ApplySorting)
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
            _activeCapture = _activeView.GetComponentInChildren<ITransformCapture>();

            _activeView.transform.SetParent(transform, false);
            _activeView.transform.localPosition = Vector3.zero;
            _activeView.transform.localScale = Vector3.one;

            var color = _palette.GetColor(colorName);
            _activeView.Activate(color);
            ApplySorting(_slotIndex.Value);
        }

        private void RecolorActiveVisual(string colorName)
        {
            if (_activeView == null || string.IsNullOrEmpty(colorName))
            {
                return;
            }

            _activeView.SetColor(_palette.GetColor(colorName));
        }

        private void ReturnActiveVisual()
        {
            if (_activeView != null && _poolManager != null && _activePoolKey != null)
            {
                _poolManager.Return(_activePoolKey, _activeView);
            }

            _activeView = null;
            _activeCapture = null;
            _activePoolKey = null;
        }
    }
}

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

        private IGameConfiguration _config;
        private int _baseSortingOffset;
        private int _balloonRendererCount;
        private GameObject _activeInstance;
        private IReadOnlyReactiveProperty<Vector2Int> _slotIndex;

        public void Bind(
            IReadOnlyReactiveProperty<ItemType> item,
            IReadOnlyReactiveProperty<string> colorName,
            IReadOnlyReactiveProperty<Vector2Int> slotIndex,
            IGameConfiguration config,
            int baseSortingOffset,
            int balloonRendererCount)
        {
            Unbind();

            _config = config;
            _baseSortingOffset = baseSortingOffset;
            _balloonRendererCount = balloonRendererCount;
            _slotIndex = slotIndex;

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
            DestroyActiveVisual();
        }

        private void OnItemChanged(ItemType type, string colorName)
        {
            DestroyActiveVisual();

            if (type == ItemType.None || _config == null)
            {
                return;
            }

            var settings = _config.ItemConfiguration[type];
            if (settings.VisualPrefab == null)
            {
                Debug.LogWarning($"[ItemDisplayService] VisualPrefab is null for {type} — assign it in ItemConfiguration.");
                return;
            }

            _activeInstance = Instantiate(settings.VisualPrefab, transform);
            _activeInstance.transform.localPosition = Vector3.zero;

            var color = _config.BalloonColor(colorName);
            var view = _activeInstance.GetComponent<ItemVisualView>();
            if (view != null)
            {
                view.Activate(color);
                ApplySorting(_slotIndex.Value);
                Debug.Log($"[ItemDisplayService] Spawned {type} visual on {transform.parent?.name ?? "root"}");
            }
            else
            {
                Debug.LogWarning($"[ItemDisplayService] VisualPrefab for {type} has no ItemVisualView component.");
            }
        }

        private void ApplySorting(Vector2Int slot)
        {
            if (_activeInstance == null || _config == null)
            {
                return;
            }

            var baseOrder = SortingHelper.SlotBaseSortingOrder(slot, _config.SlotsSize, _baseSortingOffset);
            var view = _activeInstance.GetComponent<ItemVisualView>();
            if (view != null)
            {
                view.ApplySortingOrder(baseOrder + _balloonRendererCount);
            }
        }

        private void DestroyActiveVisual()
        {
            if (_activeInstance != null)
            {
                Destroy(_activeInstance);
                _activeInstance = null;
            }
        }
    }
}

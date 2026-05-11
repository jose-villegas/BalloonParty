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
        private readonly ReactiveProperty<Color> _activeColor = new(default);
        private readonly ReactiveProperty<ItemType> _activeItem = new(ItemType.None);
        private readonly CompositeDisposable _disposables = new();
        private readonly ReactiveProperty<int> _sortingStartOrder = new(0);

        public IReadOnlyReactiveProperty<ItemType> ActiveItem => _activeItem;
        public IReadOnlyReactiveProperty<Color> ActiveColor => _activeColor;
        public IReadOnlyReactiveProperty<int> SortingStartOrder => _sortingStartOrder;

        public void Bind(
            IReadOnlyReactiveProperty<ItemType> item,
            IReadOnlyReactiveProperty<string> colorName,
            IReadOnlyReactiveProperty<Vector2Int> slotIndex,
            IGameConfiguration config,
            int baseSortingOffset)
        {
            Unbind();

            item
                .Subscribe(type =>
                {
                    _activeItem.Value = type;
                    _activeColor.Value = type != ItemType.None
                        ? config.BalloonColor(colorName.Value)
                        : default;
                })
                .AddTo(_disposables);

            slotIndex
                .Subscribe(slot =>
                {
                    var baseOrder = SortingHelper.SlotBaseSortingOrder(slot, config.SlotsSize, baseSortingOffset);
                    _sortingStartOrder.Value = baseOrder + baseSortingOffset;
                })
                .AddTo(_disposables);
        }

        public void Unbind()
        {
            _disposables.Clear();
            _activeItem.Value = ItemType.None;
        }
    }
}

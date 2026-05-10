#region

using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using UniRx;
using UnityEngine;

#endregion

namespace BalloonParty.Balloon.Items
{
    public class ItemDisplayService : MonoBehaviour
    {
        private readonly CompositeDisposable _disposables = new();
        private readonly ReactiveProperty<ItemType> _activeItem = new(ItemType.None);
        private readonly ReactiveProperty<Color> _activeColor = new(default);
        private readonly ReactiveProperty<int> _sortingStartOrder = new(0);

        public IReadOnlyReactiveProperty<ItemType> ActiveItem => _activeItem;
        public IReadOnlyReactiveProperty<Color> ActiveColor => _activeColor;
        public IReadOnlyReactiveProperty<int> SortingStartOrder => _sortingStartOrder;

        public void Bind(IBalloonModel model, IGameConfiguration config, int baseSortingOffset)
        {
            Unbind();

            model.Item
                .Subscribe(type =>
                {
                    _activeItem.Value = type;
                    _activeColor.Value = type != ItemType.None
                        ? config.BalloonColor(model.Color.Value)
                        : default;
                })
                .AddTo(_disposables);

            model.SlotIndex
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

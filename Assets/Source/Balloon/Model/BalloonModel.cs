using BalloonParty.Configuration;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    public class BalloonModel : IWriteableBalloonModel
    {
        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<string> TypeName { get; } = new();
        public ReactiveProperty<int> HitsRemaining { get; } = new(1);
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);

        IReadOnlyReactiveProperty<string> IBalloonModel.Color => Color;
        IReadOnlyReactiveProperty<string> IBalloonModel.TypeName => TypeName;
        IReadOnlyReactiveProperty<int> IBalloonModel.HitsRemaining => HitsRemaining;
        IReadOnlyReactiveProperty<Vector2Int> IBalloonModel.SlotIndex => SlotIndex;
        IReadOnlyReactiveProperty<bool> IBalloonModel.IsStable => IsStable;
        IReadOnlyReactiveProperty<ItemType> IBalloonModel.Item => Item;
    }
}

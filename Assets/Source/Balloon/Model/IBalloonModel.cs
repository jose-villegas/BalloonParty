#region

using BalloonParty.Configuration;
using UniRx;
using UnityEngine;

#endregion

namespace BalloonParty.Balloon.Model
{
    public interface IBalloonModel
    {
        IReadOnlyReactiveProperty<string> Color { get; }
        IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
        IReadOnlyReactiveProperty<bool> IsStable { get; }
        IReadOnlyReactiveProperty<ItemType> Item { get; }
    }
}


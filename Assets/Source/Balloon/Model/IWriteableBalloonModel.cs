#region

using UniRx;
using UnityEngine;

#endregion

namespace BalloonParty.Balloon.Model
{
    public interface IWriteableBalloonModel : IBalloonModel
    {
        new ReactiveProperty<string> Color { get; }
        new ReactiveProperty<Vector2Int> SlotIndex { get; }
        new ReactiveProperty<bool> IsStable { get; }
    }
}


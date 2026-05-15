using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    public interface IWriteableBalloonModel : IBalloonModel
    {
        new ReactiveProperty<string> Color { get; }
        new ReactiveProperty<BalloonType> TypeName { get; }
        new ReactiveProperty<int> HitsRemaining { get; }
        new ReactiveProperty<Vector2Int> SlotIndex { get; }
        new ReactiveProperty<bool> IsStable { get; }
        new ReactiveProperty<ItemType> Item { get; }

        new NudgeOverride[] NudgeOverrides { get; set; }
        new bool CanHoldItem { get; set; }
        new bool IsPaintable { get; set; }
        new int ScoreValue { get; set; }
    }
}

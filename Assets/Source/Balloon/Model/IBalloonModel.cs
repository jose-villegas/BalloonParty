using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    public interface IBalloonModel
    {
        IReadOnlyReactiveProperty<string> Color { get; }
        IReadOnlyReactiveProperty<BalloonType> TypeName { get; }
        IReadOnlyReactiveProperty<int> HitsRemaining { get; }
        IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
        IReadOnlyReactiveProperty<bool> IsStable { get; }
        IReadOnlyReactiveProperty<ItemType> Item { get; }

        /// <summary>Per-type nudge distance override. Null = use global config default.</summary>
        float? NudgeDistanceOverride { get; }

        /// <summary>Per-type nudge duration override. Null = use global config default.</summary>
        float? NudgeDurationOverride { get; }
    }
}

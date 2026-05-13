using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
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

        /// <summary>Per-type nudge overrides. Empty/null = use global config defaults.</summary>
        NudgeOverride[] NudgeOverrides { get; }
    }
}

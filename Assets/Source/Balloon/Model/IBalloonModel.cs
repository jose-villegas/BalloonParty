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
        bool CanHoldItem { get; }
        bool IsPaintable { get; }
        int ScoreValue { get; }

        /// <summary>Per-type nudge overrides. Empty/null = use global config defaults.</summary>
        NudgeOverride[] NudgeOverrides { get; }

        /// <summary>
        ///     Pure query: given incoming damage, will this balloon deflect or pop?
        ///     Unbreakable balloons (HitsRemaining == -1) always deflect.
        /// </summary>
        HitOutcome EvaluateHit(int damage);
    }
}

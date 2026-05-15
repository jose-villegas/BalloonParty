using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    public class BalloonModel : IWriteableBalloonModel
    {
        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<BalloonType> TypeName { get; } = new();
        public ReactiveProperty<int> HitsRemaining { get; } = new(1);
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);

        public NudgeOverride[] NudgeOverrides { get; set; }
        public bool CanHoldItem { get; set; } = true;
        public int ScoreValue { get; set; } = 1;

        IReadOnlyReactiveProperty<string> IBalloonModel.Color => Color;
        IReadOnlyReactiveProperty<BalloonType> IBalloonModel.TypeName => TypeName;
        IReadOnlyReactiveProperty<int> IBalloonModel.HitsRemaining => HitsRemaining;
        IReadOnlyReactiveProperty<Vector2Int> IBalloonModel.SlotIndex => SlotIndex;
        IReadOnlyReactiveProperty<bool> IBalloonModel.IsStable => IsStable;
        IReadOnlyReactiveProperty<ItemType> IBalloonModel.Item => Item;

        public HitOutcome EvaluateHit(int damage)
        {
            if (HitsRemaining.Value == -1)
            {
                return HitOutcome.Deflect;
            }

            return HitsRemaining.Value - damage > 0 ? HitOutcome.Deflect : HitOutcome.Pop;
        }
    }
}

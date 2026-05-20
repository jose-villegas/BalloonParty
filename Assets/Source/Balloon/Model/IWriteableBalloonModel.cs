using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    public interface IWriteableBalloonModel : IWriteableSlotActor, IBalloonModel
    {
        new ReactiveProperty<string> Color { get; }
        new ReactiveProperty<BalloonType> TypeName { get; }
        new ReactiveProperty<int> HitsRemaining { get; }
        new ReactiveProperty<ItemType> Item { get; }

        new NudgeOverride[] NudgeOverrides { get; set; }
        new bool CanHoldItem { get; set; }
        new int ScoreValue { get; set; }
    }
}

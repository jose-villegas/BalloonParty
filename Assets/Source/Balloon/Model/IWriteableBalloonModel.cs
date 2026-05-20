using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    public interface IWriteableBalloonModel : IWriteableDynamicSlotActor, IBalloonModel
    {
        new ReactiveProperty<string> Color { get; }
        new BalloonType TypeName { get; init; }
        // Writable for spawn-time writes; IHasDurability exposes the read-only view.
        new ReactiveProperty<int> HitsRemaining { get; }
        new ReactiveProperty<ItemType> Item { get; }

        new NudgeOverride[] NudgeOverrides { get; init; }
        new bool CanHoldItem { get; init; }
        new int ScoreValue { get; init; }
    }
}

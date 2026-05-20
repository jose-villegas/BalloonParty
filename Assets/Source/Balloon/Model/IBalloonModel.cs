using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Slots;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    public interface IBalloonModel : ISlotActor, IHasColor, IHasScore, IHasNudge
    {
        IReadOnlyReactiveProperty<BalloonType> TypeName { get; }
        IReadOnlyReactiveProperty<int> HitsRemaining { get; }
        IReadOnlyReactiveProperty<ItemType> Item { get; }
        bool CanHoldItem { get; }

        /// <summary>
        ///     Pure query: given incoming damage, will this balloon deflect or pop?
        ///     Unbreakable balloons (HitsRemaining == -1) always deflect.
        /// </summary>
        HitOutcome EvaluateHit(int damage);
    }
}

using System.Collections.Generic;
using BalloonParty.Shared;

namespace BalloonParty.Game.Upgrades
{
    /// <summary>
    ///     The cards a level-up deals and the run holds. The hand opens when a level-up is presented
    ///     (<c>LevelUpPhase.Pending</c>), and a pick is only held once that level-up advances the level
    ///     (<c>Transitioning</c>); an aborted level-up voids the hand and any pick with it.
    /// </summary>
    internal interface IUpgradeDraft
    {
        /// <summary>
        ///     The hand dealt by the level-up being presented. Empty when no level-up is pending or this one
        ///     deals none, which the ceremony reads as "no screen". Seeded by the level, so a retry that
        ///     reaches the same level-up with the same cards is dealt the same hand.
        /// </summary>
        IReadOnlyList<IUpgradeCardSettings> Offer { get; }

        /// <summary>
        ///     The bonus card a retry bought with a rewarded ad was dealt, for that attempt only; null when
        ///     none. Never an Extra Pen, and not counted in <see cref="StacksOf" />.
        /// </summary>
        IUpgradeCardSettings BonusCard { get; }

        /// <summary>How many times the run holds <paramref name="card" />.</summary>
        int StacksOf(IUpgradeCardSettings card);

        /// <summary>
        ///     The most times the run can hold <paramref name="card" />: its <c>MaxStacks</c>, plus what the held
        ///     <see cref="UpgradeKind.RaiseLimits" /> cards add (to every card but themselves).
        /// </summary>
        int LimitOf(IUpgradeCardSettings card);

        /// <summary>
        ///     Stages the card in <paramref name="slot" /> of <see cref="Offer" /> as this level-up's pick.
        ///     False, and nothing staged, when no hand is open, a pick is already staged, or the slot is
        ///     outside the hand.
        /// </summary>
        bool Pick(int slot);
    }
}

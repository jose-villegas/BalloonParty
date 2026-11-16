using System.Collections.Generic;

namespace BalloonParty.Shared
{
    /// <summary>What an upgrade card changes. Each kind is read at one decision point.</summary>
    public enum UpgradeKind
    {
        /// <summary>+<see cref="IUpgradeCardSettings.Amount" /> balls in flight at once (rounded).</summary>
        BallCap,

        /// <summary>Reload time multiplied by (1 − <see cref="IUpgradeCardSettings.Amount" />) per stack.</summary>
        FasterReload,

        /// <summary>+<see cref="IUpgradeCardSettings.Amount" /> starting shields per ball (rounded).</summary>
        Shield,

        /// <summary>+<see cref="IUpgradeCardSettings.Amount" /> chance of one more item per item roll.</summary>
        ItemOdds,

        /// <summary>
        ///     +<see cref="IUpgradeCardSettings.Amount" /> to every other card's <see cref="IUpgradeCardSettings.MaxStacks" />
        ///     (rounded). Dealt only once another card has reached its limit.
        /// </summary>
        RaiseLimits,

        /// <summary>
        ///     +<see cref="IUpgradeCardSettings.Amount" /> chance that each ball the thrower loads starts armed as a
        ///     Snipe lance (the total never passes 1).
        /// </summary>
        SnipeChance,

        /// <summary>
        ///     The reward balloons (golden, heart, disco, nuke) come likelier: every roll that brings one has its chance
        ///     × (1 + <see cref="IUpgradeCardSettings.Amount" />) per stack, up to certain. A sure one stays sure.
        /// </summary>
        RewardOdds
    }

    /// <summary>One card the level-up can offer.</summary>
    public interface IUpgradeCardSettings
    {
        UpgradeKind Kind { get; }
        string Title { get; }
        string Description { get; }

        /// <summary>The per-stack value; what it means depends on <see cref="Kind" />.</summary>
        float Amount { get; }

        /// <summary>
        ///     Most times a run can hold this card, before any <see cref="UpgradeKind.RaiseLimits" /> card; a card at
        ///     its limit stops being offered.
        /// </summary>
        int MaxStacks { get; }

        /// <summary>Relative odds of the card being dealt. 0 keeps it out of the deck.</summary>
        float Weight { get; }

        /// <summary>
        ///     Extra odds of this card being a bought retry's bonus card, on top of <see cref="Weight" />: × (1 + this).
        ///     A retry follows a lost run, so the cards that keep the player alive come likelier. 0 = no extra.
        /// </summary>
        float RetryDealBoost { get; }

        /// <summary>
        ///     The run's first card is this one, whatever deals it: the first level-up's hand (in its first slot, the
        ///     one the level-up takes), a golden balloon or a bought retry's bonus card, while the run holds none.
        /// </summary>
        bool DealtFirst { get; }
    }

    /// <summary>When the level-up deals cards, how many, and from which deck.</summary>
    public interface IUpgradeSettings
    {
        /// <summary>A pick every this many level-ups; other level-ups pass with no screen. 0 = never.</summary>
        int CardEveryLevels { get; }

        /// <summary>Cards dealt per pick; fewer when fewer cards are left unmaxed.</summary>
        int CardsOffered { get; }

        IReadOnlyList<IUpgradeCardSettings> Cards { get; }

        /// <summary>
        ///     Seconds of game time after a ball loads as a Snipe (<see cref="UpgradeKind.SnipeChance" />) before the
        ///     next load can roll again. 0 = every load rolls.
        /// </summary>
        float SnipeCooldownSeconds { get; }
    }
}

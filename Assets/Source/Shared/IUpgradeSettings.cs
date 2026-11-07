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
        ItemOdds
    }

    /// <summary>One card the level-up can offer.</summary>
    public interface IUpgradeCardSettings
    {
        UpgradeKind Kind { get; }
        string Title { get; }
        string Description { get; }

        /// <summary>The per-stack value; what it means depends on <see cref="Kind" />.</summary>
        float Amount { get; }

        /// <summary>Most times a run can hold this card; a maxed card stops being offered.</summary>
        int MaxStacks { get; }

        /// <summary>Relative odds of the card being dealt. 0 keeps it out of the deck.</summary>
        float Weight { get; }
    }

    /// <summary>When the level-up deals cards, how many, and from which deck.</summary>
    public interface IUpgradeSettings
    {
        /// <summary>A pick every this many level-ups; other level-ups pass with no screen. 0 = never.</summary>
        int CardEveryLevels { get; }

        /// <summary>Cards dealt per pick; fewer when fewer cards are left unmaxed.</summary>
        int CardsOffered { get; }

        IReadOnlyList<IUpgradeCardSettings> Cards { get; }
    }
}

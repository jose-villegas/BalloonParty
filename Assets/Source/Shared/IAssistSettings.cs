namespace BalloonParty.Shared
{
    /// <summary>
    ///     Help that comes only when the player needs it: how need is judged (<c>Game/Assist/PlayerNeed</c>) and
    ///     how each chance-rolled help forgives misses made in need (<see cref="IPitySettings" />).
    /// </summary>
    public interface IAssistSettings
    {
        /// <summary>
        ///     How much the danger signal counts toward need, 0 to 1: at 1, a board one line from a lost heart is
        ///     as much need as the last heart. Need is the larger of the hearts' and the danger's.
        /// </summary>
        float DangerNeedWeight { get; }

        /// <summary>The Sharpshooter card's Snipe roll.</summary>
        IPitySettings SnipePity { get; }

        /// <summary>
        ///     How much armour left standing counts toward the Snipe roll's need, 0 to 1: at full armour
        ///     (<see cref="ArmourCountForFull" /> toughs or unbreakables, each <see cref="ArmourSecondsForFull" /> on the
        ///     board) the roll reads this much need, so its pity builds even while the hearts are full. The roll takes
        ///     the larger of this and the player's need. 0 = armour never counts.
        /// </summary>
        float ArmourNeedWeight { get; }

        /// <summary>How many armoured balloons, each standing its full age, make full armour.</summary>
        float ArmourCountForFull { get; }

        /// <summary>Seconds of game time an armoured balloon stands before it counts in full; it counts from 0 as it lands.</summary>
        float ArmourSecondsForFull { get; }

        /// <summary>
        ///     Bounces off the run's first golden balloon after which the next ball loaded is a Snipe lance, whatever
        ///     the Sharpshooter roll, so the player learns what pops it. Once a run. 0 = never.
        /// </summary>
        int FirstGoldenSnipeAfterDeflects { get; }

        /// <summary>The heart balloon's roll on an ordinary level.</summary>
        IPitySettings HeartPity { get; }

        /// <summary>A cadence wave's roll for any item at all, off its item-count curve.</summary>
        IPitySettings ItemPity { get; }

        /// <summary>
        ///     How much the threats that come by chance ease off at full need, 0 to 1: the extra balloon a pop can
        ///     spawn and an untouched formation's call for reinforcements. 0.5 = half as likely at the last heart.
        /// </summary>
        float ThreatEaseAtFullNeed { get; }
    }
}

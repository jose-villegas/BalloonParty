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

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
    }
}

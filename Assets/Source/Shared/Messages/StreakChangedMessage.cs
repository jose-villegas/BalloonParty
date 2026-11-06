using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published whenever a colour pop-streak changes — grew, switched colour, broke, or reset.
    ///     A colour switch changes two colours at once (old drops to 0, new starts at 1), so consumers
    ///     should re-query <c>IColorStreak.GetStreak</c> for their own colour rather than trust the payload.
    /// </summary>
    public readonly struct StreakChangedMessage
    {
        /// <summary>
        ///     The ball whose streak changed, or null for the shared streak (pops no ball caused) and for
        ///     the reset of every streak at level-up. With several balls in flight one ball's reset is not
        ///     the streak breaking, so a consumer that tracks one ball checks this first.
        /// </summary>
        public readonly IProjectileModel Projectile;

        public readonly string Color;
        public readonly int Streak;

        public StreakChangedMessage(IProjectileModel projectile, string color, int streak)
        {
            Projectile = projectile;
            Color = color;
            Streak = streak;
        }
    }
}

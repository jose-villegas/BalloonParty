namespace BalloonParty.Shared.Messages
{
    /// <summary>What gave a ball a shield, so a reward can be told apart from a pickup where it's shown.</summary>
    public enum ShieldGainSource
    {
        /// <summary>A shield item on a popped balloon.</summary>
        Item,

        /// <summary>A continued colour streak (<see cref="StreakShieldRule" />).</summary>
        Streak,

        /// <summary>Deflects in a row without a wall (<see cref="DeflectChainRule" />).</summary>
        Ricochet
    }
}

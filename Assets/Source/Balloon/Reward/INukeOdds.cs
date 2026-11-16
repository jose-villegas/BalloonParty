namespace BalloonParty.Balloon.Reward
{
    /// <summary>The nuke balloon's chance on a clock line (PLAN-NukeBalloon), raised by the trouble the player is in.</summary>
    internal interface INukeOdds
    {
        /// <summary>The chance this clock line brings a nuke, read when the line rolls.</summary>
        float Chance { get; }

        /// <summary>A clock line has rolled: the hearts lost recently fade by a line.</summary>
        void LinePassed();
    }
}

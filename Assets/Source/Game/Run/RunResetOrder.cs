namespace BalloonParty.Game.Run
{
    /// <summary>Ordering for <see cref="IRunResettable.ResetOrder"/> — ascending, lower runs first.</summary>
    internal static class RunResetOrder
    {
        public const int Quiesce = 0;
        public const int Board = 20;
        public const int Derived = 40;
        public const int Counters = 60;
        public const int Score = 100;
        public const int Respawn = 120;
    }
}

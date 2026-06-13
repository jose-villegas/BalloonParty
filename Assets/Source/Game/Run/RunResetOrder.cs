namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Ordering for <see cref="IRunResettable.ResetOrder"/> — ascending, lower runs first.
    ///     A restart must quiesce in-flight async/tweens, then clear the board, then rebuild
    ///     derived state, then reset counters, then run-level score, and finally repopulate the
    ///     fresh board. Implementations pick the stage they belong to so a new resettable never
    ///     has to guess a magic number.
    /// </summary>
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

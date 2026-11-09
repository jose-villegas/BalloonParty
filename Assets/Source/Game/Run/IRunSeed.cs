namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     The run's own roll, mixed into what may differ from one run to the next (which levels are on the
    ///     ground). Rolled fresh for each new run and kept through a retry, so a retried level plays as before.
    /// </summary>
    internal interface IRunSeed
    {
        int Value { get; }
    }
}

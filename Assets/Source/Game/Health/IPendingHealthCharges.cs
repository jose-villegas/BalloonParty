namespace BalloonParty.Game.Health
{
    /// <summary>
    ///     How many hit-point charges are already committed but not yet applied — the overflow pile's
    ///     queued balloons, each of which will unconditionally cost one HP when its heart launches
    ///     (nothing cancels a queued balloon except a run reset). Implemented by the overflow effect;
    ///     consumed by <see cref="ILossForecast"/>.
    /// </summary>
    internal interface IPendingHealthCharges
    {
        int PendingCharges { get; }
    }
}

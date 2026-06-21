namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     The player's current-HP counter. A distinct type so <c>HealthUILifetimeScope</c> can gather it
    ///     independently of other counters; all behaviour lives in <see cref="ReactiveCounterLabel" />.
    /// </summary>
    internal sealed class HealthCounterLabel : ReactiveCounterLabel
    {
    }
}

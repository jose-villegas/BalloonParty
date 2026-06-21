namespace BalloonParty.UI.Shields
{
    /// <summary>
    ///     The loaded projectile's remaining-shields counter. A distinct type so the shield UI can gather
    ///     it independently; all behaviour lives in <see cref="ReactiveCounterLabel" />. Bound directly by
    ///     <see cref="ShieldCounterAnimation" /> on projectile load, not by a Start-time binder.
    /// </summary>
    internal sealed class ShieldCounterLabel : ReactiveCounterLabel
    {
    }
}

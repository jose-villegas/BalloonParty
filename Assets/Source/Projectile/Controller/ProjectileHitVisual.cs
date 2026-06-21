namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     The view-side follow-up a hit resolution asks for. The rule logic and all model/message
    ///     effects already happened inside <see cref="ProjectileHitResolver" />; this only tells the
    ///     <c>ProjectileView</c> which of its own visuals to play.
    /// </summary>
    internal enum ProjectileHitVisual
    {
        None,
        Recolored,
        Destroyed
    }
}

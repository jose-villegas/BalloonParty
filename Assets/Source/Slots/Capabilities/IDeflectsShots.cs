namespace BalloonParty.Slots.Capabilities
{
    /// <summary>
    ///     A slot actor that bounces an ordinary shot rather than letting it through or popping.
    /// </summary>
    /// <remarks>
    ///     Asked by the aim telegraph and the shield planner, both of which need the answer WITHOUT
    ///     mutating anything — <see cref="IHitable.EvaluateHit" /> spends durability, so it cannot be
    ///     used to preview. A capability rather than a base-class rule because deflecting is not a
    ///     balloon trait: the static archetypes deflect too, and a rule living on the balloon
    ///     hierarchy is invisible to them by construction.
    /// </remarks>
    public interface IDeflectsShots
    {
        /// <summary>Whether the next ordinary (non-piercing, 1 damage) hit would bounce.</summary>
        bool DeflectsOrdinaryHit { get; }
    }
}

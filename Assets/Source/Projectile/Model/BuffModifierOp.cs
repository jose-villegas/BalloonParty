namespace BalloonParty.Projectile.Model
{
    /// <summary>
    ///     How a buff's value is combined with other modifiers targeting the same stat.
    ///     Evaluation order: base + Flat → × (1 + Additive sum) → × Multiplicative product.
    /// </summary>
    public enum BuffModifierOp
    {
        /// <summary>Added directly to the base value before percentage scaling.</summary>
        Flat,

        /// <summary>Summed with other additives, then applied as (1 + sum) multiplier. E.g. two +0.2 = ×1.4.</summary>
        Additive,

        /// <summary>Each multiplicative modifier multiplies independently. E.g. two ×2 = ×4.</summary>
        Multiplicative
    }
}

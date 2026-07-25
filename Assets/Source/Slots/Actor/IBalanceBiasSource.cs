namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     The read-set the balance-bias formulas (<see cref="BalloonParty.Shared.Extensions.BalanceBiasExtensions" />)
    ///     need from a NEIGHBOUR occupant to test same-color and same-type — the minimal surface that lets
    ///     both a live balloon model and a (future) sim stub actor drive identical formulas without either
    ///     side depending on the other's namespace.
    /// </summary>
    internal interface IBalanceBiasSource
    {
        /// <summary>Palette color ID, or empty when colorless.</summary>
        string ColorId { get; }

        /// <summary>Opaque per-type identifier (NOT <c>BalloonType</c> — Slots must not reference Balloon);
        /// equal for two occupants a live model would consider the "same type" for line/clump bias.</summary>
        int BiasTypeId { get; }

        /// <summary>Which shared formula this occupant's <c>WeightBias</c> applies —
        /// <see cref="BalanceBiasKind.None" /> for an occupant with no bias strategy.</summary>
        BalanceBiasKind BiasKind { get; }

        /// <summary>The bias magnitude the formula named by <see cref="BiasKind" /> scales by (a live
        /// model's config-authored balance bias).</summary>
        float BiasValue { get; }
    }
}

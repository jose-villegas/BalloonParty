namespace BalloonParty.Slots.Capabilities
{
    /// <summary>
    ///     How a balloon reacts when a pressure-balance push reaches it. The cascade is a chain of
    ///     shoves between neighbours; this decides whether a balloon passes the shove along by
    ///     stepping one cell, or gets out of the way entirely — and if so, where it heads.
    /// </summary>
    public enum PressureResponse
    {
        /// <summary>Displaces one cell to a neighbour, continuing the chain (the default balloon).</summary>
        ShoveNeighbour,

        /// <summary>Vacates to the nearest free slot, ending the chain — stays close (BubbleCluster).</summary>
        RelocateNearest,

        /// <summary>Vacates to the farthest free slot, ending the chain — clears right out (Unbreakable).</summary>
        RelocateFarthest
    }
}

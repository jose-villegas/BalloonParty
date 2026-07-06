namespace BalloonParty.Slots.Capabilities
{
    /// <summary>How a balloon reacts when a pressure-balance push reaches it.</summary>
    public enum PressureResponse
    {
        /// <summary>Shoves a neighbour one cell, continuing the chain.</summary>
        ShoveNeighbour,

        /// <summary>Vacates to the nearest free slot, ending the chain.</summary>
        RelocateNearest,

        /// <summary>Vacates to the farthest free slot, ending the chain.</summary>
        RelocateFarthest
    }
}

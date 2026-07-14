namespace BalloonParty.Configuration.Palette
{
    /// <summary>
    ///     How a palette index is packed into a single texture channel for the stamp/field systems:
    ///     <c>(index + 1) / Slots</c>, so 0 always reads "untagged" and indices quantize into
    ///     <see cref="Slots"/> slots. Shared by the disturbance field and the scene-light field (their
    ///     shaders mirror this in HLSL, and decode with <c>round(a * Slots) - 1</c>).
    /// </summary>
    internal static class PaletteChannelEncoding
    {
        /// <summary>Channel slots — also the size of the palette array the shaders declare.</summary>
        internal const int Slots = 16;

        /// <summary>Packs a palette index into its channel value; a negative index encodes to 0 (untagged).</summary>
        internal static float Encode(int paletteIndex)
        {
            return paletteIndex >= 0 ? (paletteIndex + 1f) / Slots : 0f;
        }
    }
}

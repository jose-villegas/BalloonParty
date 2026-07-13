namespace BalloonParty.Configuration.Palette
{
    /// <summary>Base contract for a config entry keyed to a set of palette colours by a
    /// <see cref="PaletteColorMaskAttribute" /> bitfield — "map this thing to these palette colours". Bit
    /// <c>i</c> selects palette colour <c>i</c> (same convention as <see cref="IGamePalette.ColorNamesForMask" />).</summary>
    internal interface IPaletteColorMasked
    {
        int ColorMask { get; }
    }

    internal static class PaletteColorMaskedExtensions
    {
        /// <summary>True if <paramref name="paletteIndex" /> is one of the masked colours.</summary>
        internal static bool Covers(this IPaletteColorMasked masked, int paletteIndex)
        {
            return paletteIndex >= 0 && (masked.ColorMask & (1 << paletteIndex)) != 0;
        }
    }
}

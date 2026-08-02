namespace BalloonParty.Game.Telemetry
{
    // ColorIndex, not a color name — this pure-C# layer has no palette access (R5/R12). The index
    // matches IGamePalette.ProgressColorNames order, with the last index reserved as the "other"
    // bucket for unknown colors and actors without IHasColor; a later, palette-aware wave resolves it.
    // Shared shape for every color-keyed breakdown, not named for pops specifically — PointsBanked
    // (points) breaks down by the same (colorIndex, int) pair as Pops (counts), just through a
    // different AxisSlot.
    internal readonly struct ColorCount
    {
        public readonly int ColorIndex;
        public readonly int Count;

        public ColorCount(int colorIndex, int count)
        {
            ColorIndex = colorIndex;
            Count = count;
        }
    }
}

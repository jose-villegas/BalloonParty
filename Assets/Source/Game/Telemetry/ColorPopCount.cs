namespace BalloonParty.Game.Telemetry
{
    // ColorIndex, not a color name — this pure-C# layer has no palette access (R5/R12). The index
    // matches IGamePalette.ProgressColorNames order, with the last index reserved as the "other"
    // bucket for unknown colors and actors without IHasColor; a later, palette-aware wave resolves it.
    internal readonly struct ColorPopCount
    {
        public readonly int ColorIndex;
        public readonly int Count;

        public ColorPopCount(int colorIndex, int count)
        {
            ColorIndex = colorIndex;
            Count = count;
        }
    }
}

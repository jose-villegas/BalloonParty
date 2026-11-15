namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published by the disco ball's wave each frame it grows, and once with 0 as it starts.
    ///     <see cref="Progress" /> is the share of the wave's time gone (0 to 1), not of its radius, which eases out.
    /// </summary>
    public readonly struct DiscoWaveAdvancedMessage
    {
        public float Progress { get; }

        public DiscoWaveAdvancedMessage(float progress)
        {
            Progress = progress;
        }
    }
}

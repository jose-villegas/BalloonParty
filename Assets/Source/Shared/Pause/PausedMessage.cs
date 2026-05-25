namespace BalloonParty.Shared.Pause
{
    public readonly struct PausedMessage
    {
        public PauseSource Source { get; }

        internal PausedMessage(PauseSource source)
        {
            Source = source;
        }
    }
}


namespace BalloonParty.Shared.Pause
{
    public readonly struct ResumedMessage
    {
        public PauseSource Source { get; }

        internal ResumedMessage(PauseSource source)
        {
            Source = source;
        }
    }
}

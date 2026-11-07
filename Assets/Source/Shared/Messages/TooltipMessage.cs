namespace BalloonParty.Shared.Messages
{
    /// <summary>A short line for the on-screen tooltip, e.g. a level passed in play or a retry's bonus card.</summary>
    public readonly struct TooltipMessage
    {
        public readonly string Text;

        public TooltipMessage(string text)
        {
            Text = text;
        }
    }
}

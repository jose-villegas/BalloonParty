namespace BalloonParty.Shared.Messages
{
    public readonly struct ProgressBarCompletedMessage
    {
        public readonly string ColorName;

        public ProgressBarCompletedMessage(string colorName)
        {
            ColorName = colorName;
        }
    }
}

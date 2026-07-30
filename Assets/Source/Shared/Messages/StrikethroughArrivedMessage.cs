namespace BalloonParty.Shared.Messages
{
    /// <summary>Published when a strikethrough heart trail finishes its path over a blocked spawn line.</summary>
    internal readonly struct StrikethroughArrivedMessage
    {
        /// <summary>Index of the blocked spawn line that was struck through.</summary>
        public readonly int LineIndex;

        public StrikethroughArrivedMessage(int lineIndex)
        {
            LineIndex = lineIndex;
        }
    }
}

namespace BalloonParty.Shared.Messages
{
    /// <summary>Published once per spawn wave when the line-based deficit causes heart loss.</summary>
    internal readonly struct WaveDamageMessage
    {
        /// <summary>Number of hearts lost this wave (full lines of deficit).</summary>
        public readonly int HeartsLost;

        /// <summary>Total balloon slots that couldn't spawn.</summary>
        public readonly int BlockedSlots;

        /// <summary>Grid column count — one row length = one heart.</summary>
        public readonly int RowLength;

        public WaveDamageMessage(int heartsLost, int blockedSlots, int rowLength)
        {
            HeartsLost = heartsLost;
            BlockedSlots = blockedSlots;
            RowLength = rowLength;
        }
    }
}

namespace BalloonParty.Configuration.Level
{
    /// <summary>A catalogue entry the level generator can include in a level.</summary>
    internal interface IContentPiece
    {
        /// <summary>First level the piece can appear at; it's always in for the featured levels from there.</summary>
        int UnlockLevel { get; }

        /// <summary>Budget a level spends to include the piece. 0 = a staple, in every level from its unlock on.</summary>
        int Cost { get; }

        /// <summary>Which budget pays for the piece.</summary>
        ContentRole Role { get; }
    }
}

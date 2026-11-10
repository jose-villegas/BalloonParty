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

        /// <summary>Authored but kept out of generation, e.g. until its art or terrain exists.</summary>
        bool Withheld { get; }

        /// <summary>Whether the piece needs ground under it; sky levels leave it out.</summary>
        bool RequiresGround { get; }

        /// <summary>
        ///     Whether water levels leave the piece out: the water surface has no clouds or bushes, and nothing on it
        ///     bounces shots but the unbreakable balloon.
        /// </summary>
        bool KeptOffWater { get; }
    }
}

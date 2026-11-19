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

        /// <summary>
        ///     Whether a level set on <paramref name="scenario" /> may include the piece. A level leaves out what its
        ///     scenario doesn't allow before any budget is spent, so the budget buys the pieces that are allowed.
        /// </summary>
        bool AllowedOn(ScenarioKind scenario);
    }
}

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     Member names are a telemetry wire contract — `TelemetryEnvelopeSerializer` keys balloon-type
    ///     breakdowns by them, so renaming one silently relabels historical data in the warehouse and
    ///     nothing in the build will complain. Append freely; rename only with that in mind.
    /// </summary>
    public enum BalloonType
    {
        Simple,
        Tough,
        Unbreakable,
        BubbleCluster,

        // Same as Simple — distinct IDs so the level-range type gate can withhold each skin independently.
        SimpleSilver,
        SimpleGold,

        // A colourable BalloonModel that starts in rainbow mode — see RainbowBalloonVariant.
        Rainbow,

        Tougher
    }
}

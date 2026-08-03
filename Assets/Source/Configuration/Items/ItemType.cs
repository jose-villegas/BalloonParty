namespace BalloonParty.Configuration.Items
{
    /// <summary>
    ///     Member names are a telemetry wire contract — `TelemetryEnvelopeSerializer` keys item
    ///     breakdowns by them, so renaming one silently relabels historical data in the warehouse and
    ///     nothing in the build will complain. Append freely; rename only with that in mind.
    /// </summary>
    public enum ItemType
    {
        None,
        Shield,
        Bomb,
        Laser,
        Lightning,
        Paint,
        Snipe
    }
}

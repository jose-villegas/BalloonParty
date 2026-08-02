namespace BalloonParty.Game.Telemetry
{
    // The four nested scopes (R1), ordered innermost to outermost so MetricScope.Absorb can compare
    // ordinals directly: a metric whose catalog-declared scope sits above the child being folded is
    // skipped (see MetricCatalog's Scope column). Also identifies a MetricScope instance itself
    // (MetricScope.Create's first argument) so a malformed scope — e.g. the wrong timer count — cannot
    // be constructed.
    internal enum MetricScopeKind
    {
        Flight,
        Level,
        Run,
        Session
    }
}

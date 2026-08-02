namespace BalloonParty.Game.Telemetry
{
    // How a value combines with itself across a scope boundary (Flight -> Level -> Run -> Session).
    // Drives MetricScope.Absorb mechanically (R4) — never a hand-written per-field accumulate.
    internal enum FoldRule
    {
        Sum,
        Max,
        Min,
        Last
    }
}

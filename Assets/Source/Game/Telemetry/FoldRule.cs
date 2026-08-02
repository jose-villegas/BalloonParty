namespace BalloonParty.Game.Telemetry
{
    // How a value combines with itself across a scope boundary (Flight -> Level -> Run -> Session).
    // Drives MetricScope.Absorb mechanically (R4) — never a hand-written per-field accumulate.
    // No Min: health only ever decreases within a level (the sole recovery is level-up), so a minimum
    // is just the level's end value and is already implied by HeartsLost. If a genuine minimum is ever
    // needed, restoring Min is two lines — but it needs a real unset sentinel, since a zero-initialised
    // counter makes every "record a non-negative minimum" call a silent no-op.
    // Last is only meaningful when every child scope at the folding boundary writes it — Absorb skips a
    // metric declared above the child's own scope (its Scope column), but a metric declared exactly at
    // that scope and left untouched by one particular child still folds its untouched zero straight
    // into Last, overwriting whatever the parent already had.
    internal enum FoldRule
    {
        Sum,
        Max,
        Last
    }
}

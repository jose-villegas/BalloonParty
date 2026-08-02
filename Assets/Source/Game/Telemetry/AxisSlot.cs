namespace BalloonParty.Game.Telemetry
{
    // A dense index into MetricSet's per-slot axis storage — one array per (MetricId, MetricAxis) pair
    // declared in MetricCatalog, not one array per axis kind (see "Axis storage" in
    // PLAN-GameplayTelemetry.md). Without this, Deflects and Pops — which share the BalloonType axis
    // kind — would alias the same table, and PointsBanked (points) would sum into the same Color table
    // as Pops (counts).
    internal readonly struct AxisSlot
    {
        public readonly int Index;

        public AxisSlot(int index)
        {
            Index = index;
        }
    }
}

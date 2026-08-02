namespace BalloonParty.Game.Telemetry
{
    // One row of MetricCatalog.AllSlots: which (MetricId, MetricAxis) pair a dense AxisSlot belongs to
    // and how many buckets it holds. BucketCount is RuntimeSuppliedBucketCount for a Color slot — the
    // color axis size depends on the configured palette (ProgressColorNames plus a trailing "other"
    // bucket), and this pure-C# catalog has no access to it at static-init time, so it genuinely cannot
    // resolve to a real count here (unlike FoldRule.Min, which was a sentinel standing in for a value
    // the catalog could have carried and simply didn't). MetricSet substitutes its own colorAxisSize
    // constructor parameter wherever it sees the sentinel.
    internal readonly struct AxisSlotInfo
    {
        // Named so BuildSlots and MetricSet's constructor both read as "runtime supplies this", not as
        // a bare -1 that happens to mean the same thing in two places.
        public const int RuntimeSuppliedBucketCount = -1;

        public readonly AxisSlot Slot;
        public readonly MetricId Id;
        public readonly MetricAxis Axis;
        public readonly int BucketCount;

        public AxisSlotInfo(AxisSlot slot, MetricId id, MetricAxis axis, int bucketCount)
        {
            Slot = slot;
            Id = id;
            Axis = axis;
            BucketCount = bucketCount;
        }
    }
}

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Tuning for the item-range telegraph. Plain C# with working defaults rather than a
    ///     <c>ScriptableObject</c> — @ref plan_item_range_preview Phase 3 is the visual pass, and until the
    ///     look is settled an authored asset would just be a second place for these numbers to go stale.
    ///     Promote it to an SO (behind a read-only interface, per the config convention) once they stop moving.
    /// </summary>
    internal sealed class ItemPreviewSettings
    {
        /// <summary>Pens spawned per figure, spread round-robin over its strokes.</summary>
        public int PenCount { get; set; } = 6;

        /// <summary>Seconds a pen takes to sweep out from the host to its stroke entry point.</summary>
        public float BloomDuration { get; set; } = 0.35f;

        /// <summary>
        ///     Extra degrees of arc a pen sweeps on the way out, on top of the straight-line bearing. 0 is a
        ///     radial shot outward; larger values curl the launch into the circular motion this is named for.
        /// </summary>
        public float BloomSweepDegrees { get; set; } = 120f;

        /// <summary>World units per second a pen travels along its stroke once it starts tracing.</summary>
        public float TraceSpeed { get; set; } = 6f;

        /// <summary>
        ///     Whether the ribbon draws during the outward bloom. On, the launch spokes are visible (the
        ///     milestone-1 look); off, only the figure itself is drawn — <c>FlyingTrail</c>'s pen-up/pen-down
        ///     note records that deploy spokes can bury the shape, so expect to turn this off once the
        ///     figures matter more than the launch.
        /// </summary>
        public bool EmitDuringBloom { get; set; } = true;
    }
}

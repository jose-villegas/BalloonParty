namespace BalloonParty.Configuration.Level
{
    /// <summary>How <see cref="LevelScoringCurve"/> interpolates the segment following a control point.
    /// All modes guarantee the curve passes exactly through both adjacent CPs.</summary>
    internal enum SegmentMode
    {
        /// <summary>Fritsch–Carlson monotone cubic (default). Smooth transition respecting adjacent slopes.</summary>
        Smooth,

        /// <summary>Straight line between this CP and the next. Constant per-level increment.</summary>
        Linear,

        /// <summary>Accelerating (ease-in). Starts slow, ends fast — convex shape.</summary>
        Convex,

        /// <summary>Decelerating (ease-out). Starts fast, ends slow — concave shape.</summary>
        Concave,
    }
}

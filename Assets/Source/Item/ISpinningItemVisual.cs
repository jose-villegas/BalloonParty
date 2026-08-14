namespace BalloonParty.Item
{
    /// <summary>Read-only spin state for a held item's visual — lets the shot solver's gather
    /// extrapolate a spinning item's contact geometry (the Laser cross) at gather time. Deliberately
    /// separate from <see cref="ITransformCapture" />: its <c>CaptureSnapshot</c> is destructive (it
    /// stops the spin, live-side — see <c>LaserItemRotation.CaptureSnapshot</c>), so gather must never
    /// call it.</summary>
    internal interface ISpinningItemVisual
    {
        /// <summary>Current world-space Z rotation, in degrees.</summary>
        float AngleDegrees { get; }

        float SpinDegreesPerSecond { get; }

        /// <summary>True while <see cref="AngleDegrees" /> is holding still (dwelling on a step, or a
        /// frozen snapshot with nothing left to move), false while a transition toward a new angle is
        /// still in flight. Lets a consumer that only ever sees the raw angle — the aim-time range
        /// telegraph (<c>ItemRangePreviewController</c>) — tell "arrived" from "still easing in" without
        /// inferring it from a frame-to-frame delta, which a <c>Mathf.SmoothStep</c> ease drives toward
        /// zero on its own tail and would misread as arrived early.</summary>
        bool IsSettled { get; }

        /// <summary>How long, in seconds, the item rests at one angle before a transition to the next
        /// begins — the same span <see cref="IsSettled" /> reads true for within one step. Lets a
        /// consumer compare its own re-draw cadence against how long a spin actually dwells
        /// (<c>ItemRangePreviewController</c>'s hold-loop-vs-rotation arbitration) without re-deriving the
        /// rotation's own authored step length and easing fraction.</summary>
        float DwellSeconds { get; }
    }
}

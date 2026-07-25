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
    }
}

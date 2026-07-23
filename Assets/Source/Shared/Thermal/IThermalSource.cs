namespace BalloonParty.Shared.Thermal
{
    /// <summary>
    ///     Latest polled thermal readings for <see cref="ThermalFrameRateGovernor" />. Implementations
    ///     cache values between polls because the Android headroom API is rate-limited (see
    ///     <c>AndroidThermalSource</c>); callers may read these properties every frame cheaply.
    /// </summary>
    internal interface IThermalSource
    {
        /// <summary>
        ///     Android <c>PowerManager.getThermalHeadroom</c> semantics: normalized thermal headroom
        ///     where <c>1.0</c> is the severe-throttling threshold and lower values are cooler. Returns
        ///     the most recently polled value. When the API is unavailable it returns <c>NaN</c>, which
        ///     implementations map to <c>0</c> here (treated as fully cool) so the governor never
        ///     down-steps on a missing signal.
        /// </summary>
        float Headroom { get; }

        /// <summary>
        ///     Android <c>PowerManager.getCurrentThermalStatus</c> value (0 = NONE … 6 = SHUTDOWN);
        ///     <c>-1</c> when unavailable.
        /// </summary>
        int Status { get; }
    }
}

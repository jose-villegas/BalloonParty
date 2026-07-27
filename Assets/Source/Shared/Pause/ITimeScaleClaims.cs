namespace BalloonParty.Shared.Pause
{
    /// <summary>Write surface of the time-scale claim stack. Plain-C# controllers depend on this rather
    /// than on <see cref="TimeScaleService" /> itself, so an edit-mode fixture can substitute it instead
    /// of warping the editor's own clock for the rest of the session.</summary>
    internal interface ITimeScaleClaims
    {
        void Claim(TimeScaleSource source, float value);
        void Release(TimeScaleSource source);

        /// <summary>Takes sole ownership: <c>Apply</c> then uses ONLY this source's value, so other
        /// claimants keep recording and resume correctly on <see cref="ReleaseExclusive" />.</summary>
        void ClaimExclusive(TimeScaleSource source, float value);

        void ReleaseExclusive(TimeScaleSource source);
    }
}

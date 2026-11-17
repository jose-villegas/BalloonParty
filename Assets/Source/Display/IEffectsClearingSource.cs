namespace BalloonParty.Display
{
    /// <summary>
    ///     Where the clearing mask gets its stamps from. The mask view lives on the persistent camera and pulls
    ///     once a frame, so the run-scoped source never races its <c>LateUpdate</c>.
    /// </summary>
    internal interface IEffectsClearingSource
    {
        /// <summary>Whether any ball is in flight, which puts the clearing's clock on game time.</summary>
        bool AnyInFlight { get; }

        /// <summary>Writes this frame's stamps into <paramref name="stamps" /> and returns how many;
        /// <paramref name="deltaTime" /> moves the fading ones on.</summary>
        int FillStamps(in ClearingView view, float deltaTime, EffectsClearingStamps stamps);
    }
}

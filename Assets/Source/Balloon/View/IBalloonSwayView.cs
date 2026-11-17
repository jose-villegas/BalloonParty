namespace BalloonParty.Balloon.View
{
    /// <summary>The surface <c>BalloonSwayTicker</c> writes a balloon's idle sway to.</summary>
    internal interface IBalloonSwayView
    {
        /// <summary>Tilts the swayed child to <paramref name="degrees" /> about its local Z.</summary>
        void ApplySway(float degrees);
    }
}

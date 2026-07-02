namespace BalloonParty.Shared.Pause
{
    public enum TimeScaleSource
    {
        /// <summary>A camera-rig cinematic segment is warping time (slow-mo ramp / restore).</summary>
        Cinematic,

        /// <summary>The level-up popup freezes the world (0) while it is on screen.</summary>
        LevelUpPopup
    }
}

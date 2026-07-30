namespace BalloonParty.Shared.Pause
{
    public enum TimeScaleSource
    {
        /// <summary>A camera-rig cinematic segment is warping time (slow-mo ramp / restore).</summary>
        Cinematic,

        /// <summary>The level-up popup freezes the world (0) while it is on screen.</summary>
        LevelUpPopup,

        /// <summary>The level-transition Ascent's slow-mo while the old level's balloons pop.</summary>
        LevelTransition,

        /// <summary>A shot's doomed 'last breath' — bullet-time while it drifts to the wall it dies on.</summary>
        LastShield,

        /// <summary>A brief slow-mo dip as a piercing shot discharges — shattering the toughs it plowed.</summary>
        PierceDischarge,

        /// <summary>The level-up ceremony's tipping beat and its hand-off to the popup. Claimed
        /// EXCLUSIVELY for the beat so a doomed shot's LastShield curve can't drag it slower than the
        /// ceremony asked for.</summary>
        LevelUpCeremony,

        /// <summary>Player holding during projectile flight — speeds the game up to allow fast-forwarding.</summary>
        HoldSpeedUp
    }
}

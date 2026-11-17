namespace BalloonParty.Configuration.Balloons
{
    /// <summary>One looping sway of the balloon's pivot, baked from an Animator state and the transitions into it.</summary>
    public interface IBalloonSwayLoop
    {
        /// <summary>The loop's length.</summary>
        float Seconds { get; }

        /// <summary>
        ///     Seconds a freshly spawned balloon takes to enter this loop. Nothing fades over them (the loop
        ///     plays whole from the frame it's asked for), but a flip of the wanted loop waits for them to pass.
        /// </summary>
        float EnterSeconds { get; }

        /// <summary>Seconds the crossfade from the other loop into this one takes.</summary>
        float SwitchSeconds { get; }

        /// <summary>
        ///     The pivot's local Z angle, in degrees, <paramref name="seconds" /> into the loop, as the Animator
        ///     plays the clip.
        /// </summary>
        float AngleAt(float seconds);
    }
}

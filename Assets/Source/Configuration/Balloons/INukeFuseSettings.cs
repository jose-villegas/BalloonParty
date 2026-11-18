namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     The armed nuke's fuse (PLAN-NukeBalloon): how long it counts down before it goes off, and how it pulls the
    ///     board in towards it while it does.
    /// </summary>
    public interface INukeFuseSettings
    {
        /// <summary>
        ///     Seconds the counter's heart flashes after the hit that arms it, before the count starts. The blast
        ///     comes this long plus <see cref="Seconds" /> after that hit.
        /// </summary>
        float HeartSeconds { get; }

        /// <summary>Seconds the count runs, to the blast. The counter on its body shows the whole seconds left.</summary>
        float Seconds { get; }

        /// <summary>Seconds between two pulls on the balloons around it.</summary>
        float PullInterval { get; }

        /// <summary>World units from the nuke within which a balloon is pulled.</summary>
        float PullRadius { get; }

        /// <summary>How far the first pull draws a balloon towards the nuke, in world units.</summary>
        float PullDistanceStart { get; }

        /// <summary>How far the last pull draws it: the pull grows over the fuse.</summary>
        float PullDistanceEnd { get; }

        /// <summary>Seconds one pull takes, out and back.</summary>
        float PullSeconds { get; }

        /// <summary>World radius of the disturbance field's implosion at the nuke while the fuse runs.</summary>
        float ImplodeRadius { get; }

        /// <summary>The implosion's strength: it pulls the clouds, grass and specks in.</summary>
        float ImplodeStrength { get; }
    }
}

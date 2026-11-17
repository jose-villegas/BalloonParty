namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     A trial look: a spring between each pair of neighbours in the tough snake, from knot to knot, simulated
    ///     like the launch screen's spring line. It sags, stretches and swings as the snake steps.
    /// </summary>
    public interface IToughSnakeSpringSettings
    {
        bool Enabled { get; }

        /// <summary>Springs along each link, knot to knot.</summary>
        int Segments { get; }

        /// <summary>A link's rest length, as a share of the distance between neighbouring slots.</summary>
        float Slack { get; }

        /// <summary>How hard each spring holds its length, per solver pass: 1 rigid, lower stretches and bounces.</summary>
        float Stiffness { get; }

        /// <summary>Solver passes a step.</summary>
        int Iterations { get; }

        /// <summary>World units a second squared the links sag under.</summary>
        float Gravity { get; }

        /// <summary>Share of its speed a point loses each step.</summary>
        float Damping { get; }

        float StepsPerSecond { get; }

        /// <summary>The line's thickness, in world units.</summary>
        float Width { get; }

        /// <summary>How much of the knots' colour the line takes; the rest is <see cref="UnityEngine.Color.black" />.</summary>
        float KnotTint { get; }
    }
}

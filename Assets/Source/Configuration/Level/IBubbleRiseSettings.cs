namespace BalloonParty.Configuration.Level
{
    /// <summary>Read-only tuning for the bubbles that come in with the lines on water levels; see <see cref="BubbleRiseCurve" />.</summary>
    internal interface IBubbleRiseSettings
    {
        /// <summary>Seconds of play at a water level's start before any line brings a bubble.</summary>
        float GraceSeconds { get; }

        /// <summary>Share of a line's places that are bubbles as the grace ends (0..1).</summary>
        float StartShare { get; }

        /// <summary>Share of a line's places that are bubbles once the ramp is done (0..1).</summary>
        float MaxShare { get; }

        /// <summary>Seconds after the grace the share takes to climb from start to max.</summary>
        float RampSeconds { get; }

        /// <summary>Most bubbles on the board at once; past it a line brings the usual balloons. 0 = no cap.</summary>
        int MaxBubbles { get; }

        /// <summary>Bubbles in a cluster a line brings, so merges have room to grow it.</summary>
        int StartBubbles { get; }
    }
}

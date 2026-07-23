using System.Collections.Generic;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Tuning for the thermal frame-rate governor (<c>Shared/Thermal/ThermalFrameRateGovernor</c>):
    ///     the rate ladder plus the asymmetric-hysteresis thresholds and windows that decide when to
    ///     step between rungs.
    /// </summary>
    internal interface IThermalGovernorSettings
    {
        /// <summary>When false the governor never changes the frame rate (boot vote stands).</summary>
        bool Enabled { get; }

        /// <summary>Frame-rate rungs, fastest first (e.g. 120, 80, 60). Index 0 is the top rung.</summary>
        IReadOnlyList<int> RateLadder { get; }

        /// <summary>Headroom at/above which the device counts as hot and a down-step is armed.</summary>
        float DownHeadroom { get; }

        /// <summary>Headroom at/below which the device counts as cool and an up-step is armed.</summary>
        float UpHeadroom { get; }

        /// <summary>Sustained hot time (seconds) before stepping down one rung.</summary>
        float DownSustainSeconds { get; }

        /// <summary>Sustained cool time (seconds) before stepping up one rung.</summary>
        float UpSustainSeconds { get; }

        /// <summary>Minimum time (seconds) at the current rung before any up-step is permitted.</summary>
        float MinDwellSeconds { get; }

        /// <summary>Evaluation cadence (seconds); the governor re-reads the source and ticks its timers this often.</summary>
        float PollIntervalSeconds { get; }
    }
}

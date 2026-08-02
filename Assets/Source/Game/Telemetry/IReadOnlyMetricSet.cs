using System.Collections.Generic;

namespace BalloonParty.Game.Telemetry
{
    // Read-only face of MetricSet — the segregated-interface shape the repo already uses for
    // write/read seams (IFlightStats/IFlightStatsWriter). Snapshots and future readers see only this.
    internal interface IReadOnlyMetricSet
    {
        int this[MetricId id] { get; }

        IReadOnlyList<int> ColorAxis { get; }

        IReadOnlyList<int> BalloonTypeAxis { get; }

        IReadOnlyList<int> ItemTypeAxis { get; }
    }
}

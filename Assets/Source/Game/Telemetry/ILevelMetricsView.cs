using UniRx;

namespace BalloonParty.Game.Telemetry
{
    // The UI read seam (R16). Only BalloonParty.UI.* types may inject this (R20) — the implementation
    // (GameplayMetricsService) ships in a later wave; this interface depends on nothing but the
    // snapshots, so it can ship now and let that wave implement against a fixed contract.
    internal interface ILevelMetricsView
    {
        IReadOnlyReactiveProperty<LevelMetricsSnapshot> CeremonyLevel { get; }

        LevelMetricsSnapshot LastFlushedLevel { get; }

        RunMetricsSnapshot Run { get; }
    }
}

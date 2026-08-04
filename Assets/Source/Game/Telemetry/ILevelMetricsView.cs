using UniRx;

namespace BalloonParty.Game.Telemetry
{
    // The UI read seam (R16). Only BalloonParty.UI.* types may inject this (R20).
    //
    // All three are reactive on purpose. Every one of them is assigned from inside a message handler,
    // so a view that read a plain property from its own handler for the same message would get the
    // PREVIOUS value whenever its subscription happened to precede the service's — a game-over screen
    // showing the last run's stats, reproducible only by subscription order. Binding removes the
    // question entirely.
    internal interface ILevelMetricsView
    {
        IReadOnlyReactiveProperty<LevelMetricsSnapshot> CeremonyLevel { get; }

        IReadOnlyReactiveProperty<LevelMetricsSnapshot> LastFlushedLevel { get; }

        IReadOnlyReactiveProperty<RunMetricsSnapshot> Run { get; }
    }
}

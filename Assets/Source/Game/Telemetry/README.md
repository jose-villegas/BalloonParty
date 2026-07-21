# Telemetry

Passive gameplay recorder. Listens to the game's internal event stream, keeps running
totals for each level and each run in memory, and saves structured records to a local
log file at level boundaries and game-over.

The service never sends events back, never modifies gameplay state, and never causes
stutters during action — it only counts.

## Contents

| File | What it does |
|---|---|
| `GameplayTelemetryService` | Entry point (`IStartable`, `IDisposable`, `IRunResettable`). Orchestrates internal helpers; subscribes to gameplay messages and delegates to accumulators. Flushes on `LevelUpDismissedMessage` and `GameOverMessage` |
| `LevelTelemetryAccumulator` | Mutable counters and stopwatches for one level. Pre-sizes collections; exposes `Reset()` for reuse without reallocation |
| `RunTelemetryAccumulator` | Run-wide totals and bests. `Absorb(LevelRecord)` folds each flushed level into the run |
| `TelemetryPauseTracker` | Per-source pause depth tracking. `IsPaused` is true when any source has paused without resuming |
| `TelemetryStopwatch` | Pure C# timer driven by explicit `Advance(float)` calls. No wall-clock dependency — fully deterministic in tests |
| `TelemetrySnapshotFactory` | Converts accumulator state into immutable `LevelRecord` / `RunRecord` DTOs. Reads `CheatState` at snapshot time |
| `LevelRecord` | Sealed DTO capturing one level's statistics (pops, shots, duration, items, streaks, overflow) |
| `RunRecord` | Sealed DTO capturing the full-run summary (levels completed, total score, loss cause, timestamp) |
| `ColorPopCount` | Readonly struct — one color name + pop count pair |
| `ItemActivationCount` | Readonly struct — one item type + activation count pair |
| `ITelemetrySink` | Interface for record output — `Write(LevelRecord)`, `Write(RunRecord)` |
| `NoOpTelemetrySink` | Empty-body sink registered in release builds so the service never null-checks |
| `JsonLinesTelemetrySink` | Sink that writes one JSON object per line to a rotating log file in `Application.persistentDataPath/telemetry/`. Active only in editor and development builds |

## Registration

`GameScopeRegistration.RegisterGameplaySystems` — registered after `SpaceDanger`.
In dev builds `JsonLinesTelemetrySink` is registered as a Singleton; in release builds
`NoOpTelemetrySink` takes its place. The service always calls `sink.Write(...)` with no
null-check.

## Design plan

Full specification: @ref plan_gameplay_telemetry

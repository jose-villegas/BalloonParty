# Telemetry

Passive gameplay recorder. Listens to the game's internal event stream, keeps running
totals for each level and each run in memory, and saves structured records to a local
log file at level boundaries and game-over. Dev builds only — release builds register
none of it.

The service never sends events back, never modifies gameplay state, and never causes
stutters during action — it only counts.

## Contents

| File | What it does |
|---|---|
| `GameplayTelemetryService` | Entry point (`IStartable`, `IDisposable`, `IRunResettable`). Five-state flush machine (Idle/Playing/Ceremony/Transitioning/Ended); subscribes to gameplay messages and delegates to accumulators. Flushes on `LevelTransitionCompletedMessage` and `GameOverMessage` |
| `LevelTelemetryAccumulator` | Mutable counters and stopwatches for one level. Pre-sizes collections; `Snapshot()` produces the `LevelRecord`; `Reset()` reuses the instance without reallocation |
| `RunTelemetryAccumulator` | Run-wide totals and bests. `Absorb(LevelRecord)` folds each flushed level into the run (only `Completed` levels count toward `LevelsCompleted`); `Snapshot()` produces the `RunRecord` |
| `TelemetryStopwatch` | Pure C# timer that owns its injected `Func<float>` clock and folds elapsed time on `Pause()`/`Resume()`/`Elapsed` reads. Deterministic in tests via a fake clock |
| `LevelRecord` | Sealed DTO capturing one level's statistics (pops, shots, duration, items, streaks, overflow) |
| `RunRecord` | Sealed DTO capturing the full-run summary (levels completed, total score, end cause, timestamp) |
| `ColorPopCount` | Readonly struct — one color name + pop count pair |
| `ItemActivationCount` | Readonly struct — one item type + activation count pair |
| `ITelemetrySink` | Interface for record output — `Write(LevelRecord)`, `Write(RunRecord)` |
| `JsonLinesTelemetrySink` | Sink that writes one hand-serialized JSON object per line to a rotating log file in `Application.persistentDataPath/telemetry/`. One stream per session; never throws past its own boundary |
| `TelemetryJson` | Static, reflection-free JSON writer for the two record types (reused `StringBuilder`, `InvariantCulture` throughout) |

Pause handling subscribes `PauseService.IsAnyPaused` directly (reference-counted and
reset-safe) rather than counting `PausedMessage`/`ResumedMessage` edges, which leak on
the loss path.

## Registration

Appended at the end of `GameScopeRegistration.RegisterGameplaySystems`, with the
service and sink registrations wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
Release builds contain no telemetry code paths.

## Design plan

Full specification: @ref plan_gameplay_telemetry

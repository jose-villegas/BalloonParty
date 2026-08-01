# Telemetry

Gameplay metrics. Counts what happens during play at four nested scopes —
**Session ⊃ Run ⊃ Level ⊃ Flight** — and serves those counts to three consumers: the
level-up and game-over popups, an external analytics export, and balance analysis.

The service never publishes, never modifies gameplay state, and never causes stutters
during action — it only counts. Views read immutable snapshots; they never see a live
accumulator.

> **Nothing here is implemented yet.** This README describes the design the revised plan
> specifies (see below), so the folder reads coherently while the waves land. The file
> table is the target layout, not the current one.

## Contents

| File | What it does |
|---|---|
| `MetricId` / `TimerId` / `MetricAxis` | The vocabulary — counters, clocks, and the three dimension axes (color, balloon type, item type). Append-only once shipped |
| `MetricCatalog` | Static table: metric id → wire name, unit, fold rule (`Sum`/`Max`/`Last`). The single browsable list of what the game measures |
| `MetricSet` / `IReadOnlyMetricSet` | Dense `int[]` counters plus per-axis breakdown tables. Allocation-free increments |
| `MetricScope` | One scope's metric set and timers, with `Seal()` (immutable snapshot) and `Reset()` (reuse without reallocation). Four instances |
| `TelemetryStopwatch` | Pure C# timer that owns its injected `Func<float>` clock and folds elapsed time on `Pause()`/`Resume()`/`Elapsed` reads. Deterministic in tests via a fake clock |
| `LevelMetricsSnapshot` / `RunMetricsSnapshot` | Sealed immutable read surfaces — what the popups render and what the sink receives |
| `ILevelMetricsView` | The UI read seam. Exposes the ceremony snapshot as an `IReadOnlyReactiveProperty`, plus the last flushed level and the run. **Only `BalloonParty.UI.*` types may inject it** |
| `IFlightScope` / `FlightScopeService` | Shared per-shot boundary (`FlightIndex`, `IsInFlight`) derived from the projectile loaded/fired/destroyed messages. Consumed by metrics and by `MusicSoundRouter` |
| `GameplayMetricsService` | Entry point (`IStartable`, `IDisposable`, `IRunResettable`). Five-state level machine (Idle/Playing/Ceremony/Transitioning/Ended); routes subscriptions into scopes, takes the two per-level snapshots, hands envelopes to the sink |
| `SessionTelemetryContext` | Session id (per launch, never persisted), schema version, launch timestamp. Registered in `AppLifetimeScope` so it survives scene reloads |
| `TelemetryEnvelope` | One uniform wire record for every scope, with a `RecordKind` discriminator |
| `TelemetryEnvelopeSerializer` | Static, reflection-free JSON writer driven by `MetricCatalog` (reused `StringBuilder`, `InvariantCulture` throughout) |
| `ITelemetrySink` / `TelemetrySinkBase` | Write seam. The base owns the never-throw guard and the disabled latch, so no sink can soft-lock the game at a flush boundary |
| `ConsentGateSink` / `BatchingTelemetrySink` / `CompositeTelemetrySink` | Cross-cutting concerns as decorators — one concern each |
| `JsonLinesTelemetrySink` | Dev-only local sink: one JSON object per line, one stream per session, rotating log files in `Application.persistentDataPath/telemetry/` |
| `HttpAnalyticsSink` | Batched export to an external analytics service (last wave; gated on choosing a provider) |

## Two snapshots per level

The level-up popup shows during the ceremony, **before** the level's flush boundary —
straggler score trails are still arriving. So a *ceremony* snapshot (what the player was
shown) and a *flush* snapshot (what was true) are both taken and both logged. The gap
between them is data, not a bug. The popup shows *projected* points to match what
`ScoreController` already displays beside it.

## Where the per-flight numbers come from

Per-flight counts live in `FlightStatsService` (`Game/Flight/`, a **gameplay** service,
not part of this folder). `HitPipeline` is its only writer, recording each hit *before*
publishing `ActorHitMessage` — so the count is fixed before any subscriber runs, and
`CombatSoundRouter` reads it post-increment for its pitch ramps. Metrics folds it at
flight seal. One owner, two readers, no ordering hazard.

The one exception: `CombatSoundRouter._colorPopsThisFlight` **stays private to audio**. It
resets on streak break as well as on load, and counts only pops that use the generic
`BalloonPop` sound — a pitch cursor, not a statistic. Do not "unify" it.

## Pause handling

Subscribes `PauseService.IsAnyPaused` directly (reference-counted and reset-safe) rather
than counting `PausedMessage`/`ResumedMessage` edges, which leak on the loss path.

## Registration

`FlightScopeService` then `GameplayMetricsService` at the end of
`GameScopeRegistration.RegisterGameplaySystems`; `SessionTelemetryContext` in
`AppLifetimeScope`. Gating is three tiers, not one `#if`: the counting core ships (the
popups need it), the export layer ships inert until consent, and the local file sink plus
the cheat read are compiled out of release.

## Design plan

Full specification: @ref plan_gameplay_telemetry

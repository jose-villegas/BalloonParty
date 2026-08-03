# Telemetry

Gameplay metrics. Counts what happens during play at four nested scopes —
**Session ⊃ Run ⊃ Level ⊃ Flight** — and serves those counts to three consumers: the
level-up and game-over popups, an external analytics export, and balance analysis.

The service never publishes, never modifies gameplay state, and never causes stutters
during action — it only counts. Views read immutable snapshots; they never see a live
accumulator.

> **W1 (vocabulary, scopes, timers) and W2 (envelope, serializer, sinks) have landed.**
> W1: `MetricId`/`TimerId`/`MetricAxis`, `MetricCatalog`/`TimerCatalog`,
> `MetricSet`/`IReadOnlyMetricSet`/`ISealedMetrics`, `MetricScope`, `TelemetryStopwatch`,
> both snapshots (sharing `MetricsSnapshotBase`) and `ILevelMetricsView`. W2:
> `TelemetryEnvelope`, `TelemetryEnvelopeSerializer`, `ITelemetrySink`/`TelemetrySinkBase`,
> `CompositeTelemetrySink` (registered in `GameScopeRegistration.RegisterTelemetrySinks`,
> called from `GameLifetimeScope.Configure`) and the dev-only `JsonLinesTelemetrySink`. All
> covered by the EditMode tests in `Assets/Tests/EditMode/Game/`, including the test fake
> `RecordingTelemetrySink`. `GameplayMetricsService`, `SessionTelemetryContext` and the
> consent/batching decorators are still design — the rest of the file table below is the
> target layout for those rows, not current code.

## Contents

| File | What it does |
|---|---|
| `MetricId` / `TimerId` / `MetricAxis` / `MetricScopeKind` | The vocabulary — counters, clocks, dimension axes (color, balloon type, item type), and the four nested scope kinds. Append-only once shipped |
| `MetricCatalog` | Static table: metric id → wire name, unit, fold rule (`Sum`/`Max`/`Last`), scope (`Flight`/`Level`/`Run`) and dimension axes. The single browsable list of what the game measures; also owns the dense `AxisSlot` assignment for `(MetricId, MetricAxis)` pairs, and asserts (at static init) that `BalloonType`, `ItemType`, `MetricAxis` are contiguous from zero and that `MetricScopeKind` stays ordered `Flight < Level < Run < Session` |
| `TimerCatalog` | Static table: timer id → wire name, unit. Kept separate from `MetricCatalog` because a timer has no `FoldRule`, `Scope` or `MetricAxis` — `Absorb` never folds timers (see *Every scope runs its own clocks* below) |
| `AxisSlot` / `AxisSlotInfo` | A dense index into `MetricSet`'s per-slot axis storage, and the catalog row describing it. One slot per `(MetricId, MetricAxis)` pair — not one array per axis kind — so metrics that share an axis kind (e.g. `Pops` and `Deflects`, both `BalloonType`) never alias the same table |
| `MetricSet` / `IReadOnlyMetricSet` | Dense `int[]` counters plus one `int[]` per declared `AxisSlot`. Allocation-free increments. `IReadOnlyMetricSet` is a read-only *view*, not an immutability guarantee — `MetricScope.Metrics` is the one legitimate place a live view belongs |
| `ISealedMetrics` | The envelope's actual contract: `IReadOnlyMetricSet` plus `this[TimerId]`, implemented only by the two snapshots — never by `MetricSet` — so a reference typed as `ISealedMetrics` really is immutable |
| `MetricScope` / `MetricScopeState` | One scope's metric set and timers, with `Seal()` (immutable snapshot) and `Reset()` (reuse without reallocation). Four instances, built only through `MetricScope.Create`. `Absorb` requires its argument to be exactly one scope below (rejects itself, a sibling, or a non-adjacent scope); `Seal(int, bool)`/`Seal()` each validate the scope they run on |
| `TelemetryStopwatch` | Pure C# timer that owns its injected `Func<float>` clock and folds elapsed time on `Pause()`/`Resume()`/`Elapsed` reads. Deterministic in tests via a fake clock |
| `MetricsSnapshotBase` / `LevelMetricsSnapshot` / `RunMetricsSnapshot` | Sealed immutable read surfaces implementing `ISealedMetrics` — what the popups render and what the sink receives. The shared payload (counters, timers, axis slots, named breakdowns) lives once in the base; `LevelMetricsSnapshot` adds only `LevelIndex`/`Completed` |
| `ColorCount` / `BalloonTypeCount` / `ItemActivationCount` | The breakdown shapes the snapshots expose (`PopsByColor`, `PointsByColor`, `PopsByBalloonType`, `DeflectsByBalloonType`, `ItemsActivated`) |
| `ILevelMetricsView` | The UI read seam. Exposes the ceremony snapshot as an `IReadOnlyReactiveProperty`, plus the last flushed level and the run. **Only `BalloonParty.UI.*` types may inject it** |
| `GameplayMetricsService` | Entry point (`IStartable`, `IDisposable`, `IRunResettable`). Five-state level machine (Idle/Playing/Ceremony/Transitioning/Ended); routes subscriptions into scopes, takes the two per-level snapshots, hands envelopes to the sink |
| `SessionTelemetryContext` | Session id (per launch, never persisted), schema version, launch timestamp. Registered in `AppLifetimeScope` so it survives scene reloads |
| `TelemetryEnvelope` | One uniform wire record for every scope (`readonly struct`), with a `RecordKind` discriminator and an `ISealedMetrics` payload |
| `TelemetryEnvelopeSerializer` | Reflection-free JSON writer over one reused `StringBuilder`, driven by loops over `MetricCatalog`/`TimerCatalog` (zero-valued counters skipped, timers always emitted, `InvariantCulture` on every numeric/date append) |
| `ITelemetrySink` / `TelemetrySinkBase` | Write seam. The base owns the never-throw guard: `Write`/`FlushAsync` share one latch that permanently no-ops both once either hook throws; `Dispose` is guarded and idempotent independently, so a prior write failure never leaks the sink's resource |
| `CompositeTelemetrySink` | Fans out to an array of leaf sinks; an empty array is the inert "no export configured" state — no `NullTelemetrySink`. Registered unconditionally in `GameScopeRegistration.RegisterTelemetrySinks`, wrapping `{ JsonLinesTelemetrySink }` under the dev guard or an empty array otherwise |
| `ConsentGateSink` / `BatchingTelemetrySink` | Cross-cutting decorators — one concern each (W5) |
| `JsonLinesTelemetrySink` | Dev-only local sink (`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`): one JSON object per line, one `StreamWriter` opened in `Start()` and kept for the session, rotating log files in `Application.persistentDataPath/telemetry/` (20 most recent, sorted by file name) |
| `HttpAnalyticsSink` | Batched export to an external analytics service (last wave; gated on choosing a provider) |

## Every scope runs its own clocks

A `TimerId` names a clock; each of the four scopes holds its own instance of each one,
measuring its own window. The service drives them together — pausing the gameplay clock
is one loop over the scopes, not one call per scope per timer — so `MetricScope.Absorb`
never folds timers, only counters and axes. Folding would double-count: a Run scope's
`Gameplay` stopwatch has already measured the whole run, so adding each Level's elapsed
on top would double it. An earlier revision specified the fold (`TelemetryStopwatch.Add`
plus a `MetricScope.AbsorbTimers` step inside `Absorb`); it was wrong and has been
removed, not just left unused.

## Two snapshots per level

The level-up popup shows during the ceremony, **before** the level's flush boundary —
straggler score trails are still arriving. So a *ceremony* snapshot (what the player was
shown) and a *flush* snapshot (what was true) are both taken and both logged. The gap
between them is data, not a bug. The popup shows *projected* points to match what
`ScoreController` already displays beside it.

## Where the per-flight numbers come from

Per-flight counts live in `FlightStatsService` (`Game/Flight/`, a **gameplay** service,
not part of this folder). Each count is written by whoever publishes the message it belongs
to, immediately before that publish — `HitPipeline` for pops and deflects, `ProjectileView`
for wall bounces, `ProjectileHitResolver` for pierce discharges. The count is therefore
fixed before any subscriber runs: `CombatSoundRouter` reads it post-increment for its pitch
ramps, metrics folds it at flight seal, and neither depends on subscription order.

The one exception: `CombatSoundRouter._colorPopsThisFlight` **stays private to audio**. It
resets on streak break as well as on load, and counts only pops that use the generic
`BalloonPop` sound — a pitch cursor, not a statistic. Do not "unify" it.

## Pause handling

Subscribes `PauseService.IsAnyPaused` directly (reference-counted and reset-safe) rather
than counting `PausedMessage`/`ResumedMessage` edges, which leak on the loss path.

## Registration

`ITelemetrySink` → `CompositeTelemetrySink` registers unconditionally in
`GameScopeRegistration.RegisterTelemetrySinks`, called from `GameLifetimeScope.Configure`
before `RegisterGameplaySystems` — a later consumer (`GameplayMetricsService`, W3) can
depend on it without caring whether any leaf sink exists. `GameplayMetricsService` will
register at the end of `RegisterGameplaySystems`; `SessionTelemetryContext` in
`AppLifetimeScope` so it survives scene reloads. `FlightStatsService` is registered
earlier in `RegisterGameplaySystems` and lives in `Game/Flight/` — it is a gameplay
service, not part of this subsystem.

Gating is three tiers, not one `#if`: the counting core ships (the popups need it), the
export layer ships inert until consent, and the local file sink plus the cheat read are
compiled out of release.

## Design plan

Full specification: @ref plan_gameplay_telemetry

@page plan_gameplay_telemetry Gameplay Telemetry

# Gameplay Metrics & Telemetry

One subsystem that counts what happens during play, at four nested scopes
(**Session ⊃ Run ⊃ Level ⊃ Flight**), and serves those counts to the level-up and
game-over popups, to a local JSON Lines log, and — if it is ever wanted — to an external
analytics service.

The read model came first on purpose. A logger nobody reads is a logger nobody maintains;
a metrics system the game itself renders stays correct because a wrong number is visible
on screen.

> **This plan has been pruned.** The subsystem is built and shipping; the architecture it
> used to specify now lives in the code and in `Game/Telemetry/README.md`,
> `Game/Flight/README.md` and `UI/Telemetry/README.md`, which are the places to look
> first. What remains here is what those cannot carry: the numbered requirements and risks
> that code comments cite by name, the extraction path for goal **G5**, and short notes on
> the two pieces that were never built. Earlier revisions carried a full wave-by-wave task
> plan, a test strategy and an architecture study — all delivered, all removed.

---

## Status

**Built and in use.** Vocabulary and scopes, the envelope/serializer/sink layer, the
`GameplayMetricsService` state machine, per-flight stats extraction, and catalog-driven
metric labels for the popups.

**Not built, and out of current scope:** the consent/batching decorators and an HTTP
analytics sink. Both are sketched at the end. Neither is needed for the popups or for
local balance work — the JSON log below already answers those.

### Where the JSON goes

`JsonLinesTelemetrySink` is compiled in under `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and
writes one JSON object per line to:

```
<Application.persistentDataPath>/telemetry/telemetry_<yyyyMMdd_HHmmss>.jsonl
```

One file per session, opened at startup, flushed after every record, 20 files retained
(oldest deleted first; the timestamped name means an ordinal sort is a chronological one).

Records are written at three boundaries: every **flight** (`ProjectileDestroyedMessage`),
every **level flush** (`LevelTransitionCompletedMessage`), and the **run end**
(`GameOverMessage`). Flight records dominate the volume by an order of magnitude. Entering
play mode and leaving without ever firing produces an empty file — that is the sink
working, not failing.

---

## Goals

| # | Goal |
|---|---|
| **G1** | The counting core ships in release builds — the popups depend on it. |
| **G2** | One vocabulary for gameplay counters and "mechanic triggered" events. |
| **G3** | Roll-up between scopes is mechanical, driven by a declared fold rule per metric. |
| **G4** | Nothing is persisted client-side. A number kept for the player has stopped being telemetry. |
| **G5** | The counting engine is extractable as a game-agnostic library; BalloonParty feeds it a description of what to measure. See *Separability*. |

---

## Requirements

Numbered so task specs can cite them. Each is testable.

### Scopes and boundaries

- **R1** — Metrics are collected at four nested scopes: **Session ⊃ Run ⊃ Level ⊃ Flight**.
- **R2** — A **Flight** opens on `ProjectileLoadedMessage` (`Thrower/ThrowerController.cs:232`)
  and **seals** on `ProjectileDestroyedMessage` (`Projectile/View/ProjectileView.cs:406`).
  The `[Destroyed, next Loaded)` window is *interflight*: increments there belong to the
  Level and to no flight. This deliberately differs from `CombatSoundRouter`'s boundary
  (which clears only on `Loaded`, so its "flight" straddles the ceremony); the metrics
  definition is chosen so that **Σ flights ⊆ level** holds.
- **R3** — A **Level** flushes on `LevelTransitionCompletedMessage` (not on
  `LevelUpDismissedMessage` — straggler trail points land in between and would be charged
  to the next level). A **Run** flushes on `GameOverMessage`.
- **R4** — Roll-up Flight → Level → Run → Session is **mechanical**, driven by a declared
  fold rule per metric, not by hand-written accumulate code.

### Vocabulary

- **R5** — Counters are addressed by a `MetricId` enum over dense `int[]` storage, with
  three fixed dimension axes: **Color**, **BalloonType**, **ItemType**. A fourth axis is
  out of scope; if one is ever needed, that is the moment to revisit the whole shape.
- **R6** — A static `MetricCatalog` is the single browsable table mapping each `MetricId`
  to its wire name, unit, and fold rule (`Sum` / `Max` / `Last`). Adding a metric =
  one enum value + one catalog row + one `Increment` call site.
- **R7** — "Mechanic triggered" events use the same vocabulary as gameplay counters:
  pierce discharges, speed taps minted, sweeps, shields gained/lost, strikethroughs,
  overflow hearts lost, items activated. No parallel mechanism.

### State machine

- **R8** — The Level state machine has five states (`Idle`/`Playing`/`Ceremony`/
  `Transitioning`/`Ended`). Ceremony exits:
  - `LevelUpDismissedMessage` → `Transitioning`.
  - `LevelUpAbortedMessage` → `Playing`, discarding the ceremony clock, the captured level
    index **and the ceremony snapshot**.
  - `LevelUpAbandonedMessage` → `Playing`, same discards, **idempotent** — never `Ended`.
  - `GameOverMessage` → `Ended`. **This message alone owns the terminal transition.**
- **R8a** — `LevelUpAbandonedMessage` is a **nested re-publish**, not an independent
  event, and the plan's earlier "per the run's fate" reading was unimplementable *and*
  actively harmful. `LevelController.AbandonCeremony` (`Game/Level/LevelController.cs:506`)
  is reached from four sites and publishes synchronously **inside** whatever triggered it:
  - From the `LevelUpAbortedMessage` handler (`:419`) — metrics therefore receives
    **Abandoned before Aborted**. Had Abandoned entered `Ended`, R9's early-return would
    swallow the Aborted and the gameplay clock would never resume — every later level
    recording ≈ 0 duration, which is guardrail #5's exact failure mode.
  - From the `GameOverMessage` handler (`:142`) — nested inside
    `RunController.EndRun`'s publish. Had Abandoned entered `Ended`, the outer
    `GameOverMessage` would then hit the gate and early-return, so **no run record would
    ever be written for a lost run**.

  The message carries no reason (`Shared/Messages/LevelUpAbandonedMessage.cs` is an empty
  struct) and none is needed: `AbandonCeremony` unconditionally sets
  `_phase = LevelUpPhase.Playing`, so Abandoned always means "the ceremony is over",
  never "the run is over". Do **not** add a payload.
- **R9** — While in `Ended`, every handler early-returns. The loss cinematic completes
  straggler score trails *after* `GameOverMessage`; without the gate those arrivals
  corrupt the next run. The gate is entered by `GameOverMessage` only (**R8**).

### Fields and derivation

- **R10** — `WaveDamageMessage` contributes three metrics, not one: `HeartsLost`
  (rename `OverflowCount` → `HeartsLost`), `BlockedSlots`, and a derived
  hearts-lost-per-wave max. `RowLength` is context, not a metric.
- **R11** — Hold-to-speed-up is measured explicitly: `hold_seconds` per level and
  `hold_speed_up_flights`. Duration metrics stay on `Time.unscaledTime` — the hold metric is what makes a fast-forwarded level distinguishable from a
  slow one. `HoldSpeedUpController` publishes nothing and samples `Input.GetMouseButton(0)`
  inside its own `Tick()`, so this requires a **sanctioned read interface**:
  `internal interface IHoldSpeedUpState { bool IsHolding { get; } }` implemented by
  `HoldSpeedUpController`, registered `AsImplementedInterfaces()`. No new message type; no
  polling on the metrics side (**R30**) — the service samples it on the frame boundaries it
  already observes.
- **R12** — Pops break down by **both** color and balloon type. Unknown color ids
  (rainbow, paint-converted) and actors without `IHasColor` fall into a trailing
  *other* bucket and must never throw or index out of range.
- **R13** — Accuracy uses `DirectHitPops` (`ActorHitMessage` with
  `(Context.Flags & DamageFlags.DirectHit) != 0`), never `ShotsFired / TotalPops` —
  total pops includes item-, cheat- and board-driven pops.
- **R14** — Run identity is **three ids, not one**:
  `RunId` (the run chain — stable across retries), `AttemptId` (the `RunController`
  generation), and `AttemptIndex` (0 for the original, 1..N for retries within the
  chain). The service derives all three itself from `IRetryState.RetryLevel` at reset
  time — no new gameplay state. Read it **before** `RetryTracker` zeroes it (see **R21**).
- **R14a** — Every level record carries `LevelAttemptOrdinal`: how many times *this level
  index* has been played in this chain. `AttemptIndex` alone is wrong per level — after a
  retry at level 7, levels 8+ carry `AttemptIndex = 1` but are first attempts. Only the
  ordinal answers "how many tries did this level take".
- **R14b** — `RunRecord.TotalScore` comes from `GameOverMessage.FinalScore` and is
  **never** summed from level records. Score carries across a retry
  (`ScoreController.ClearRunState` restores `_levelStartScore` when `RetryLevel > 0`), so
  a chain with a replayed level has two records for that level and summing them
  double-counts.
- **R14c** — Attempt history is **export-only**. The UI read model exposes the current
  attempt; the popups show the try that succeeded, and prior attempts are irrelevant to
  them. Per-level history lives in the envelope stream, not in `ILevelMetricsView`.
- **R15** — Level outcome distinguishes *cleared* (`BoardDepletedMessage`) from
  *progressed on score* from *partial at game over*.

### Read model (in-game consumption)

- **R16** — Views consume metrics through a narrow read-only interface,
  `ILevelMetricsView`, exposing an `IReadOnlyReactiveProperty<LevelMetricsSnapshot>`.
  Payloads are immutable. Views read `.Value` at render time; the subsystem publishes
  no message and the View never triggers a snapshot.
- **R17** — **Two snapshots per level, one record.** The *ceremony* snapshot is taken on
  `ScoreLevelUpMessage` and is what the level-up popup renders; it is exposed on
  `ILevelMetricsView.CeremonyLevel` and **never written to a sink**. The *flush* snapshot is
  taken on `LevelTransitionCompletedMessage` and is the only one exported or folded into the
  run.

  The divergence is still data — a large gap means the ceremony fires too early — but it is
  recoverable from the single flush record, which carries `points_projected` (read at
  ceremony) alongside `points_banked` (final). A second record per level would double the
  export for one number that is already there.
- **R18** — The popup shows **projected** points, not banked. `ScoreController` already
  snaps `TotalScore` to `_projectedTotal` on level-up so the popup never shows a stale
  low number while trails sit frozen; the stats block must adopt the same convention or
  it will contradict the score number beside it on the same popup.
- **R19** — `GameOverScreen` reads the run snapshot **after** its presentation-gate
  `await`, never inside its `GameOverMessage` handler — otherwise it depends on
  MessagePipe subscriber order.
- **R20** — Only `BalloonParty.UI.*` types may inject `ILevelMetricsView`, and every
  consumer must render correctly against an empty/default snapshot. Metrics must never
  become load-bearing for gameplay.

### Lifecycle and registration

- **R21** — The service is `IRunResettable` with `ResetOrder => RunResetOrder.Quiesce`
  (0), which must stay **below** `RetryTracker`'s 110 (`RunResetOrder.Score + 10`) so it
  reads `IRetryState.RetryLevel` before that tracker zeroes it. Pin the inequality with a
  unit test. **Do not also subscribe `RunResetMessage`** — `RunController` invokes
  `ResetRun` directly, so both would double-reset.
- **R22** — Session state (`SessionTelemetryContext`: session id, schema version, launch
  timestamp) registers in **`AppLifetimeScope`**, not `GameLifetimeScope`, so it survives
  scene reloads.
- **R23** — Clocks do not start in `Start()` — entry points start during the Launcher's
  additive preload, long before the player taps Play. Enter `Playing` on
  `INavigation.Current == NavigationState.Game`.

### Export

- **R24** — One uniform record: `ITelemetrySink.Write(in TelemetryEnvelope)` with a
  `RecordKind` discriminator, `SchemaVersion`, `SessionId`, `RunId`, `LevelIndex`,
  timestamp, and the metric set. **Not** an overload per record type — that makes every
  sink change when a record kind is added, which would have made the promised network
  sink a redesign rather than a drop-in.
- **R25** — The never-throw guard (`try/catch` → `Log.Warn` → permanent `_disabled`
  latch for the session) lives in an abstract `TelemetrySinkBase` template method, not in
  any single sink. A future HTTP sink that throws at a flush boundary soft-locks the game
  exactly like a file sink would.
- **R26** — Cross-cutting concerns are decorators over `ITelemetrySink`: consent gate →
  batcher → composite fan-out. Consent is **never** an `if` inside the service.
- **R27** — Serialization is hand-rolled and reflection-free, a
  single loop over `MetricCatalog`, one reused `StringBuilder`, every numeric and date
  append through `CultureInfo.InvariantCulture`.
- **R28** — Build gating is **three tiers**, not one `#if`:
  - **R28a** — the dev-only tier: `JsonLinesTelemetrySink` and the viewer window are
    wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
  - **R28b** — the export tier: the decorator chain registers unconditionally and is
    inert until consent. When no leaf sink is registered, `CompositeTelemetrySink` holds an
    empty array and `Write` is an empty loop — do **not** add a `NullTelemetrySink`.
  - **R28c** — the cheat-read tier: the triple guard
    `#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE` **with an `#else`
    defaulting `cheatActive = false`**, verified by a rebuild with `UNITY_EDITOR` stripped.

### Performance

- **R29** — Hot-path handlers allocate nothing: no string interpolation, no LINQ, no
  `Log.Info` in the `ActorHitMessage` / `StreakChangedMessage` / `ScoreTrailArrivedMessage`
  paths. Increments are `_counters[(int)id]++` and `_axis[i]++`. Measured worst case is
  ~10–25 `ActorHitMessage` in one frame (bomb/laser/lightning resolve synchronously) plus
  tens of `ScoreTrailArrivedMessage` in the transition frame; peak is low hundreds of
  invocations per second.
- **R30** — **No pooling, no buffering, no `ITickable`** anywhere in the counting core.
  Snapshot allocation at ceremony and flush (~30–120 s apart) is negligible.
- **R30a** — `BatchingTelemetrySink` reuses one `List<TelemetryEnvelope>` rather than
  allocating per batch.

---

---

## Separability — this becomes an external library

**Target: the counting engine ships as a game-agnostic package.** BalloonParty feeds it a
description of what to measure and how to name the output; the package knows nothing about
balloons, projectiles, levels or runs. Nothing below is scheduled work — it is the seam the
remaining waves must not thicken.

Where the coupling stands today, measured rather than assumed. `GameplayMetricsService` is
the game's **translator** — every gameplay message, `IRetryState`, `IDangerLevel`,
`IRunScore`, `PauseService`, `INavigation`, `IGamePalette`, `IFlightStats` and
`IHoldSpeedUpState` terminate there, and it is the file that stays behind at extraction.
That part of the line holds: the dependency arrow runs from telemetry to the game and never
back.

Beneath it the line is **thinner than an earlier revision of this section claimed**. That
revision said the whole storage and fold engine was pure BCL and that no file referenced a
balloon, a projectile, a level or a run. It was wrong, and the correction is the point of
this section — an extraction cost that is under-stated is an extraction that stalls:

- `MetricScope` imports `BalloonType` and `ItemType`, casts bucket ordinals to them when
  building its named breakdowns, and hardcodes five specific metrics by name in
  `CopyState` — the generic fold engine knows which metrics matter.
- `MetricScope.Seal(int levelIndex, bool completed)` puts *level* in the engine's signature.
- `MetricScopeState` and `MetricsSnapshotBase` name five game concepts as properties
  (`PopsByColor`, `PopsByBalloonType`, `DeflectsByBalloonType`, `PointsByColor`,
  `ItemsActivated`), with `ColorCount`/`BalloonTypeCount`/`ItemActivationCount` behind them.
- `MetricScopeKind.Flight` is a game concept in the enum `Absorb`'s adjacency gate compares
  ordinals on.
- `TelemetryEnvelope` carries `LevelIndex`, `LevelAttemptOrdinal`, `Completed`, `RunId`,
  `AttemptId`, `AttemptIndex` and `EndCause` — run and level vocabulary on the wire record.
- `MetricCatalog` sizes its axis bucket counts from two game enums.

None of this arrived with the service; the engine was built this way in W1. The remaining
waves must not thicken it further, and the "one real design change" below is now the *first*
of several — see *What extraction actually costs*.

### What the library owns

Storage, scopes, folding, the catalog *mechanism*, snapshots, the envelope, the serializer,
and the sink seam with its never-throw guard.

### What the game supplies

| Injected | Currently | For a package |
|---|---|---|
| The catalog rows | `MetricCatalog` / `TimerCatalog`, hardcoded | Consumer-owned; the library takes them at construction |
| Axis definitions | `MetricAxis` with three hardcoded members, sized from `BalloonType`/`ItemType` | An axis descriptor: wire name, bucket count, `bucketIndex → name` |
| Colour names | `IGamePalette.ProgressColorNames` | Narrows to `IReadOnlyList<string>` at the constructor |
| Logging | `Log.Warn` | An `ILogSink`, no-op by default |
| The file sink | `JsonLinesTelemetrySink` (the only Unity-touching file) | Stays with the game; the library ships the interface |

### What extraction actually costs

Four changes, in dependency order. Only the first is small.

1. **Axes become a descriptor.** `MetricAxis` today is a fixed three-member enum whose
   bucket counts come from two game enums. Generalise it to a descriptor supplied with the
   catalog — wire name, bucket count, `bucketIndex → name` — and slots, storage, `Absorb`
   and the serializer need no change at all: they already go through `AllSlots`. W1b's slot
   indirection set this up accidentally; the storage engine has never known what an axis
   *means*. Roughly fifteen lines.
2. **The named breakdowns leave the snapshot.** `PopsByColor` and its four siblings, the
   three `*Count` record types, and the hardcoded metric names in `MetricScope.CopyState`
   all exist to give the popups typed, pre-joined lists. Once axes are descriptors the
   library can expose `AxisBucketsOf(metric, axis)` plus bucket names and let the *game*
   build the typed views it wants. This is the largest of the four and the one that touches
   UI code, so it wants to happen before W4 hardens a binding against those properties — or
   knowingly after, accepting the rework.
3. **`Flight` leaves `MetricScopeKind`.** The engine needs *ordered nesting depth*, not four
   named tiers; the consumer names them.
4. **The envelope's identity fields become a consumer-supplied bag.** `LevelIndex`,
   `Completed`, `RunId`, `AttemptId`, `AttemptIndex`, `LevelAttemptOrdinal` and `EndCause`
   are this game's run model. The library's own envelope needs a scope kind, a payload, a
   timestamp and a schema version; everything else is dimensions the consumer attaches.
   **Note the one UI dependency on this:** `MetricValueResolver.ResolveField` downcasts
   `ISealedMetrics` to `LevelMetricsSnapshot` to read `LevelIndex`/`Completed`, so
   `RecordField` moves with change #4. One `is` pattern in one method — a cost to plan
   for, not a redirection.

W4 pre-built part of change #1: `AxisBucketNaming` is the `bucketIndex → name` half of the
axis descriptor, already isolated behind an `IReadOnlyList<string>` rather than
`IGamePalette`. W4 also verified change #2's premise holds — no file outside
`Game/Telemetry/` references the five named breakdown properties, so deleting them stays a
pure engine edit that touches no UI and no prefab.

**Open question, decide before extraction:** how a metric is identified across the boundary.
`MetricId` is inherently the consumer's — thirty rows naming *this* game's events. Either the
library is generic over the consumer's enum (`MetricSet<TMetric> where TMetric : struct, Enum`,
keeping type safety, some IL2CPP/AOT care needed) or it addresses metrics as dense `int`s with
the catalog supplying names (simpler, loses compile-time safety at the seam). Not urgent —
nothing before W6 forces it.

### Why the JSON stays hand-rolled

Originally justified by a constraint: `BalloonParty.Runtime.asmdef` sets
`"overrideReferences": true` with only `DOTween.dll`, so Newtonsoft is unreachable, and
`JsonUtility` returns `{}` for these types because it serializes only Unity-serializable
public *fields*.

That framing understates it — NuGetForUnity is in the project, so a dependency *could* be
added. Restating it as a decision rather than a wall:

- **Escaping and formatting are already correct** — quotes, backslash, `\n`/`\r`/`\t` and all
  control characters below `0x20` as `\uXXXX`, every numeric and date append through
  `InvariantCulture`, all covered by tests. The usual reason to reach for a library is
  already paid for.
- **Zero dependencies is a feature of a library you intend to lift out**, not a limitation.
- **Reflection-based serializers are the classic IL2CPP/AOT landmine**, and System.Text.Json's
  source generators do not run in Unity's build pipeline.
- The allocation argument is weak in both directions: flushes are 30–120 s apart, so this is
  not a hot path either way. `Utf8JsonWriter` would avoid the string-then-encode double pass,
  which matters slightly more for W6's batched HTTP sink than for a file — revisit there if
  batch sizes get large, not before.

Nothing reads these files back yet. The day something does — a viewer, a round-trip test — a
*parser* dependency appears, and that is a better place to take one than the writer.

---

## Risk register

| ID | Risk | Trigger | Mitigation |
|---|---|---|---|
| RK-1 | Audio pitch ramp shifts a whole tone, silently | Anything but `HitPipeline` writing `FlightStatsService`, or audio forgetting the `- 1` on the post-increment read | Single-writer rule; `CombatSoundRouterTests` must pass unmodified |
| RK-2 | The colour ramp breaks after a streak break, or a coloured special type (silver/gold/rainbow) offsets the next Simple of the same colour | Moving `_colorPopsThisFlight` into the shared stats — it is streak-scoped and counts only generic-`BalloonPop` pops | It stays private to `CombatSoundRouter`. Worst failure mode here — silent and ear-only |
| RK-2a | Two answers to "how many Toughs popped" drift apart | Leaving the type counters private and recounting them in metrics — adding a `BalloonType` then requires lockstep edits in `PopSoundFor`, `PopSemitoneOffset`, `IncrementPopCounter` *and* metrics | Single owner: `FlightStatsService` |
| RK-3 | Levels-reached distribution corrupted by retries | Treating the run generation as run identity | **R14** — chain / attempt / attempt-index split |
| RK-3a | Level difficulty analysis blames levels that were never retried | Segmenting by `AttemptIndex` instead of `LevelAttemptOrdinal` | **R14a** |
| RK-3b | Run totals double-count a replayed level | Summing `PointsBanked` across level records in a chain | **R14b** — take `TotalScore` from `GameOverMessage` |
| RK-4 | Metrics reset reads a zeroed `RetryLevel` | Anyone changes `ResetOrder` on either side of 0 vs 110 | **R21** + unit test on the inequality |
| RK-5 | Soft-lock at a flush boundary | Sink throws; MessagePipe has no exception isolation | **R25** + service-side guard |
| RK-6 | Popup stats contradict the score beside them | Showing banked instead of projected points | **R18** |
| RK-7 | Heisenbug on the game-over screen | Reading in the handler instead of after the gate | **R19** |
| RK-8 | Post-game-over stragglers corrupt the next run | Loss cinematic completes trails after `GameOverMessage` | The `Ended` gate, **R9** |
| RK-9 | Stale ceremony snapshot after an aborted ceremony | Abort not clearing `CeremonyLevel` | **R8** + test |
| RK-10 | Release build breaks while `dotnet build` stays green | Missing `#else` on the triple cheat guard | Rebuild with `UNITY_EDITOR` stripped in W3 |
| RK-11 | Wire-name drift breaks the external warehouse | Renaming a catalog entry | Append-only; consider a test pinning the id→name map |
| RK-12 | Metrics becomes load-bearing gameplay | A gameplay system injects `ILevelMetricsView` | **R20**, enforced by README contract |
| RK-13 | Consent checks scattered through the service | Implementing consent as an `if` | `ConsentGateSink`, **R26** |
| RK-14 | Session id becomes a device fingerprint | Persisting the GUID to `PlayerPrefs` | Explicit non-goal — regenerate per launch |
| RK-15 | `session.total_score` reports the *last* run's score | `Absorb` stops a metric folding **below** its declared scope but not **above** it, so `TotalScore`, `RetriesUsed` and `PointsProjected` fold as `Last` into the Session scope | Harmless while `RecordKind.Session` is never written. **Decide before W6**, not during: either a `MaxScope` column, or skip `Last` metrics in `Absorb` once the child scope is the one that owns them. Note also that `ResetRun` never folds the run into the session, so a run abandoned to the menu contributes nothing |

---

---

## Not built

Neither is required by anything shipping. Both are recorded so the seams they were
designed against are not quietly closed.

### Export decorators

`ConsentGateSink`, `BatchingTelemetrySink`, and `ITelemetrySettings` behind a config asset
(injected as the read-only interface, never the concrete SO). Members and defaults, so
they are not re-invented: `string Endpoint` (empty), `int BatchSize` (20),
`float FlushIntervalSeconds` (30), `bool Enabled` (false), at
`Assets/Configuration/TelemetrySettings.asset`.

`Enabled` is a build/ops kill-switch and consent is a player decision — different
concerns, but both belong at the same seam so there is exactly one place a record can be
dropped (**R26**). `CompositeTelemetrySink` already registers unconditionally and holds an
empty array when nothing is configured, which is the inert state this builds on
(**R28b**).

**The non-goal that matters: never gate the accumulator or the read model, only the
sink.** Consent sits downstream of `GameplayMetricsService`, which is also what serves
`ILevelMetricsView`. Since the popups bind to it, that placement is the difference between
"export is off" and "the level-up popup renders `--`". Nothing in code enforces this.

#### Cadence: one queue, many triggers

Records are already *produced* at their own cadence — a flight record at every projectile
destroy, a level record at the transition, a run record at game over. That part is done and
is the natural shape.

**Do not carry that through into per-kind upload cadences.** One queue, one connection.
The reason is the cellular radio: each upload wakes it and holds it in a high-power state
for seconds afterwards, so three timers cost roughly three times the battery of one for the
same bytes. Coalescing everything into a single batch is why the mainstream mobile SDKs do
it that way, and it is worth more than the freshness that separate streams buy. Separate
streams also arrive interleaved, which makes reconstructing a session downstream harder
than reading one ordered batch.

What *should* vary per kind is the **flush trigger**, not the queue:

| Kind | Behaviour |
|---|---|
| Flight | Accumulate silently. Never triggers a flush — it is the high-volume kind, and no single shot is worth a radio wake |
| Level | Accumulate. Size- and timer-triggered flush only |
| Run | Triggers a flush. It closes a coherent unit, and game over is a genuinely idle moment — no action on screen to stutter |
| *App pause / focus loss* | **Always flush.** This is the one that actually matters: mobile OSes kill backgrounded apps without warning, and an unflushed queue is simply lost data |

So `BatchingTelemetrySink` takes a flush-priority per `RecordKind` rather than a cadence
per kind, plus a pause hook. `PauseService` is not the right source for that hook — every
`PauseSource` today is gameplay-owned, not an application-lifecycle event.

#### Offline is a queue problem, not a timestamp problem

Batching is safe to delay arbitrarily because **every envelope already carries its own
`timestamp_utc`, stamped when the record was produced**, not when it was sent. A batch
uploaded an hour late still attributes each event to the moment it happened, so event time
and ingest time stay separable downstream. Ordering within a session is doubly safe:
`flight_index` and `level_index` are explicit, so a session reconstructs even if two records
share a timestamp.

Two things that follow, and are easy to get wrong:

- **`DateTime.UtcNow` is device wall-clock.** A player with a wrong clock, or one who
  changes it mid-session, ships a batch of confidently wrong timestamps and nothing
  downstream can tell. If that matters, the fix is not a better clock — it is to also carry
  a monotonic value (ticks since session start) plus a client *send* time, so a server can
  compute the skew and correct the batch. Worth deciding when a provider is chosen, since
  most of them already have a convention for it.
- **The real offline requirement is that the queue survives an app kill.** Records live in
  memory today; a backgrounded app that the OS reclaims loses whatever was buffered. That is
  what "persistent envelope queue" in the HTTP sink note means, and it — not timestamping —
  is the work that makes offline actually work.

If payload size ever becomes the constraint, the lever is **sampling flight records** (one
in N, or only flights that did something notable), not uploading them more often. Flight
records outnumber every other kind by an order of magnitude, so they are both the problem
and the only worthwhile place to economise.

Blocked on a consent policy: opt-in, opt-out, or region-dependent; where the toggle lives;
what happens to records buffered before a decision.

### HTTP analytics sink

`HttpAnalyticsSink`, a persistent envelope queue, retry/backoff, and schema governance.
The project's first outbound network I/O, so offline semantics, backpressure and
cancellation are the real work. Blocked on choosing a provider.

Resolve **RK-15** before this ships, not during.

---

## Deferred, deliberately

- A fourth dimension axis.
- Run-counter persistence across app restarts — the session id stays non-persistent
  (**RK-14**).
- Any client-side profile data (**G4**).

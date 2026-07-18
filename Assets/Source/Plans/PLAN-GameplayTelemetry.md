@page plan_gameplay_telemetry Gameplay Telemetry

# Gameplay Telemetry

> **DRAFT (2026-07-18) — parked for a future session.** First pass at gameplay-only
> analytics: what a run/level actually looked like (durations, items, multipliers,
> shields), collected passively off the existing message bus. Explicitly OUT of scope:
> user/device/identity tracking, monetization events, any network backend decision —
> this draft is the event taxonomy and the collection seam only.

---

## Goal

Answer balance/pacing questions from real play data: how long levels take, where runs
die, which items get used, how streaks/multipliers actually behave — the numbers behind
the pacing tables in `LevelPacingConfiguration` and the loss-condition tuning.

## Principles

- **Gameplay reporting only** (José, 2026-07-18). No PII, no device fingerprinting, no
  session identity beyond an anonymous run counter — revisit only if a backend ever needs
  more.
- **Subscriber-only collection**: a plain C# `GameplayTelemetryService` (VContainer,
  `IStartable`/`IDisposable`) that ONLY consumes existing MessagePipe messages. Zero new
  publishes from gameplay code; if a stat needs a signal that doesn't exist yet, prefer
  adding a subscriber-visible message that gameplay would plausibly want anyway.
- **Aggregate in memory, emit at boundaries**: per-level and per-run accumulators, flushed
  as one record on `ScoreLevelUpMessage` / `GameOverMessage` / `RunResetMessage`. No
  per-pop emission — the frenetic-game rule applies to telemetry too (no per-event I/O or
  allocation during bursts; accumulators are plain fields/arrays).
- **Sink behind an interface**: `ITelemetrySink.Write(in LevelRecord)` etc. First sink =
  structured local log / JSON lines file for editor+device debugging; a real backend is a
  later sink implementation, not a redesign.

## Event taxonomy (v0 sketch — refine when picked up)

**LevelRecord** (flushed on level-up):
- level index, duration (unscaled + scaled), shots fired, pops (per color + total),
- max streak / max multiplier reached, points banked, overshoot carried (once
  FutureIdeas §15 lands), balloons spawned/overflowed,
- items: collected / activated per item type, shields gained / spent / peak.

**RunRecord** (flushed on game-over or reset):
- levels completed, total duration, total score, loss cause (health-drain vs overflow —
  whatever `GameOverMessage`/loss forecast expose), restarts,
- aggregates of the level records (max multiplier overall, item totals).

**Signal sources already on the bus** (verify exact names when implementing):
`ActorHitMessage` (pops, colors), `StreakChangedMessage` (streak/multiplier),
`ScoreTrailArrivedMessage` (banked points), `ScoreLevelUpMessage` /
`LevelUpDismissedMessage` (level boundaries + ceremony timing),
`ProjectileDestroyedMessage` (shots), `SpawnBlockedMessage` (overflow pressure),
`GameOverMessage`, `RunResetMessage`, item activation messages (inventory/pickup —
check `Item/` for what exists), shield gain/spend (check `ProjectileHitResolver` /
shield messages).

## Open questions (for the implementation session)

1. Time base — unscaled wall-clock per level, or exclude ceremony/pause time? (Probably
   report both; pacing wants play-time, UX wants wall-time.)
2. Where records go on device builds — persistent-data JSONL with rotation? Editor-only
   first?
3. Does the run counter persist across app restarts (PlayerPrefs int) or reset?
4. Cheat runs (`CheatState.*` active) — tag records or drop them? (Tag, probably.)
5. Which existing gaps need new messages (item pickup vs use, shield spend reason)?

## Non-goals (v0)

Remote upload, dashboards, A/B plumbing, editor visualization tooling (a simple
`Tools > BalloonParty > Dump Telemetry` can come with the first sink if it's cheap).

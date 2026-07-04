@page arch_balance_flow Balance Flow

# Balance Flow

@image html balance_flow.svg "Balance Algorithm — Per-Actor Transit Tracking"

## What this diagram shows

How `BalloonBalancer` moves balloons to fill grid gaps while keeping concurrent spawn
animations aware of in-progress movement.

**Core algorithm:**
`Balance()` scans the grid bottom-up for unsupported dynamic actors (slots with no
occupant directly below them). For each unsupported actor it finds the best lower
slot via `GridBalanceQuery.OptimalNextEmptySlot` (a weighted recursive search that prefers
slots with more occupied neighbors above them — gravity-like settling). The actor is
removed from its current slot, placed in the target slot, and a DOTween path animation
is started along the grid waypoints.

**Transit tracking — `BalancePathHolder`:**
Before the tween starts, both the source and target slots are registered in
`BalancePathHolder` under the actor's identity. While registered, `ComputePath` treats
these slots as occupied-in-transit and emits a warning if a spawn animation path
crosses them. When the tween's `OnComplete` fires, `BalancePathHolder.Release(actor)`
clears both reservations.

This reservation persists across balance passes — a second balance triggered after
spawning does not erase transit data from a still-running first pass.

## Guidance

**Why the two-pass model (pre-spawn + post-spawn):**
- Pre-spawn pass consolidates gaps before new balloons are placed, so `ComputePath`
  finds the post-balance grid state rather than the mid-gap state
- Post-spawn pass handles new gaps created when a freshly placed balloon needs a
  neighbor that didn't exist at placement time

**`BalancePathHolder` is your read point for in-flight balance moves:**
If you're writing a system that needs to know whether a given slot is currently being
vacated or occupied by a balance animation, query `BalancePathHolder.IsInTransit(slot)`
before assuming a slot is stably empty.

**`OptimalNextEmptySlot` tie-breaking:**
When two candidate slots (directly above vs diagonally above) have equal weight, the
diagonal wins (`>=` comparison). This creates a slight bias toward the center of the
grid over time, which reads more naturally than strict left/right alignment.


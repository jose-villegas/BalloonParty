@page plan_level_up_timing Level-Up Timing

# Level-Up Timing

Replace the level-up ceremony's *gate-before-fire* model with a *flag-then-orchestrate* one: the
moment a pop's score completes the level requirement, raise a flag, hold the other systems, let the
shot play out its whole flight, and show the popup where the reload would have gone. Produced
2026-07-27 from a soft-lock José hit twice (stuck before the popup after completing level 12) and the
diagnosis that followed.

**Why it is available now:** `LevelController.ClaimProgress` already caps progress at the level
requirement and banks every excess point run-scoped, per colour. Points scored after the tipping pop
are therefore already safe — nothing has to be suppressed, dropped, or hurried, which is what let the
whole "interrupt the flight" apparatus exist in the first place.

---

## 1. What is wrong today

`CheckLevelUp` refuses to fire on four conditions, and is re-entered from only two places (a trail
arrival, and the pierce falling edge). Two of those conditions have **no re-check when they clear**,
so a refusal is forgotten forever:

| Gate | Purpose | Re-checked when it clears? |
|---|---|---|
| `_phase != Playing` | reentrancy | yes (phase transitions) |
| `_pierce.IsPiercing` | don't interrupt a plow; don't strand `PendingPierceHits` | yes (falling edge) |
| `_navigation != Game` | don't fire off-screen | **no** |
| `_lossForecast.LossImminent` | don't celebrate mid-death | **no** |

**The compound path that most likely caused the observed hang.** `LossImminent` is
`PendingCharges >= health`, and `PendingCharges` counts *queued rejected balloons* — overflow, which
is produced by the **spawn phase that runs at the end of a shot**. Meanwhile the pierce gate defers
the check to the pierce falling edge, which fires when the shot dies — i.e. immediately *before* that
spawn. So:

1. Final pops land; their trails launch (flight takes time).
2. Shot dies → pierce clears → `CheckLevelUp` runs, but confirmed progress is not complete yet
   (trails still in the air).
3. Spawn runs → overflow queued → `LossImminent` goes true.
4. The last trail arrives → `CheckLevelUp` → refused by the loss gate.
5. Nothing re-checks. Progress complete, no popup, no recovery except another scoring trail.

No exotic state required — just the final confirming trail landing after a spawn that queued
overflow. Late levels have more overflow, so the window is widest exactly where it was seen, and a
clean jump to level 12 (empty overflow queue, full hearts) cannot reproduce it.

**Second, independent candidate (same symptom):** a dropped trail (pool loss, or the "no target
provider" path) leaves projected complete but confirmed never done. §4 removes this one by
construction.

## 2. The new model

```
pop scores → requirement met (CLAIM time)
        │
        ├─ raise the flag: Phase → Completing
        ├─ orchestrator claims the time scale EXCLUSIVELY (slow-mo)
        ├─ other systems hold: no spawn, no reload, no board churn
        │
        ├─ next wall hit → release the slow-mo, flight runs normally
        │                  (a piercing shot's discharge happens here too, naturally)
        │
        └─ flight ends (shields spent / absorbed / death wall)
                   │
                   └─ popup INSTEAD of reload → Phase → Pending
                              → dismissal → Transitioning (level advances, Ascent)
                              → LevelTransitionCompletedMessage → Playing + reload
```

The boundary that presents the ceremony is **end-of-flight**, which always arrives. A recorded flag
consumed at a boundary cannot be forgotten; a refused check can. That is the whole fix.

## 3. What this deletes

- **The pierce gate** and its falling-edge subscription — the flight always finishes, so the
  discharge always happens and `PendingPierceHits` can never be stranded.
- **The loss and navigation gates** as *races*. Loss becomes a precedence rule evaluated once, at the
  boundary (see §6).
- **`ThrowerController.ScaleAwayActiveProjectile` on `LevelUpDismissedMessage`** — the shot is already
  gone by then. It stays for game-over only, which is the case it was written for.
- **`AllColorsConfirmed` as a gate on firing** (see §4). It remains useful as a *presentation* check.

## 4. Detect on projected progress, not confirmed

Detection moves to claim time (`ClaimProgress`), not trail arrival. The claim is authoritative the
instant the pop happens; the trail is presentation. Consequences:

- The **dropped-trail soft-lock stops existing**: a lost trail can no longer withhold the ceremony. At
  worst a bar is briefly short — a cosmetic glitch instead of an unplayable run.
- The projected/confirmed reconciliation (whose mismatch caused the old straggler misfire, fe1f8c07)
  stops being load-bearing for *firing*. Keep the straggler cap: it still protects the bars.
- The slow-mo plus the played-out flight is what gives trails time to land, so in practice the bars
  are full before the popup anyway — by construction rather than by gate.

## 5. The orchestrator is the existing phase machine, extended

**Do not add a second controller.** Two authorities over this ceremony is the exact shape of every
historical misfire here. `LevelController` already owns `LevelUpPhase`; add one phase:

`Playing → Completing → Pending → Transitioning → Playing`

- **`Completing`** — entered at the tipping claim. Owns the hold window and the time scale. Every
  other input is rejected for having no transition, as today.
- **`Pending`** — entered at end-of-flight; publishes `ScoreLevelUpMessage` (popup). Unchanged from
  here on.

`WillLevelUp()` (the pan-in arm) collapses into `Phase == Completing` — the pan-in, the slow-mo and
the flag become one beat instead of three signals that can disagree.

## 6. Holds, per system

Each hold is a *read* of the phase, not a new flag, so there is one source of truth:

| System | Seam today | Hold |
|---|---|---|
| Spawn **wave** | `BalloonSpawner` opens its sequence on `ProjectileDestroyedMessage` | skip while `Completing`; the transition resets the board anyway, so it is **cancelled**, not deferred |
| **Loose (pop) spawns** | `BalloonSpawner` rolls one per direct-hit pop (`DamageFlags.DirectHit`, `BalloonSpawner.cs:197-206`) | also skipped — the rule is *all* spawns, so the flag means "the board is finished growing". Simpler to reason about than a list of exceptions, and it keeps the played-out flight clean: nothing new enters the board after the level is already decided |
| Reload | `ThrowerController` reloads on `ProjectileDestroyedMessage` → swap | skip while `Completing`; the reload already happens on `LevelTransitionCompletedMessage` for this path (5ce98bb6) |
| Balance pulses | `BalloonBalancer.Tick` (flight-gated) | allowed — the board is still live while the shot flies |
| Loss / overflow | hearts launch from the spawn | cancelled with the spawn, which resolves the precedence question: **a completed level wins over the overflow it would have caused.** A loss already in progress before the tipping pop keeps priority |

## 7. Time-scale ownership

`TimeScaleService.Apply` takes the **minimum** of all active claims — "slowest wins". Nobody can speed
the world up, but a `LastShield` curve or a `PierceDischarge` dip can drag it slower than the ceremony
intends, so exclusivity has to be real:

- `ClaimExclusive(source, value)` records `_exclusiveOwner`; `Apply` then uses **only** the owner's
  value.
- Other sources keep claiming and releasing as normal — their values simply never reach
  `Time.timeScale` while exclusivity holds, so an in-flight source resumes correctly when it ends.
- `ReleaseExclusive(source)` clears the owner and re-applies the minimum of what remains.

**Suppress at Apply, do NOT cancel the other claims.** Cancelling was considered and rejected on
evidence: two of the five claimants re-claim *every frame* — `LastShield`
(`ProjectileDoomedTimeScaleController.cs:65`, curve over elapsed time) and `Cinematic`
(`CameraRigCinematic`, per-frame curve plus a restore curve). Releasing them just invites the claim
back on the next frame, and `LastShield` is exactly the collision case that matters (a doomed shot
completing the level on its last pop is a likely pairing, not a rare one). Owner-only `Apply` is what
actually suppresses them. Suppressing `LastShield` is safe: the doomed drift is eased over normalized
GAME time in the resolver, so muting the claim changes the drift's wall-clock length, never its shape.

**Two windows, not one** — whole-window ownership would contradict §2's decision to let the flight
play out:

| Window | Time scale | Why |
|---|---|---|
| **A — the tipping beat**: flag → next wall hit | exclusive, owner-only | the unmistakable "that was the last shot" signal, immune to a slow-mo already running |
| between: wall → flight end | released, normal rules | the flight plays out WITH its own punctuation — a pierce discharge still lands its dip |
| **B — the ceremony**: flight end → popup/Ascent | exclusive again | a handoff: `LevelUpPopup` (freeze at 0) and `LevelTransition` are themselves claimants, so blanket ownership until the popup would suppress the popup's own freeze |

The signal that reads as "last shot" is the *transition into* slow-mo plus the pan-in, not its
duration; holding it through the flight adds no emphasis and subtracts the discharge's.

**What exclusivity in window A actually buys** (the minimum rule means two claims never compound
multiplicatively, so the risk is not "twice as slow"):

1. Without it the world is pinned at whatever the OTHER claim wants — the beat asks for 0.5, a
   `LastShield` curve says 0.25, you get 0.25 and the ceremony's pacing is not yours.
2. Worse, the **release at the wall would not restore normal time**, because the other claim is still
   active. You hit the wall expecting the snap back to full speed and the world just stays slow.

The second is the one that would read as a bug rather than as bad tuning, and it is the reason the
window needs ownership rather than merely a well-chosen value.

Note the intended consequence on the far side of the wall: releasing exclusivity lets the recorded
claims apply again, so a genuinely doomed shot resumes its own last-breath bullet-time. That is
correct — "the flight plays normally" includes the flight's own slow-mos.

## 8. Risks and what only a playtest can answer

- **This changes when the popup appears** — after the flight instead of interrupting it. That is the
  point, and it is a feel change José has to sit with.
- **`LevelUpCinematic` carries the same two gates** at three points (`LossImminent`, navigation).
  They must move to the phase or they reintroduce the hole one layer down.
- **A very long armed flight** now delays the popup by its whole duration. Taps accrue for the whole
  flight and banked charges re-arm the lance, so "one more wall" can be several seconds. If that
  reads as dead air, the mitigation is a cap on the hold, not a return to interrupting.
- **Cancelling the spawn** for the tipping shot is a balance change (one fewer spawn wave per
  level-up). Deliberate, but worth watching over a few levels.

## 9. Tests

- `LevelControllerTests`: tipping claim → `Completing`; end-of-flight → `Pending` exactly once; a
  trail arriving during `Completing` does not re-fire; a dropped trail still reaches `Pending`.
- The old latch, as a regression: complete the requirement while `LossImminent` is true → the
  ceremony still fires at end-of-flight (this is the reported bug, pinned).
- `TimeScaleService`: exclusivity ignores a competing claim, and releasing it restores the minimum of
  the recorded remainder.
- Spawner/thrower: no spawn and no reload while `Completing`.

## 10. Sequencing

1. Time-scale exclusivity (isolated, testable alone).
2. The `Completing` phase + detection at claim time, with the four gates removed.
3. The holds (spawn, reload).
4. Delete the now-dead paths (`ScaleAwayActiveProjectile` on dismissal, the pierce subscription,
   `WillLevelUp`'s separate progress read).
5. Watchdog log (see §11) — keep it permanently, dev-only.

## 11. Keep a watchdog regardless

Whatever the fix, add a dev-only warning when progress is complete and the phase has not moved within
a couple of seconds, dumping phase, pierce, navigation, `LossImminent`, and per-colour
confirmed-vs-required. This class of bug is rare, unreproducible on demand, and costs a whole session
to diagnose from a description. The next occurrence should name its own cause.

@page plan_sweep Sweep

# Sweep — speed boost for clearing a corridor

> A "tap" is a speed boost the projectile earns mid-flight — it makes the ball faster and
> moves it closer to piercing (punching through balloons instead of stopping). Cruise taps
> come from bouncing off walls in empty corridors. **Sweep** adds a second way to earn
> taps: pop every balloon between two walls so the corridor behind the ball is now empty,
> and the ball earns a Sweep tap at the wall hit.
>
> **No changes until this plan is actioned.**

---

## Motivation

Cruise rewards a ball for surviving in empty space — bouncing off walls with nothing to
hit. Sweep rewards a ball for *creating* empty space — popping every balloon in a
corridor so it's clean behind it. Both feed the same speed/piercing system but encourage
complementary play: long avoidance runs vs. surgical clearing shots.

---

## Trigger conditions

At the moment a projectile **hits a wall**, evaluate:

1. **Popped at least one balloon this segment, and every hit was a 1HP one-shot** —
   "segment" = the straight-line path between the previous wall bounce (or cannon
   origin on the first leg) and this wall hit. The projectile model already tracks
   segment pops; it also tracks whether every balloon hit on that segment had exactly
   1 hit remaining at contact time. Any hit on a balloon with HP > 1 invalidates the
   sweep, even if a later hit popped something else.
2. **Path behind is now clear** — circle-cast backward from the wall-hit position, in
   the OPPOSITE direction of travel, using the same radius as `IsPathClearAhead()`
   (`_contactRadius`), over the segment length. If no balloon collider is hit → clear.

Both conditions must be true → apply one Sweep speed tap.

### Timing guarantee

The check runs AFTER the last balloon on the segment has been removed from physics (the
pop resolves before the wall contact in the same fixed step). The backward cast therefore
sees the post-pop state.

---

## Effect

A Sweep tap is mechanically identical to a Cruise tap at the moment it is awarded:

- Adds one `CruiseSpeedPerShield` worth of speed into the shot's shared tap-speed system.
- Counts toward the piercing threshold (`CruisePiercingTapThreshold`).
- Resets `CruiseTapElapsed` so the same tap-beat ease / aura feedback replays.

The projectile gets faster and moves closer to earning piercing, exactly as if it had
received a Cruise tap.

---

## Comparison with Cruise

| | Cruise tap | Sweep tap |
|---|---|---|
| Trigger | Wall bounce after N consecutive bounces with no balloon contact | Wall hit after 1HP-only segment pops AND backward path is now clear |
| Condition | Absence of targets (empty corridor rattling) | Destruction of all targets, with no >1HP hit anywhere on the segment |
| Speed system | Shared | Shared — same `CruiseSpeedPerShield`, same tap ease, same piercing threshold |
| Stacking | One tap per qualifying bounce | One tap per qualifying wall hit (v1) |

---

## Detection details

- **Segment pop count**: increment a counter on the model each time the projectile pops
  a balloon; reset it to zero on each wall bounce (before the Sweep check reads it).
  Read the *pre-reset* value for the Sweep condition, then reset.
- **Segment sweep validity**: start the segment with `SegmentSweepValid = true`; clear it on
  any hit where the balloon had more than 1 hit remaining at contact time. Sweep requires
  both `SegmentPopCount > 0` and `SegmentSweepValid`.
- **Backward circle-cast**: `Physics2D.CircleCast(wallHitPos, _contactRadius,
  -travelDir, segmentLength, 1 << BalloonsLayer)`. Reuses the same layer mask and radius
  as `IsPathClearAhead()`.
- **Segment length**: magnitude of `(wallHitPos - lastBouncePos)`. Track
  `lastBouncePos` in the flight state (or compute from position history).

---

## Implementation location

`ProjectileView` — inside the existing wall-hit callback where `TryEnterCruise` is
called. Adjacent to that call, add a `TryAwardSweepTap` method:

```
private void TryAwardSweepTap(Vector3 wallHitPos, Vector3 travelDir)
{
    if (!_config.SweepEnabled) return;
    if (_model.Flight.SegmentPopCount <= 0) return;
    if (!_model.Flight.SegmentSweepValid) return;

    var segLen = (_model.Flight.LastBouncePosition - wallHitPos).magnitude;
    var hit = Physics2D.CircleCast(wallHitPos, _contactRadius, -travelDir, segLen,
                                   1 << BalloonsLayer);
    if (hit.collider != null) return;

    // Award tap — same effect as a cruise tap.
    _model.Flight.CruiseTapElapsed = 0f;
    _model.Flight.SweepSpeedBonus += _config.CruiseSpeedPerShield;
    _model.Flight.TotalCruiseTaps++;
}
```

No new controller or service; logic stays in the view alongside Cruise.

---

## Configuration

Keep in `IProjectileFlightConfig` (and the backing `ProjectileFlightConfig` SO):

| Field | Type | Purpose |
|---|---|---|
| `SweepEnabled` | `bool` | Feature toggle — false disables the mechanic entirely |
No dedicated Sweep speed field. Sweep taps reuse `CruiseSpeedPerShield` so both tap
sources always feed the exact same speed system.

---

## Model changes

Add to `ProjectileFlightState`:

| Field | Type | Purpose |
|---|---|---|
| `SegmentPopCount` | `int` | Pops since last wall bounce; reset on bounce |
| `SegmentSweepValid` | `bool` | Starts true each segment; false if any hit was not on a 1HP balloon |
| `LastBouncePosition` | `Vector3` | Position of last wall bounce (or fire origin) |

Both reset naturally on projectile recycle (fresh `ProjectileFlightState` per shot).

---

## Performance

- One `Physics2D.CircleCast` per wall hit, only when `SegmentPopCount > 0`.
- Wall hits happen far less frequently than pops — negligible cost.
- Same radius / layer as `IsPathClearAhead()` — no new physics setup.

---

## Telemetry hook (Phase 2)

`SweepTriggeredMessage` — published when a sweep tap is awarded. Feeds into the
telemetry service (@ref plan_gameplay_telemetry) for balance analysis: frequency of sweep
taps vs. cruise taps, correlation with level completion.

---

## Scope & constraints

- **v1**: one sweep tap per wall hit — no multi-stacking from a single bounce.
- Logic lives in `ProjectileView` adjacent to Cruise — no new controller/service.
- Tap applies to the current projectile immediately — no next-shot carryover.
- Estimated complexity: ~50–80 LOC across 2–3 files (view, flight state, config).

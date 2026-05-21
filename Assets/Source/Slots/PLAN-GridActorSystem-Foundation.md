# Grid Actor System — Foundation Reference (Phases 1–7.5)

> Compact record of completed phases. Working plan for Phase 8+ is in
> `PLAN-GridActorExpansion.md`.

---

## Key Design Decisions — Summary

### Mobility axis: `SlotActorKind`
- `Static` — fixed, balancer skips, always stable
- `Dynamic` — balancer may relocate; has `IsStable` animation state

Interface hierarchy:
```
ISlotActor                  — plain Vector2Int SlotIndex; Kind
IDynamicSlotActor           — new IReadOnlyReactiveProperty<Vector2Int> SlotIndex; IsStable (read-only)
IWriteableDynamicSlotActor  — new ReactiveProperty<Vector2Int> SlotIndex; IsStable (mutable)
IWriteableSlotActor         — plain Vector2Int SlotIndex with setter (statics only)
```

### Durability axis: `IHitable` + `IHasDurability`
- `IHitable` — declares projectile response (`EvaluateHit → HitOutcome`)
- `IHasDurability : IHitable` — adds reactive `HitsRemaining`; `EvaluateHit` decrements inline
- Actors without `IHitable` have no collider — no convention-based fallback

```
HitOutcome: Deflect | PassThrough | Pop | Absorb
```

### Item slot axis: `IHasItemSlot`
- `IHasItemSlot : IHasColor` — presence IS the capability; absence = cannot hold items
- `IHasWriteableItemSlot : IHasItemSlot` — `ReactiveProperty<ItemType> Item`
- `BalloonModel` opts in; `ToughBalloonModel` does not

### Spawner type selection
`BalloonSpawner` switches on `entry.BalloonType` (`Simple → BalloonModel`,
`Tough → ToughBalloonModel`). `IsPaintable` / `CanHoldItem` removed from config.

### Capability interfaces in `Slots/`
`IHasColor`, `IHasWriteableColor`, `IHasScore`, `IHasNudge`, `IHasItemSlot`,
`IHitable`, `IHasDurability`, `IPassThrough`, `SlotActorKind`, `HitOutcome`

---

## Phase Status

| Phase | Status |
|---|---|
| 1 — Define actor interfaces | ✅ Complete |
| 2 — Refactor `SlotGrid` | ✅ Complete |
| 3 — Update consumers | ✅ Complete |
| 4 — Update tests | ✅ Complete |
| 5 — Update documentation | ✅ Complete |
| 6 — Static actor evaluation | ✅ Complete |
| 7 — `IHitable` + Durability abstraction | ✅ Complete |
| 7.5 — `BalloonModelBase` hygiene + `IHasItemSlot` | ✅ Complete |
| 8+ — Expansion | → `PLAN-GridActorExpansion.md` |

### Phase 7 — key changes
`IHitable`, `IHasDurability`, `IDynamicSlotActor`, `BalloonModelBase`, `BalloonModelConfig`,
`HitOutcome.PassThrough` + `Absorb`, `BalloonController` switched to `msg.Outcome`,
`BalloonBalancer` + `NudgeService` cast to `IDynamicSlotActor`.

Deferred: `UnbreakableBalloonModel`, `Absorb` routing in `ProjectileView`.
Both delivered in Phase 8.1 (see expansion plan).

### Phase 7.5 — key changes
`IHasItemSlot`/`IHasWriteableItemSlot`; `Item`/`CanHoldItem` removed from
`BalloonModelBase`; reactive `SlotIndex` on `IDynamicSlotActor`; `BalloonSpawner` type
switch; all consumers updated; `ItemSlotTests` passing.

`ScoreValue` + `NudgeOverrides` remain on `BalloonModelBase` — moved when the first
non-scoring or non-nudgeable actor demands it (tracked in expansion plan).

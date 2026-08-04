# UI / Telemetry

Puts a gameplay metric into a label without writing code for it. Drop `MetricLabel` on a
`TMP_Text`, type the format into the label's own text field (`"Reds popped: {0}"`), and pick
the value from a dropdown generated out of `MetricCatalog`.

Adding a metric to the catalog makes it selectable here. Nothing in this folder changes.

## Contents

| File | What it does |
|---|---|
| `MetricBinding` | `[Serializable]` struct naming **one** value: which snapshot, how to read it, and the id that needs. The unit the whole folder is built around |
| `MetricBindingSource` | Which of `ILevelMetricsView`'s three reactive snapshots to read — ceremony level, last flushed level, or the run |
| `MetricValueKind` | What the binding's id fields mean: a scalar metric, a timer, one axis bucket, or a record field |
| `RecordField` | The snapshot's own identity fields (`LevelIndex`, `Completed`) — everything else the envelope carries lives on `TelemetryEnvelope`, never on the snapshot, so it is out of reach here by construction |
| `MetricValueResolver` | Pure C#: `(snapshot, binding) → string`. The only file here with real logic, and the only one with tests |
| `MetricLabel` | The `MonoBehaviour`. Holds one binding, subscribes to its source, renders through `FormattedLabel` |
| `MetricLabelBinder` | `IStartable` that binds every label under a scope at `Start` |
| `MetricLabelRegistration` | `RegisterMetricLabels(scope)` — call it from a UI `LifetimeScope`. Already wired into `LevelUpLifetimeScope` and `GameOverLifetimeScope` |

## Formatting comes from the catalog, not the label

The author writes the sentence; the unit column decides the number's shape. `seconds` renders
as `1:23` (and `1:02:05` past an hour), `level_hundredths` as `87%`, everything else as a
thousands-separated integer. So a label never has to know that the danger level is stored in
hundredths because `MetricSet` holds ints.

## Why a dropdown and not a component per metric

A component per metric means a new component every time the catalog grows. More importantly,
binding through `(MetricId, axis, bucket)` means **no label ever touches the snapshot's typed
breakdown properties** (`PopsByColor`, `PointsByColor`, `ItemsActivated`). Those are scheduled
to leave the snapshot when the counting engine is extracted as a standalone library — see
*Separability* in @ref plan_gameplay_telemetry. Going through the catalog means that removal
does not touch a single prefab.

## Colour buckets are stored by name

The colour axis is sized from `IGamePalette.ProgressColorNames` at runtime, so a stored index
would silently repoint every label the day the palette is reordered — a label that reads "Reds
popped" quietly showing the blue count. Balloon-type and item-type buckets are enum ordinals
and store as-is.

A colour name that no longer resolves warns **once** and shows the placeholder. It never
throws: a popup is the worst possible place for an exception, and the binding is serialized in
a prefab, so it would fail identically on every frame the popup is open.

## Reading the ceremony snapshot

Two rules, both from `Game/Telemetry/README.md`: read it after the popup's gate `await` (never
inside a `ScoreLevelUpMessage` handler), and treat `LevelIndex` as the only trustworthy
discriminator of stale vs. current. `MetricLabel` binds reactively, so it satisfies the first
by construction.

Every label must survive the empty snapshot — it is a real runtime state, not a defensive
hypothetical, since an aborted level-up clears `CeremonyLevel` to it while the popup may still
be awaiting its gate.

## Editor

`MetricBindingDrawer` (`Assets/Source/Editor/`) is a `[CustomPropertyDrawer]` on
`MetricBinding` rather than a custom editor for `MetricLabel` — Unity reuses a property drawer
for each element of an array, so the day a label needs several values, the drawer does not
change. Do not fold the binding's fields into the `MonoBehaviour`; that forfeits it.

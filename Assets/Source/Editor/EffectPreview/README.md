# EffectPreview

Reusable editor-time preview system for `EffectView` subclasses. Lets designers play effect animations inside the Inspector (prefab edit mode or scene) without entering Play mode.

## Architecture

The system follows a **player + module** composition pattern:

```
EffectViewPreviewPlayer          ← owns animation loop, color picker, config caches
  └── IEffectPreviewModule       ← pluggable rendering logic per effect type
```

The **player** handles everything shared across all effect previews:
- `EditorAnimationLoop` (play/pause/stop, speed slider, delta-time ticking)
- `PaletteColorPicker` (color selection from `GamePalette`)
- `EditorAssetCache<SlotGridConfig>` and `EditorAssetCache<ItemConfiguration>`
- Inspector GUI layout (header, disabled scope during playback, controls)
- Repaint scheduling (`SceneView.RepaintAll` + inspector `Repaint`)

The **module** handles everything specific to a particular effect:
- Custom inspector controls (blob count, target count, etc.)
- Preview setup (positioning objects, generating targets)
- Per-frame tick logic (moving blobs, updating line renderers)
- Cleanup (hiding objects, destroying temporaries)

## Contents

| File | What it provides |
|---|---|
| `IEffectPreviewModule` | Interface — `UsesColorPicker` (whether the player shows a palette picker), `DrawGUI()`, `Start(context)`, `Tick(delta)`, `CleanUp()` |
| `EffectPreviewContext` | Data class passed to `Start()` — `Tint`, `Settings`, `GameConfig` |
| `EffectViewPreviewPlayer` | Reusable player — animation loop, color picker, config caching, inspector GUI. Constructed with a module, header label, `ItemType`, and repaint callback |
| `EditorGridHelper` | Static utility — `RandomSlotPositions` (picks N random slots sorted by distance from origin). Delegates hex grid math to `SlotGrid.IndexToWorldPosition(index, separation, offset)` — single source of truth for index-to-world conversion |
| `PaintSplashPreviewModule` | Module for `PaintSplashView` — blob arc flights with particles, spin, splash particle spawning (prefab-stage-aware). Delegates position/scale/MPB math to `PaintSplashView.ComputeBlobFlight` and `ApplyBlobMaterial` |
| `ChainLightningPreviewModule` | Module for `ChainLightningView` — generates random grid positions as targets, delegates bolt buffer building to `ChainLightningGeometry.BuildBoltBuffers` (with configurable `fractalDecay`), builds a smooth glow path via `ChainLightningGeometry.BuildGlowPath`, interpolates glow position every tick using `PathHelper.SampleAt`, animates forward growth + retraction via delta-time state machine |
| `LaserPreviewModule` | Module for `LaserView` — recolours the view's wired renderers with the picked tint and samples the effect's `AnimationClip` frame-by-frame via `SampleAnimation` (`Animator.Update` is unreliable outside Play mode), re-applying the tint after each sample |

## Adding a new effect preview

1. Create a class implementing `IEffectPreviewModule`
2. In `DrawGUI()`, draw any type-specific controls (sliders, labels)
3. In `Start(context)`, set up the preview using `context.Tint`, `context.Settings`, `context.GameConfig`
4. In `Tick(delta)`, advance the animation — return `true` to keep running, `false` when done
5. In `CleanUp()`, tear down all preview state
6. Create a `CustomEditor` for the target view, construct an `EffectViewPreviewPlayer` with your module, and call `DrawInspectorGUI()` from `OnInspectorGUI()`

Example (minimal):

```csharp
[CustomEditor(typeof(MyEffectView))]
public class MyEffectViewEditor : NaughtyInspector
{
    private EffectViewPreviewPlayer _player;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        _player ??= new EffectViewPreviewPlayer(
            new MyPreviewModule((MyEffectView)target),
            "My Effect Preview",
            ItemType.MyType,
            Repaint);
        _player.DrawInspectorGUI();
    }

    protected override void OnDisable()
    {
        _player?.Stop();
        base.OnDisable();
    }
}
```

## Design notes

- **Single source of truth** — preview modules reuse runtime math wherever possible. `PaintSplashPreviewModule` calls `PaintSplashView.ComputeBlobFlight` and `ApplyBlobMaterial` for position/scale/MPB computation. `ChainLightningPreviewModule` calls `ChainLightningGeometry.BuildBoltBuffers`, `BuildGlowPath`, and uses `PathHelper.SampleAt` for per-tick glow interpolation along the smooth centroid path. `EditorGridHelper` delegates to `SlotGrid.IndexToWorldPosition(index, separation, offset)`. Only editor-specific behavior (particle simulation, prefab-stage instantiation, delta-time state machine vs async) lives in the modules
- Modules access private fields on their target views via `System.Reflection.FieldInfo` — cached as `static readonly` to avoid repeated lookups
- The player creates `EffectPreviewContext` once per play cycle; modules should not cache the context across multiple play/stop cycles
- `EditorGridHelper.RandomSlotPositions` sorts results nearest-first from the given origin, matching the runtime `LightningItemHandler` sort behavior
- Prefab edit mode requires `SceneManager.MoveGameObjectToScene(go, prefabStage.scene)` for instantiated objects to be visible — see `PaintSplashPreviewModule.SpawnEditorSplash`
- Particle systems in edit mode require manual `ParticleSystem.Simulate()` calls since `Play()` doesn't work outside Play mode


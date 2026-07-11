# Maps

Editor-only **Game Render Maps** window (`Tools > BalloonParty > Game Render Maps`) — a
unified play-mode preview for the project's shared render-target "maps," with
per-channel RGBA isolation. Supersedes `DisturbanceFieldPreviewWindow`; the
per-inspector previews on `SceneCaptureService` / `ScreenSpaceLightService` stay as-is
(useful with that component selected in the Hierarchy) but this is the one-stop window
for comparing any of them.

## Contents

| File | What it does |
|---|---|
| `GameRenderMapsWindow` | The `EditorWindow`. A toolbar dropdown picks the map; four toggle buttons (R/G/B/A) mask the preview; a `Custom…` entry lets you drop in any `Texture` by reference. Repaints continuously via `EditorApplication.update` so a live-updating buffer (the GI light buffer, the disturbance field) is never stale. |

## What it shows

Three built-in maps, plus a `Custom…` slot for ad hoc inspection:

- **Scene Capture** — the downscaled capture-layer scene color (`Shader.GetGlobalTexture(_SceneCaptureTex)`), read from `SceneCaptureService`. RGB is scene color; alpha is a sprite coverage mask (~0 over open sky/ground, ~1 over casters) that doubles as the GI light buffer's occlusion/shadow source, since the capture camera clears with alpha 0.
- **Disturbance Field** — the global disturbance texture (`Shader.GetGlobalTexture(_DisturbanceTex)`). R is density (1.0 = undisturbed equilibrium, stamped toward 0.0 by cloud/foliage displacement); G/B are 0.5-biased X/Y displacement; A is unused (always written 1.0 by `DisturbanceDiffusion.shader`).
- **GI Light Buffer** — `ScreenSpaceLightService.LightTexture`, found in-scene via `FindFirstObjectByType`. RGB is bounce color (scene color marched toward the light, minus ambient sky); A is shadow amount (occluder coverage marched away from the light, masked off the casters themselves so only the ground beside them darkens).
- **Custom…** — any `Texture` you assign via an `ObjectField`, for one-off inspection outside the three registered maps.

Each map that only binds during Play Mode shows an explanatory `HelpBox` instead of a
blank preview when unavailable (not in Play Mode, or the owning service/component isn't
in the scene yet).

## Channel toggles & tooltips

The four R/G/B/A toggle buttons mask the preview to the channels you care about. Each
button's tooltip is per-map and per-channel — e.g. the Disturbance Field's `G` toggle
reads "Displacement X — 0.5-biased…" rather than a generic "green channel" label — so
the meaning of each channel is discoverable without leaving the window. The `Custom…`
map has no defined channel semantics, so its tooltips fall back to a generic "Raw {0}
channel" format.

## Registry pattern

Each built-in map is a `MapDescriptor`: a name, a `Func<Texture> Fetch`, an
unavailable-hint string, and four channel-tooltip strings. Descriptors are built once
into a static `Descriptors` array (`BuildDescriptors`) — this is a plain lookup
registry, not a service dependency: the window doesn't inject or reference
`SceneCaptureService`/`ScreenSpaceLightService` directly, it only knows the global
shader-texture IDs (`_SceneCaptureTex`, `_DisturbanceTex`) and, for the GI buffer,
looks the service up by type at fetch time. This keeps the window decoupled from those
systems' lifetimes.

The GI light buffer is the one exception to "just a global texture fetch": it's
ping-ponged (see `ScreenSpaceLightService.LightTexture`), so `FetchLightBuffer` calls
`FindFirstObjectByType` and re-resolves `LightTexture` fresh on every repaint rather
than caching the service or the texture reference — caching either would risk showing a
buffer that's already been swapped out from under it.

## ChannelPreview shader

`Assets/Shaders/BalloonParty/Editor/ChannelPreview.shader` (`Hidden/BalloonParty/Editor/ChannelPreview`)
is the editor-only blit that masks `_MainTex` down to the toggled channels via
`_ChannelMask`. `GameRenderMapsWindow` resolves it with `Shader.Find` rather than a
serialized reference — safe here because this window is editor-only tooling that's
never shipped, so the build-stripping concern that makes `ScreenSpaceLightService`
serialize its shaders doesn't apply. If the shader fails to load (`Shader.Find` returns
null — `dotnet build` can't validate shader compilation, so a typo here only surfaces in
the editor), the window falls back to `EditorGUI.DrawPreviewTexture` with no material
instead of breaking the preview.

### Alpha-only-when-isolated display rule

Exactly one channel toggled on replicates that channel as grayscale, so a lone R/G/B/A
toggle reads clearly on its own. Multiple channels toggled on keep each in its own RGB
slot and zero the rest — alpha has no RGB slot of its own, so **alpha only visibly
contributes to the preview when it's the sole channel selected**; with other channels
also on, alpha is masked but not shown. The shader always forces output alpha to 1 so
`EditorGUI.DrawPreviewTexture` never blends the preview against the window background.

## Dependencies

- `BalloonParty.Editor` assembly (editor-only platform).
- References `BalloonParty.Display` (`SceneCaptureService`, `ScreenSpaceLightService`) for the global texture IDs and the GI buffer lookup — no dependency on `BalloonParty.Runtime` beyond that.

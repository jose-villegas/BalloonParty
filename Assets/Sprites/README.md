# Sprites

Sprite textures and atlases, grouped into per-domain folders (they were previously all
dumped flat here). References — prefabs, materials, atlases — are all by **GUID**, so
sprites can be moved between folders freely; only the `.png` + its `.png.meta` must move
together.

## Folder layout

| Folder | Contents |
|---|---|
| `Balloons/` | Balloon body/shine/spec/blur; `Unbreakable/` holds the chrome-sphere parts |
| `Thrower/` | Thrower shell, caparace, and tap-charge sprites |
| `Projectile/` | The thrown ball only — `bullet`, `bullet_blur` |
| `Items/` | Item effect art — bomb, laser, lightning, spark, shield-lose (bomb/laser/lightning/shield) |
| `UI/` | Shield counter/fill/fx, HUD counter backing |
| `VFX/` | Ambient particle art — circle/dust/smoke/star/glow/gradient workhorses |
| `Trails/` | TrailRenderer / LineRenderer textures (`trail`, `score_trail`, `score_trail_shadowed`) — see the atlas note below |
| `Scenario/` | `background` (full-screen; intentionally un-atlased) |
| `Heart/` | Heart icon + motion-blur trail |
| `Noise/` | Shader noise textures (cloud noise) — not atlased |
| `Baked/` | `SpriteShadowBaker` / `SpriteLayerCombiner` output (mirrors prefab paths) |

## Sprite atlases

Four atlases batch sprites that draw together, reducing texture binds. They pack sprites
**by GUID** (not by folder), so the folder move above left them intact.

| Atlas | Packs (domains) |
|---|---|
| `Balloons.spriteatlas` | `Balloons/` |
| `Thrower.spriteatlas` | `Thrower/` |
| `UI.spriteatlas` | `UI/` |
| `VFX.spriteatlas` | particle/beam art from `VFX/`, `Projectile/`, `Items/` (minus the material-only textures noted below) |

`Scenario/`, `Noise/`, `Baked/`, and `Trails/` are deliberately un-atlased.
`Trails/` in particular: a Trail/LineRenderer material references its texture directly as
`_MainTex`, not as an atlased `Sprite`, so atlasing a trail texture gives no batching
benefit and just duplicates it (standalone for the material + a copy in the atlas). Same
reasoning applies to any texture consumed only as a material `_MainTex` rather than by a
`SpriteRenderer`/UI `Image`.

## Adding sprites

Drop the `.png` in the matching domain folder, then add its GUID to that atlas's
`packables` list (the atlases currently list sprites individually).

Folder packing (pointing an atlas at a whole folder instead of a GUID list) is *not*
a clean fit here: the VFX atlas spans three folders, and a few textures inside atlased
folders are deliberately excluded (line/trail material textures — see below). Folder
packing would re-include those. So the GUID-list approach stays.

## Project settings

Sprite packing is enabled via `m_SpritePackerMode: 2` (Sprite Atlas V1 — Always Enabled)
in `ProjectSettings/EditorSettings.asset`.

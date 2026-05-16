# Sprites

All sprite textures and sprite atlases live under `Assets/Sprites/`.

## Sprite atlases

Sprites are packed into 4 atlases by usage domain, reducing draw calls by batching sprites that share an atlas texture into fewer texture binds.

| Atlas | Sprites | Typical renderers |
|---|---|---|
| `Balloons.spriteatlas` | balloon, balloon_blur, balloon_shine, balloon_spec, bomb_cord, bomb_spec | SpriteRenderers on balloon prefabs |
| `UI.spriteatlas` | progress, shield, shield-fill, shield-fill-blur, shield-fx, background_counter, score_trail, play, plus, plus_blur | UI Images and SpriteRenderers |
| `VFX.spriteatlas` | star_01, star_07, smoke_04, smoke_09, spark, circle_dust, cartoon-smoke, circle, circle_glow, blur_circle, light_01, trace, trail, gradient, pixel, q-moon, bullet, bullet_blur, laser, laser_shine, lightning, lightning_chain | ParticleSystems and projectile renderers |
| `Thrower.spriteatlas` | thrower, thrower_blur, thrower_spec, thrower_caparace_back, thrower_caparace_front, thrower_tap, thrower_tap_filled, thrower_tap_filled_hlf | SpriteRenderers on thrower prefab |

`background.png` is intentionally excluded — full-screen backgrounds don't benefit from atlasing.

## Project settings

Sprite packing is enabled via `m_SpritePackerMode: 2` (Sprite Atlas V1 — Always Enabled) in `ProjectSettings/EditorSettings.asset`.

## Adding sprites

1. Place the `.png` in `Assets/Sprites/`.
2. Add its GUID reference to the appropriate `.spriteatlas` file's `packables` list.
3. If the sprite is used with a custom shader that manipulates UVs (scaling, shadow offset), verify it works in the atlas context — atlas UVs are remapped from `[0,1]` to a sub-rect of the atlas texture.


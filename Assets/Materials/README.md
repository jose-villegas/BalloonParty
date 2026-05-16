# Materials

All materials live under `Assets/Materials/`, organized by feature.

## Folder structure

| Folder | Contents |
|---|---|
| `Balloon/` | Main balloon sprite, specular blur overlays, tough balloon, balloon pop/smoke particles |
| `Projectile/` | Projectile sprite, trail, prediction trail, shield trail |
| `Items/Bomb/` | Bomb explosion particles and spark trails |
| `Items/Lightning/` | Lightning line renderers and spark particles |
| `Items/Paint/` | Paint blob sprites and splash trail particles |
| `Items/Shield/` | Shield gain/lose/bounce particles, shield material |
| `UI/` | Score trail, blur/shadow sprites, level-up rays/fill, color progress |

## GPU instancing policy

| Category | Instancing | Reason |
|---|---|---|
| SpriteRenderer materials using custom shaders (color-only variation) | ✅ Enabled | Color is per-instance via `unity_SpriteRendererColorArray` |
| Particle/trail/line materials using **built-in** Unity shaders | ✅ Enabled | Unity's built-in shaders handle instancing natively |
| Materials with per-instance `MaterialPropertyBlock` properties | ❌ Disabled | Instancing batching discards MPB values not in the instancing buffer |
| Particle/trail materials using **custom** shaders | ❌ Disabled | ParticleSystem/TrailRenderer don't populate `unity_SpriteRendererColorArray` |

### Materials with instancing disabled (11)

| Material | Shader | Per-instance MPB properties |
|---|---|---|
| `ToughBalloonMaterial` | ToughBalloon | `_DamageProgress`, `_VoronoiSeed` |
| `PaintBlob` | PaintBlob | `_TimeOffset` |
| `PaintFlyingBlob` | PaintBlob | `_TimeOffset` |
| `PSMaterial_BalloonPop` | SpriteShadow | _(particle on custom shader)_ |
| `PSMaterial_ToughBalloonPopSmoke` | SpriteShadow | _(particle on custom shader)_ |
| `PSMaterial_BombRangePU` | SpriteShadow | _(particle on custom shader)_ |
| `PSMaterial_ShieldGain` | SpriteShadowComposite | _(particle on custom shader)_ |
| `PSMaterial_ShieldGainPU` | SpriteShadow | _(particle on custom shader)_ |
| `PSMaterial_ShieldLose` | SpriteShadow | _(particle on custom shader)_ |
| `TrailMaterial_Projectile` | SpriteShadow | _(trail on custom shader)_ |
| `TrailMaterial_Shield` | SpriteShadow | _(trail on custom shader)_ |

### Adding new materials

- If the material uses a **custom shader** on a **SpriteRenderer** with no per-instance MPB properties → enable instancing.
- If the material uses a **custom shader** on a **ParticleSystem** or **TrailRenderer** → disable instancing.
- If the material sets per-instance values via `MaterialPropertyBlock` → disable instancing.
- If the material uses a **built-in Unity shader** → enable instancing (Unity handles it).


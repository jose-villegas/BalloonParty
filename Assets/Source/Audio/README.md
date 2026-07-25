# Audio / SFX

Gives gameplay a voice: every time something happens on screen — a balloon pops, a shot
fires, a shield breaks, you level up or lose — this system decides what sound to play and
plays it. It is a pure **listener**: it never triggers gameplay, only reacts to it. Native
Unity audio only (`AudioSource` + `AudioMixer`), no middleware.

## Folder structure

| File | What it owns |
|---|---|
| `GameSoundId` | Enum — the semantic identity of a sound moment (`BalloonPop`, `LevelUp`, …), decoupled from any message type. Append-only: entries are indexed by ordinal into the sound bank |
| `SoundHandle` | Readonly struct returned by `Play()` for loops — the only way to `Stop()` a specific playing voice. See *The `SoundHandle` generation guard* below |
| `ISoundPlayer` / `SfxService` | The orchestrator (Controller). `Play(id, position)` resolves the id to clips/tuning, throttles, caps voices, and plays; `Stop(handle)` ends a loop |
| `IMelodicContext` | Narrow interface `SfxService` also implements — `SetStreak(int)` feeds the current combo streak into the melodic pop system |
| `IAudioMixerRouter` / `AudioMixerRouter` | Seam between `SfxService` and the real `AudioMixer` — resolves a channel to an `AudioMixerGroup` and ducks a channel on pause. `AudioMixerRouter` is the registered implementation; `NullAudioMixerRouter` survives only as a test double |
| `Configuration/AudioMixerSettings`, `IAudioMixerSettings` | The mixer's own config asset — `AudioMixer` reference, one `AudioMixerGroup` and one exposed volume-param name per `SfxChannel`, and the duck volume in dB |
| `AudioChannelController` | Subscribes to `PausedMessage`/`ResumedMessage` (ducks the `Gameplay` channel) and `GameOverMessage` (stops all `Gameplay` voices) |
| `VoiceLimiter` | Per-id and global concurrent-voice accounting; steals the oldest same-id voice or the lowest-priority voice when a cap is hit |
| `SfxThrottleGate` | Wall-clock cooldown per id, plus burst coalescing (collapses a rapid-fire burst into a few voices instead of dozens) |
| `VariationPicker` | Picks a clip (no immediate repeat), then a pitch — plain random range, or one of the two melodic modes (see *Melodic pops*) |
| `VoicePlayback` / `PickContext` | Small readonly structs carrying a resolved play (clip/pitch/volume/pan) and the picker's inputs (streak, current semitone, burst index, pan) |
| `SoundIds` | One cached `Enum.GetValues(...).Length` — sizes every per-id array the helpers above key by `(int)GameSoundId` |
| `AudioPoolKeys` | The `PoolManager` key string for the voice pool |
| `SfxVoicePoolBootstrap` | `IStartable` — registers and pre-warms the `AudioSourceVoice` pool before any router can play a sound |
| `View/AudioSourceVoice` | **View** — the only `MonoBehaviour` in this feature, and the only type that touches `AudioSource` directly. Wraps one `AudioSource`; pooled; schedules its own real-time return |
| `Routing/CombatSoundRouter` | Hits, shots, reload, cruise loop, doomed warning, pierce, shield gained/lost |
| `Routing/ProgressionSoundRouter` | Streak, score chime, level-up (+ glow, dismiss), level transition, board clear, game-over (+ dismiss) |
| `Routing/ItemSoundRouter` | Per-item activation, overflow heart, spawn-blocked thud |
| `Routing/DangerSoundRouter` | Starts/stops the `DangerWarn` loop as `IDangerLevel` crosses `DangerLevelThreshold` |
| `Routing/WindSoundRouter` | Drives the `WindLoop` volume from cruise speed (TotalCruiseTaps) via `SetVolumeFactor` |
| `Configuration/GameSoundId`, `SfxChannel`, `SfxEntry`, `ISoundBankConfiguration`, `SoundBankConfiguration` | The data side — see *Configuration* below |

**Namespace:** `BalloonParty.Audio` (`.Routing`, `.View`, `.Configuration` for their folders).

## The pipeline

Nothing publishes a new message for audio. Every sound rides an event the game already
publishes for other reasons:

```
gameplay event (MessagePipe) → Router → ISoundPlayer.Play(id, position) → SfxService
    → throttle gate (cooldown/burst) → voice limiter (cap/priority) → variation picker
    → PoolManager.Get<AudioSourceVoice>() → voice.Play(clip, pitch, volume, pan)
```

A router (`CombatSoundRouter`, `ProgressionSoundRouter`, `ItemSoundRouter`) is a thin
translator: it subscribes to one or more existing `MessagePipe` messages and turns each into
a `(GameSoundId, Vector3? worldPosition)` call on `ISoundPlayer`. It never picks a clip,
computes a volume, or throttles anything — all of that is `SfxService`'s job. When a voice
finishes (or its throttle/cap check fails), `SfxService` returns it to the pool; nothing
downstream needs to know a sound was skipped.

## MVC split

Every class in this folder except `AudioSourceVoice` is plain C# — no `MonoBehaviour`, no
`transform`. `AudioSourceVoice` (`View/`) is the single **View**: a pooled `MonoBehaviour`
wrapping one `AudioSource`. `AudioMixerRouter` is a plain-C# adapter that also touches
`UnityEngine.Audio` (mixer asset references, `SetFloat`) — an accepted ports-and-adapters
carve-out, not a View, since it has no `MonoBehaviour`/`transform` and exists purely to hide
the mixer asset behind `IAudioMixerRouter`. Routers and `SfxService` are Controllers;
`SfxEntry` / `SoundBankConfiguration` / `AudioMixerSettings` are the config-flavored Model.

## Adding a new sound

This is deliberately a small, closed set of steps — no new types, no DI changes, no producer
edits:

1. Add a value to `GameSoundId` (append only — never reorder or insert, the sound bank
   indexes entries by ordinal).
2. In the relevant router (or a new one), subscribe to the message that should trigger it and
   call `_player.Play(GameSoundId.YourNewSound, position)`.
3. In the editor, open the `SoundBankConfiguration` asset and author the matching `SfxEntry`
   slot (clips, pitch/volume range, cooldown, cap, priority, channel).

An unauthored `GameSoundId` (empty clip array) is a silent no-op — `TryGet` returns `false`
and `Play` returns `SoundHandle.None` — so shipping the enum ahead of the art asset never
breaks anything.

### Volume envelope & stop routing

`SfxEntry` carries a small envelope: `FadeInSeconds` ramps volume 0→target at play start (one-shots
and loops), and `FadeOutSeconds` ramps it target→0 when the sound is *stopped* — via `Stop(handle)`
or another entry's stop routing. Both are DOTween fades on the voice's `AudioSource`, unscaled, and
0 means instant. A faded stop returns the voice through the normal completion path once the fade
finishes; scope resets (`ResetRun`/`Dispose`) and channel stops still cut immediately.

`StopsOnPlay` lets one entry silence others: when it plays, `SfxService` fades out any active voice
whose `GameSoundId` is in the list (each per *its own* `FadeOutSeconds`). Use it for a resolve cue
that should cut a still-playing loop — e.g. a warning loop silenced once the danger passes.

## Melodic pops (streak-driven scale)

The pop sound for an ordinary balloon is not one fixed clip — it climbs a musical scale as
your streak of same-color pops grows. Consecutive pops step up a **pentatonic** scale (a
scale with no adjacent semitones, so any order of notes still sounds pleasant, never
clashing), so a hot streak sounds like a rising musical phrase instead of the same click
repeated. Breaking the streak resets the phrase back to the root note.

The flip side is deliberate: a *bad* outcome — deflecting off a tough balloon, or the shot
hitting a wall — plays a distinct, intentionally sour note (a semitone rub against the
current pop pitch) instead of a generic buzz. So the two failure sounds are audibly
different from each other, and different from every pop, and you register "that went wrong"
before you've even seen why.

Mechanically: `SfxEntry.MelodicMode` selects between `None` (plain random pitch — most
sounds), `ScaleWalkUp` (the simple-balloon pop; a net-climbing yoyo — `VariationPicker` maps
the current streak to a scale degree read from `SoundBankConfiguration.MelodicScale`/
`MelodicRootSemitone`, each cycle rising one scale octave then dipping back the entry's own
`MelodicSkipSteps`, so the pitch trends up without a jarring reset, ceilinged at the entry's
own `MelodicMaxOctaves`), `ScaleWalkDown` (the same yoyo mirrored below the root — dips down
first, then works up), and `Tension` (deflect/wall-hit; offsets `SfxEntry.TensionSemitones`
against whatever semitone the pop system is currently on). `MelodicMaxOctaves` and
`MelodicSkipSteps` are per-`SfxEntry` fields, so each `ScaleWalkUp`/`ScaleWalkDown` sound
tunes its own climb independently. `ProgressionSoundRouter` feeds the pop streak in via
`IMelodicContext.SetStreak` on every `StreakChangedMessage`; `SfxService` remembers the last
melodic semitone so a `Tension` entry can react against it.

`WallHit` carries a melodic descent tied to shields left. `WallHitMessage` includes the
shields-remaining count, and `CombatSoundRouter` plays the cue as `ScaleWalkDown` at
`depth = max(0, ShieldToneThreshold - shieldsRemaining)` — the threshold lives on the flight
config (`IProjectileFlightConfig.ShieldToneThreshold`, the "shields config", default 5). So
while shields are at or above the threshold the tone stays on the root, and each shield below
steps it one degree down. Because the depth is a pure function of the live count, regaining
shields lifts the tone back toward the root on the next hit — no counter, nothing to reset on
reload. `ShieldLost` and `ShieldGained` play plain. A play with an explicit `melodicStreak`
never updates the ambient pop key, so it can't disturb the `Tension` cues. **The melodic pop
entries ship dormant** — see *Deferred*.

## Channels and duck-on-pause

Every `SfxEntry` names an `SfxChannel`: `Gameplay` (pops, shots, items — most sounds),
`UI` (button taps/confirms), or `Stinger` (level-up fanfare, game-over sting). `SfxService`
asks `IAudioMixerRouter.GroupFor(channel)` for the `AudioMixerGroup` to route a voice's
output to, so mixing/ducking is entirely the mixer router's decision — `SfxService` itself
never inspects pause state.

`AudioChannelController` is the only place that reacts to pause: on `PausedMessage` it ducks
the `Gameplay` channel, on `ResumedMessage` it un-ducks it, and on `GameOverMessage` it stops
every currently-playing `Gameplay` voice outright (`SfxService.StopChannel`). `UI` and
`Stinger` are left alone in both cases, so a level-up fanfare or a button tap is never cut
off by the freeze that silences pop spam.

`IAudioMixerRouter`'s registered implementation is `AudioMixerRouter`, a plain-C# adapter
over an `AudioMixer` asset described by `AudioMixerSettings`: `GroupFor` looks up the
group assigned to that channel, `SetChannelDucked` sets the channel's exposed volume
param to `AudioMixerSettings.DuckVolumeDb` (or back to 0 dB). Until the `AudioMixerSettings`
asset is authored and assigned, it degrades to exactly the old no-op behavior — every
channel resolves to the mixer's default output group and ducking does nothing — so nothing
breaks while the asset is missing. See *Configuration* for what to author to turn ducking on.

## Voice limiting, throttling, and coalescing

Three independent guards keep a burst of simultaneous pops from either sounding like a wall
of noise or exceeding the platform's real concurrent-voice budget:

- **`SfxThrottleGate`** — a wall-clock cooldown per id (never frame-based: a frame-based
  window would fire twice as often at 120 Hz as at 60 Hz). Within a short coalescing window
  it also counts a "burst index" instead of hard-dropping every extra request.
- **`VariationPicker`** — spends that burst index as a pitch spread and a volume falloff, so
  a burst reads as a quick pitched "chord" rather than N identical clicks.
- **`VoiceLimiter`** — enforces a per-id cap (e.g. only 3–4 concurrent pops) and a global cap
  (`SoundBankConfiguration.GlobalVoiceCap`, comfortably under a phone's real-voice budget).
  Over cap, it steals the oldest same-id voice, or — if every slot belongs to a different id —
  the lowest-*priority* voice, so `LevelUp`/`GameOver` (high priority) can never be starved by
  pop spam, and an equal-or-lower-priority newcomer is dropped rather than stealing a peer.

## The `SoundHandle` generation guard

`SoundHandle` is the only way to stop a specific loop (currently `CruiseLoopStart`). It
carries a voice-slot index *and* a generation counter. Every time `SfxService` (re)plays a
slot — including when `VoiceLimiter` steals it out from under an existing sound — the slot's
generation is bumped and the new `SoundHandle` captures the bumped value. `Stop(handle)`
compares the handle's generation against the slot's *current* generation and no-ops if they
don't match. This matters because a caller can legitimately hold a stale handle: if the
cruise loop's slot gets stolen by a higher-priority sound before `Stop` is ever called, the
old handle must not tear down whatever new sound is now occupying that slot. Without the
guard, a late `Stop` would silently kill an unrelated voice.

`GameSoundId.CruiseLoopStop` is **reserved, not a bug** — it is not currently played anywhere.
The cruise loop ends by calling `ISoundPlayer.Stop(handle)` on the `CruiseLoopStart` voice,
not by playing a separate "stop" cue; the enum value exists so a distinct stop *sound*
(rather than an abrupt cutoff) can be authored later without renumbering the bank.

## Configuration (in-editor authoring requirements)

None of this feature makes a sound until it is wired in the Unity Editor:

- **`SoundBankConfiguration` asset** (`Configuration/Sound Bank Configuration` menu) — one
  `SfxEntry` per `GameSoundId` (each with its own `MelodicMaxOctaves`/`MelodicSkipSteps` when
  its mode is `ScaleWalkUp`/`ScaleWalkDown`), plus the shared `MelodicScale`/
  `MelodicRootSemitone` (the key) and `GlobalVoiceCap`. `OnValidate` auto-resizes the entry
  array when a new `GameSoundId` is appended, but clips must be dragged in by hand per entry.
- **The `SfxVoice` prefab** — a `GameObject` with `AudioSourceVoice` + a plain `AudioSource`.
  Recommended `AudioSource` settings: **Play On Awake off**, **Loop off** (both are set by
  code on every `Play()`), spatial blend 0 (2D — `AudioSourceVoice` also forces this at
  runtime).
- **`AudioMixerSettings` asset** (`Configuration/Audio Mixer Settings` menu) — the `AudioMixer`
  reference, one `AudioMixerGroup` per `SfxChannel` (`Gameplay`/`UI`/`Stinger`), the matching
  exposed volume param name per channel (must match the name exposed on the `AudioMixer`
  itself), and `DuckVolumeDb` (default -12). `OnValidate` auto-resizes the per-channel arrays
  when a new `SfxChannel` is appended.
- **Scene wiring** — `GameLifetimeScope` needs the `SoundBankConfiguration` asset, the
  `SfxVoice` prefab, and the `AudioMixerSettings` asset dragged into its
  `_soundBank`/`_sfxVoicePrefab`/`_audioMixerSettings` fields. Any of the three left
  unassigned degrades gracefully (`RegisterAudio` logs a warning and skips registration if
  the voice prefab is missing; a missing bank falls back to an empty in-memory one; a missing
  mixer settings asset falls back to master output with ducking as a no-op), but no sound
  plays until the prefab and bank are set, and ducking stays inert until the mixer asset is.
- **Project Settings → Audio** — set **Max Real Voices** deliberately, comfortably above
  `GlobalVoiceCap` (Android budgets are tight, ~32 real voices); DSP buffer size should bias
  toward **Good/Best Latency** on target devices.
- **Clip import settings** — short SFX: **ADPCM + Decompress On Load** (near-zero decode
  cost, no per-play hitch). Very short stingers: PCM. Long music/ambience (future): Vorbis +
  Streaming. Downsample to ~22 kHz where it stays transparent. Keep clips out of
  `Resources/`.

## Deferred / not yet live

- **The `AudioMixer` asset itself is not yet authored.** The router and its config type
  (`AudioMixerRouter`/`AudioMixerSettings`) are shipped and registered — routing and ducking
  work end-to-end once someone builds the `AudioMixer` asset (Gameplay/UI/Stinger groups,
  matching exposed volume params) and assigns it and an `AudioMixerSettings` asset to
  `GameLifetimeScope`. Until then it degrades to master output with a no-op duck; no code
  change is needed to activate it, only in-editor authoring. A smooth snapshot-transition
  duck (instead of the instant `SetFloat` shipped now) is a possible later refinement behind
  the same `IAudioMixerRouter` seam, not a commitment.
- **Melodic pops are dormant.** The `ScaleWalkUp`/`ScaleWalkDown`/`Tension` machinery is code-complete and
  tested, but ships live only once a sound designer authors `SfxEntry`s with those modes set
  and the scale/tension semitones tuned by ear — until then every pop plays with plain random
  pitch.
- **Coalesce window/burst cap are `const`s** on `GameScopeRegistration`
  (`CoalesceWindowSeconds`, `MaxBurstPerWindow`) rather than fields on the sound bank SO —
  they were frozen at Step-1 authoring time; migrating them onto the SO is a Phase-2 task.
- **Automated SFX fetching — SHIPPED (editor-only).** On the `SoundBankConfiguration`
  inspector (custom editor in `Audio/Editor/`, never ships), each sound with a fetch prompt and
  no clip yet grows an inline **"Fetch clips (Freesound)"** foldout: fetch candidates, audition
  them in-editor (▶, via `AudioUtil`) or open on the site, and Accept to import + assign. Source
  is Freesound filtered to CC0 + CC-BY; CC-BY attribution is recorded to
  `Resources/AudioAttributions.json`. Needs a free Freesound token (`FREESOUND_API_TOKEN` env
  var, or paste it into the inspector — stored per-machine, never committed). Before shipping any
  fetched CC-BY audio: clear Freesound's API TOS and confirm a credits screen shows the recorded
  TASL attribution.
- **Full spatial 3D, a Launcher-scene voice, and a dedicated music/ambience bus** are all
  out of scope for now — see `Assets/Source/Plans/PLAN-Audio.md` for the fuller reasoning.

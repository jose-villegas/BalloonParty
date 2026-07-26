@page plan_web_demo_hosting Web Demo Hosting

# Web Demo Hosting

Put a playable build behind a link, so showing the game to someone costs them a click
instead of an APK sideload. Attempted end to end on 2026-07-26, reached the point of
being live, then rolled back over an unresolved runtime crash. This page keeps the
recipe and the blocker so the attempt does not have to be re-derived.

**Status: rolled back, resumable.** The tooling was deleted; the hosting change it
depended on was kept (see *What survived*). Nothing here is built.

---

## Why keep it

Sideloading is the only alternative without a Google Play account, and a stranger being
asked to enable "install from unknown sources" for an unsigned APK is a hard sell — the
scary prompts are not removable from outside a store. A URL has no such problem, which
makes the web build the honest demo channel even though mobile is the shipping platform.

## What survived

`.github/workflows/doxygen.yml` was moved off `actions/deploy-pages` onto
`peaceiris/actions-gh-pages@v4`, which **pushes the `gh-pages` branch**. This is
committed and live. It was done because **a repo serves only one Pages site**, so the
docs and a game could not each own an Actions-artifact deployment.

Consequences to remember:

- **Pages source is set to branch `gh-pages` / `(root)`**, not "GitHub Actions". If the
  docs site ever 404s, check that setting first.
- The docs publish no longer passes `keep_files`, so it replaces the branch contents
  outright. Anything else living on that branch gets pruned. Re-adding a second occupant
  means re-adding `keep_files: true` to the docs job *and* giving the new content its own
  `destination_dir`.
- `gh-pages` history permanently carries one 29 MB player build from the attempt. Only
  squashing the branch reclaims it.

## The recipe, if resumed

Build locally, deploy by script. **Do not** reach for GameCI: it needs a `UNITY_LICENSE`
secret (see the CI notes in Claude's memory for the `.alf` → `.ulf` activation path), and
José's call was explicitly to build by hand instead. Actions cannot see a local build
folder anyway, so once bytes are being pushed, pushing them straight to `gh-pages` *is*
the deploy — a workflow adds latency and no logic.

An editor entry point (`Tools/BalloonParty/Build WebGL Player`, plus a batchmode
`-executeMethod` twin) must set these, because the defaults do not work on Pages:

| Setting | Value | Why |
|---|---|---|
| `compressionFormat` | `Gzip` | Brotli is the project default. |
| `decompressionFallback` | `true` | **Pages cannot send `Content-Encoding`.** Without the fallback the loader never inflates the payload and the page dies on a black screen. Files come out named `.unityweb` when it is on — that is the confirmation it took. |
| `dataCaching` | `true` | Cheap; the browser Cache API will still refuse payloads over its quota. |
| Graphics APIs | `[WebGPU, OpenGLES3]` | WebGPU has compute, which WebGL2 lacks; see *The blocker*. Both backends ship, which inflates the build. |
| Exception support | `ExplicitlyThrownExceptionsOnly` shipping, `FullWithStacktrace` + development build for diagnosis | Without the latter, wasm frames read `$func107655` and nothing is debuggable. |

These are all WebGL-scoped, so they do not affect mobile builds and can simply live in
ProjectSettings — set them in the editor, not by hand-editing the asset.

The deploy step: fetch `origin/gh-pages` into a `mktemp` worktree **outside the project
tree** (or Unity will import the site), replace the target subdirectory wholesale rather
than syncing it (`rsync`'s size+mtime quick check can skip a same-size file — this bit us),
`touch .nojekyll`, commit, push, remove the worktree. Guard on `index.html` existing, and
no-op cleanly when nothing changed.

Nothing else needs editor setup. WebGL's default quality level is `High` against mobile's
`Medium`, but all six levels share one URP asset and differ only in `lodBias`,
`particleRaycastBudget`, and reflection probes — irrelevant for a 2D game.

## The blocker

The player reached the site and booted: `Unity WebGPU: Version WebGPU 1.0`, physics and
input initialised, player data loaded. It then died on the **first main-loop frame** with
`RuntimeError: null function` → `Halting program.` Never attributed. Two hypotheses:

1. **`SpeckField`.** `DustSpeckField` is live in `Game.unity`, so its `Start()` runs on
   exactly the frame that crashes, and it needs the vertex stage to read a `ComputeBuffer`
   (`SystemInfo.maxComputeBufferInputsVertex`) — the least-supported corner of any
   backend, on a backend Unity labels experimental. Choosing WebGPU is what made this path
   reachable at all; under WebGL2 it self-disables.
2. **IL2CPP managed stripping** versus VContainer/MessagePipe reflection. There is no
   `link.xml` in `Assets/`, no `[Preserve]` anywhere, and `managedStrippingLevel` has
   per-platform entries for Android/PS4/Switch but **none for WebGL**, so it takes the
   default. A stripped method or a never-AOT-instantiated generic produces this exact
   error shape.

**One build separates them:** exception support `FullWithStacktrace`, development build,
and `DustSpeckField` disabled in `Game.unity`. Boots clean → hypothesis 1, and the fix is
a WebGPU-aware guard or the texture-based fallback that `SpeckField.cs`'s own comment
suggests. Still crashes → there is now a named managed stack pointing at hypothesis 2.

Benign log noise to ignore while debugging: `Content-Encoding: gzip` advice (expected with
the decompression fallback), `AudioContext was not allowed to start` (autoplay policy,
clears on first click), `Cache.put() encountered a network error` (payload over the browser
cache quota), and the `JS_FileSystem_Sync` deprecation notice. Also note `Screen.resolutions`
returns **empty** on WebGL — `FrameRateSettings` handles it, other indexing code may not.

## Residue from the attempt

- Commit `4d517d61` persisted `webGLCompressionFormat: 1`, `webGLDecompressionFallback: 1`
  and a `WebGLSupport` graphics-API entry into ProjectSettings. Harmless and WebGL-scoped,
  but dead while this plan is dormant. Clear them in the editor if it bothers you; the same
  commit also carries an unrelated iOS texture-format entry, so do not revert the file.

## If web stays dead

Ranked alternatives that need no Google Play account:

1. **itch.io** — hosts HTML5 *and* APK, no Google account, and carries enough brand
   recognition that a download does not read as a risk. Pages can stay docs-only.
2. **Direct APK download** — works, but Play Protect and the unknown-sources flow will
   frighten non-technical players. Publishing the APK's SHA-256 and signing with a stable
   key helps a technical audience and nobody else.
3. **Play Console internal testing** — the actually-friction-free mobile answer, and the
   one option that requires the Google account this plan exists to avoid.

## Decision log

- **2026-07-26** — Attempted, went live at `/play`, crashed on the first frame, rolled
  back. Tooling deleted (`webgl.yml`, `Assets/Source/Editor/CI/WebGLBuilder.cs`,
  `Tools/deploy_webgl.sh`); the `gh-pages` branch hosting change kept. Shipping WebGPU
  was a deliberate call to preserve `SpeckField`, accepting experimental-backend risk.

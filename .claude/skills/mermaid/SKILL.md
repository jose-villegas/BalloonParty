---
name: mermaid
description: Write Mermaid diagrams that actually parse. Load before authoring or editing any ```mermaid block in this repo — plan documents, feature READMEs, architecture pages. Covers the syntax traps that silently break rendering on GitHub and in the Doxygen docs site, plus which diagram type to reach for.
---

# Mermaid in BalloonParty

Diagrams live in `Assets/Source/**/README.md`, `Assets/Source/Plans/PLAN-*.md`, and
`Assets/Source/ARCHITECTURE.md`. They render in two places, and **both must work**:

- **GitHub** — renders ```mermaid fences natively in the repo view.
- **The Doxygen docs site** (`https://jose-villegas.github.io/BalloonParty/`) — built by
  `Tools/generate-docs.sh` and the `doxygen.yml` workflow on every push to `main`.

There is no Node toolchain in this repo, so **`mmdc` / `mermaid-cli` cannot be run to
validate**. A broken diagram is not caught by CI, the style audit, or the pre-commit hook —
it fails silently at render time and the page shows a parse error instead of a diagram.
That means the syntax rules below are the only line of defence. Follow them literally.

---

## The five traps that actually bite

These are the ones that have broken diagrams in this repo. Every one of them *looks* fine
in the source.

### 1. Semicolons terminate statements — never put one in a label

`;` is a statement separator across every Mermaid diagram type. A semicolon inside message
text ends the message and the parser then chokes on the remainder.

```
%% BROKEN — parse error: expects an arrow, gets a newline
Svc->>Lvl: Absorb into Run; Reset(); resume clock
Gate-->>Svc: ok; picker yields clip + pitch
```

```
%% CORRECT — use commas or "and"
Svc->>Lvl: absorb into Run, reset, resume clock
Gate-->>Svc: ok, picker yields clip + pitch
```

Applies to sequence messages, notes, edge labels, and state transition labels alike.

### 2. A composite state cannot also carry a `:` description

The `state X : description` form declares a **simple** state. You cannot then reopen `X`
as a composite block — the parser reports `Expecting 'AS', got 'COMPOSIT_STATE'`.

```
%% BROKEN
state Run : "Run (generation + retry provenance)"
state Run {
    [*] --> Level
}
```

```
%% CORRECT — description and body in one declaration
state "Run (generation + retry provenance)" as Run {
    [*] --> Level
}
```

```
%% ALSO CORRECT, and preferred here — plain id, detail in the prose/table beside it
state Run {
    [*] --> Level
}
```

Prefer the third form. Plan documents put a table under every diagram anyway, so a long
label inside the box is duplication that can only rot.

### 3. `\n` in a single-line note is not a line break

Use the block form. It is also easier to read in the source.

```
%% BROKEN — renders the literal characters, or fails
note right of Ended : accumulation OFF\ntrails must not leak
```

```
%% CORRECT
note right of Ended
    Accumulation OFF. Post-game-over straggler trails
    must NOT leak into the next run.
end note
```

### 4. `<` and `>` in a flowchart label need quotes

Mermaid treats `<` as the start of an HTML tag — deliberate in the case of `<br/>`, fatal
for a comparison. Quote the whole label.

```
%% BROKEN
P{arbitrated <120 +<br/>missed vsync, frame <8.3ms?}
```

```
%% CORRECT
P{"arbitrated <120 +<br/>missed vsync, frame <8.3ms?"}
```

Quoting is always safe — reach for it whenever a label contains anything but words,
spaces, and simple punctuation.

### 5. `end` is a reserved word — never use it as a node id

```
%% BROKEN — silently corrupts the flowchart
flowchart TD
    start --> end
```

Use `Done`, `Finish`, `Terminal`. Same for `subgraph`/`graph`/`class` as bare ids.

---

## Safe-by-default habits

- **Keep labels to words, commas, and periods.** Every symbol you add is a gamble you
  cannot test. `->`, `|`, `{`, `;`, `<`, `#`, backticks and stray quotes in free text are
  the usual culprits. Prefer "triggers", "yields", "then" over arrow glyphs inside a label
  that is *already* drawn as an arrow.
- **Prefer plain node ids** (`Svc`, `Lvl`, `Bus`) with the descriptive text in the bracket
  label, not in the id.
- **Don't put parentheses in `participant ... as` aliases.** `participant Lvl as Level
  MetricScope` over `participant Lvl as MetricScope (Level)`.
- **Method signatures in `classDiagram` are fine** — `+Write(in TelemetryEnvelope)`,
  `+FlushAsync(CancellationToken) UniTask`, and a trailing `*` for abstract all parse.
  Annotations (`<<interface>>`, `<<abstract>>`, `<<readonly struct>>`) are fine too.
- **Unicode is fine in labels** — em dashes, arrows (`→`), and accented characters render.
  It is the *structural* ASCII characters above that break things.
- **Re-read the diagram as the parser would** before committing: scan every line after a
  `:` or inside a `|...|` for the five traps.

---

## Choosing the diagram type

| Use | When | Notes |
|---|---|---|
| `classDiagram` | type hierarchies, interface/impl relationships, responsibility surfaces | `..\|>` realization, `<\|--` inheritance, `-->` association with a `: label` |
| `sequenceDiagram` | message flow over time, who calls whom in what order | the right choice for anything involving MessagePipe ordering |
| `stateDiagram-v2` | a feature with explicit modes and transitions | always `-v2`, never bare `stateDiagram` |
| `flowchart TD` / `graph TD` | dependency graphs, decision trees, registration wiring | `graph` is the legacy keyword; both work, don't churn existing ones |

Use ASCII art only when Mermaid genuinely cannot express the thing — spatial/grid layouts,
hex geometry. Prefer Mermaid for anything relational.

---

## Before you commit a diagram

1. Re-scan each label for the five traps above.
2. Confirm nesting: every `state X {`, `subgraph`, `alt`/`loop`/`opt` has its closing
   `}` or `end`.
3. Confirm the fence is exactly ```` ```mermaid ```` and closes with ```` ``` ````.
4. Say plainly in your summary that the diagram is **unvalidated** — no renderer exists in
   this environment. Do not claim a diagram "renders correctly"; you cannot know that.
5. If a diagram is load-bearing for a plan, ask the author to eyeball it on GitHub after
   the push.

## New `.md` files

Every new `.md` under `Assets/` needs a Unity `.meta` file — mirror an existing sibling's
format. Plans additionally need a Doxygen `@page` header and an `@subpage` entry in
`Assets/Source/Plans/Plans.md`.

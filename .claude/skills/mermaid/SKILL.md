---
name: mermaid
description: Author and validate Mermaid diagrams in this repo. Load before writing or editing any ```mermaid block — plan documents, feature READMEs, architecture pages. Carries the official per-diagram-type syntax references, the traps that have actually broken diagrams here, and the mermaid-cli validation command that must pass before you commit.
---

# Mermaid in BalloonParty

Diagrams live in `Assets/Source/**/README.md`, `Assets/Source/Plans/PLAN-*.md`, and
`Assets/Source/ARCHITECTURE.md`. They render in two places and **both must work**:

- **GitHub** — renders ```mermaid fences natively in the repo view.
- **The Doxygen docs site** (`https://jose-villegas.github.io/BalloonParty/`) — built by
  `Tools/generate-docs.sh` and the `doxygen.yml` workflow on every push to `main`.

Neither target fails loudly. A broken diagram shows a parse error where the picture should
be, and nothing else catches it: the style audit is C#-only, and the pre-commit hook does
not look at Markdown. **The validator below is the only gate.**

---

## Validate — this is not optional

```bash
node Tools/validate-mermaid.mjs
```

Renders every ```mermaid block in `Assets/`, `README.md`, and `.claude/agents/` through
mermaid-cli and reports `file:line` for each failure. Exits non-zero if any block fails.
Run it after **any** edit to a diagram, and paste the result rather than asserting the
diagram is fine.

```bash
node Tools/validate-mermaid.mjs Assets/Source/Plans/PLAN-Audio.md   # one file or dir
node Tools/validate-mermaid.mjs --verbose                            # also list passes
```

Requires `@mermaid-js/mermaid-cli` (`npm install -g @mermaid-js/mermaid-cli`) and a Chrome
that Puppeteer can find. If `mmdc` is missing the script reports it per block — install it
rather than skipping validation.

The skill's own `references/` are excluded by default: they are upstream's documentation,
not our prose. Pass the path explicitly if you want them checked.

---

## The traps that actually bite

Every one of these has broken a diagram in this repo. They all *look* fine in the source.

### 1. Semicolons terminate statements — never put one in a label

`;` is a statement separator in every diagram type. A semicolon inside message text ends
the statement and the parser chokes on the remainder.

```
%% BROKEN — "Expecting SOLID_ARROW..., got NEWLINE"
Svc->>Lvl: Absorb into Run; Reset(); resume clock
```
```
%% CORRECT
Svc->>Lvl: absorb into Run, reset, resume clock
```

### 2. A composite state cannot also carry a `:` description

`state X : description` declares a **simple** state; reopening `X` as a composite is
illegal — `Expecting 'AS', got 'COMPOSIT_STATE'`.

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
%% ALSO CORRECT, and usually better — plain id, detail in the table beside the diagram
state Run {
    [*] --> Level
}
```

Prefer the third form in plans: they put a table under every diagram anyway, so a long
label inside the box is duplication that can only rot.

### 3. `\n` in a single-line note is not a line break

```
%% BROKEN
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

Mermaid reads `<` as an HTML tag open — deliberate for `<br/>`, fatal for a comparison.

```
%% BROKEN
P{arbitrated <120 +<br/>missed vsync, frame <8.3ms?}
```
```
%% CORRECT
P{"arbitrated <120 +<br/>missed vsync, frame <8.3ms?"}
```

### 5. A dotted-link label cannot contain a dot

`-.text.->` delimits its label with dots, so a `.` inside the text closes it early —
`Lexical error ... Unrecognized text`.

```
%% BROKEN
A1 -.BackgroundFieldService.Tick.-> G4
```
```
%% CORRECT — quote it
A1 -."BackgroundFieldService.Tick".-> G4
```

Type names with dots are everywhere in this codebase, so this one recurs.

### 6. `end` is a reserved word — never a node id

Use `Done`, `Finish`, `Terminal`. Same for bare `graph`, `subgraph`, `class`.

---

## Safe-by-default habits

- **Quote any label containing punctuation.** Quoting is always legal and costs nothing.
  Reach for it the moment a label holds anything but words, spaces, and commas.
- **Keep free text plain.** `->`, `|`, `{`, `;`, `<`, `#` and stray quotes inside a label
  are gambles. Prefer "triggers", "yields", "then" over arrow glyphs inside a label that
  is *already* drawn as an arrow.
- **Plain node ids** (`Svc`, `Lvl`, `Bus`) with descriptive text in the bracket label.
- **No parentheses in `participant ... as` aliases** — `participant Lvl as Level
  MetricScope`, not `participant Lvl as MetricScope (Level)`.
- **`classDiagram` signatures are fine** — `+Write(in TelemetryEnvelope)`,
  `+FlushAsync(CancellationToken) UniTask`, trailing `*` for abstract, and annotations
  (`<<interface>>`, `<<abstract>>`, `<<readonly struct>>`) all parse.
- **Unicode is fine** — em dashes, `→`, accents all render. It is the *structural* ASCII
  characters above that break things.

---

## Choosing a diagram type

Read the matching reference before writing anything non-trivial. These are the official
Mermaid docs, vendored so they work offline.

| Type | Reference | Use for |
| --- | --- | --- |
| Flowchart | [flowchart.md](references/flowchart.md) | dependency graphs, decision trees, registration wiring |
| Sequence | [sequenceDiagram.md](references/sequenceDiagram.md) | message flow over time — the right pick for MessagePipe ordering |
| Class | [classDiagram.md](references/classDiagram.md) | type hierarchies, interface/impl, responsibility surfaces |
| State | [stateDiagram.md](references/stateDiagram.md) | features with explicit modes; always `stateDiagram-v2` |
| ER | [entityRelationshipDiagram.md](references/entityRelationshipDiagram.md) | data/entity relationships |
| C4 | [c4.md](references/c4.md) | system-context architecture |
| Architecture | [architecture.md](references/architecture.md) | service/component topology |
| Block | [block.md](references/block.md) | system components and modules |
| Gantt | [gantt.md](references/gantt.md) | schedules, phase plans |
| Timeline | [timeline.md](references/timeline.md) | milestones, history |
| Git graph | [gitgraph.md](references/gitgraph.md) | branch/merge topology |
| Mindmap | [mindmap.md](references/mindmap.md) | hierarchies, idea maps |
| Journey | [userJourney.md](references/userJourney.md) | player/user experience flow |
| Quadrant | [quadrantChart.md](references/quadrantChart.md) | four-quadrant trade-off analysis |
| Sankey | [sankey.md](references/sankey.md) | flow volumes, conversions |
| XY chart | [xyChart.md](references/xyChart.md) | line and bar charts |
| Pie | [pie.md](references/pie.md) | proportions |
| Others | `references/` | radar, treemap, kanban, packet, requirement, zenuml, venn, swimlanes, wardley, … |

Config and theming: [theming](references/config-theming.md) ·
[directives](references/config-directives.md) · [layouts](references/config-layouts.md) ·
[configuration](references/config-configuration.md) · [math](references/config-math.md).

Use ASCII art only when Mermaid genuinely cannot express the thing — spatial or grid
layouts, hex geometry. Prefer Mermaid for anything relational.

---

## Before you commit

1. Run `node Tools/validate-mermaid.mjs` and confirm zero failures.
2. Confirm nesting: every `state X {`, `subgraph`, `alt`/`loop`/`opt` has its `}` or `end`.
3. Confirm the fence is exactly ```` ```mermaid ```` and closes with ```` ``` ````.
4. New `.md` files under `Assets/` need a Unity `.meta` file (mirror a sibling). Plans also
   need a Doxygen `@page` header and an `@subpage` entry in `Assets/Source/Plans/Plans.md`.
5. A passing validation means it *parses*. It does not mean the layout reads well — for a
   load-bearing diagram, eyeball it on GitHub after the push.

---

## Attribution

`references/` is vendored from [WH-2099/mermaid-skill](https://github.com/WH-2099/mermaid-skill)
(MIT, see `references/LICENSE.upstream`), which syncs them from the official
`mermaid-js/mermaid` documentation. Refresh by re-copying that repo's
`.claude/skills/mermaid/references/`. The traps, the validation workflow, and the
repo-specific guidance above are ours — keep them when refreshing.

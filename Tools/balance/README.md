# Level balance

`level_balance.py` works out how many points each level asks so that a level takes as long as its target
says, and draws the charts and tables of [`Docs/level-balance.md`](../../Docs/level-balance.md). The target and
the reasoning are on that page; this one says how the tool is run.

```bash
python3 Tools/balance/level_balance.py collect "Logs/playtest/*from1-fit*"   # a sweep's runs, into the repository
python3 Tools/balance/level_balance.py fit                                   # the points each level asks
python3 Tools/balance/level_balance.py fit --apply                           # ... written into the asset too
python3 Tools/balance/level_balance.py charts                                # the page's charts
python3 Tools/balance/level_balance.py report                                # the page's tables, to paste
```

Needs matplotlib for `charts`. The sweeps themselves are run with `Tools/playtest/device_copies.py`, always
from level 1 and many runs to a player: a single run, or one started deep, says little.

| Step | Reads | Writes |
|---|---|---|
| `collect SESSIONS...` | Each session's `ticks.jsonl` (`Logs/playtest/`, git-ignored). Only runs that began at level 1 | `Docs/balance/data/runs-<curve>.csv`, one row per level per run: seconds, points, shots, hearts lost, retries. A run collected before is replaced |
| `fit` | Every data file (or `--from CURVE...`), and the asset's control points for levels 1 to 9 | `Docs/balance/fit.json`: the control points, the tail, and each level's target and points a second. `--apply` writes the control points and tail into `Assets/Configuration/LevelPacingConfiguration.asset` |
| `charts` | The data files, `fit.json`, the asset | `Docs/balance/*.png`, one chart to a file |
| `report` | The same | Markdown tables on the terminal |

## Which runs count for what

`<curve>` in a data file's name is a fingerprint of the points curve its runs played: the asset's control
points and tail, as they are now or as they were at `collect --asset COMMIT`.

- **Points a second** do not depend on the curve played, so the fit and the two scoring charts read every
  data file.
- **Level times** belong to one curve. The time chart, the hearts chart and the measured table read only the
  file whose fingerprint is the asset's present curve. After `fit --apply` changes the curve there is no such
  file until the next sweep, and `report` says so.

## The fit

- A level's target seconds are a logarithm of its number, through the two anchors at the top of the script
  (`ANCHORS`).
- A player's points a second at a level are the pooled points over the pooled seconds of every run there.
  From level 19 on they are read half from that number and half from the trend (an exponential fitted where
  at least eight runs gave a level), and past the last such level from the trend alone: deeper, only the
  fastest runs are left, and they would read too high.
- Each level from 10 to 80 asks for target seconds times the average player's points a second, rounded to 50
  and never under the level before. Levels 10 to 40 become control points; the tail's growth is what carries
  level 40's ask to level 80's.

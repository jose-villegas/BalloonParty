# UI/Tooltip

The on-screen tooltip: a short line of text that fades in over play, holds briefly, then fades out.
Anything in the game can show one by publishing a `TooltipMessage`; the view that publishes it owns
the wording.

## Contents

| File | What it does |
|---|---|
| `TooltipBanner` | `MonoBehaviour` view on the `Tooltip` prefab (in `GameCanvas`). Subscribes to `TooltipMessage` and shows its text: fades its `CanvasGroup` to `_visibleAlpha`, holds for `_holdDuration`, fades out. A new line replaces the one showing and restarts the timing; an empty one shows nothing. The tween runs on unscaled time, since lines land during slow-motion beats and frozen screens. It also words `BonusCardDealtMessage` with `_bonusCardFormat` and holds it until navigation is back in `Game`: the card is dealt during the retry's reset, while the game-over screen is still up. Injected via `RegisterComponentInHierarchy<TooltipBanner>` in `GameLifetimeScope` |
| `Shared/Messages/TooltipMessage` | The line to show. Registered as a MessagePipe broker in `GameLifetimeScope` |
| `Shared/Extensions/StringFormatExtensions` | `FormatOrRaw`: formats an editor-authored string, returning it as written when it's malformed, so a typo in a format never throws mid-flow |

## Who shows what

| Publisher | When | Text |
|---|---|---|
| `LevelUpPopUp` | A level-up passes in play (see `UI/LevelUp/README.md`) | `_inPlayTooltipFormat` on the popup, `Level {0}`; on a card level-up `_inPlayCardTooltipFormat`, `Level {0}: {1}` with the card taken |
| `TooltipBanner` itself | A retry bought with a rewarded ad resumes play with its bonus card (`BonusCardDealtMessage` from `Game/Upgrades/RunUpgrades`) | `_bonusCardFormat` on the banner, `Bonus card: {0}` |

## Wiring requirements

- `_group` and `_label` must be assigned; the prefab assigns the `Banner` child's `CanvasGroup` and
  its `Text (TMP)`.
- The `CanvasGroup` should not block raycasts: the tooltip sits over play and must never eat a tap.

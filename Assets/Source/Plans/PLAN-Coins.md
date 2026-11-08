@page plan_coins Coins and the Card Shop

# Coins and the Card Shop

> Working plan for a coin currency earned deep in a run and spent on the game-over screen for a card
> that helps the retry.

**Status:** Phase 1 built (the wallet, the drop and telemetry, see `Game/Coins/README.md`); phase 2
(tuning) next. Decided with the owner: a card bought with coins applies to the
retry at the level reached; coins are a saved wallet; they drop from pops from level 15 on; the
counter sits bottom left and appears only once coins exist. Claude draws the art in the existing UI
style (§7); the coin is drawn.

---

## 1. Why

The playtest bot (`Tools/playtest/`) shows where each skill starts losing hearts: low skill from
about level 4, average from 9, high from 12. Most runs that reach level 15 are in the stretch where
hearts go. Coins there reward getting that deep, and the shop turns a loss into a choice: spend what
the good runs earned to make the retry easier.

It is a second way to buy what the rewarded ad already buys (a bonus card for one attempt, see
`Game/Upgrades/README.md`), paid with play instead of an ad. It never buys score.

## 2. The loop

1. From `CoinsFromLevel` (15) on, each balloon popped has a `CoinDropChance` of dropping a coin.
2. The coin flies to the coin counter, like a score trail, and is added to the wallet when it lands.
3. The wallet is saved, so coins carry over between runs and app restarts.
4. On a loss, the game-over screen offers **Buy a card** next to Retry, priced at `CardPrice`
   coins.
5. Buying one shows a hand of three cards (as at a level-up), the player picks one, the coins are
   spent, and the retry starts with that card for this attempt only.
6. The retry opens with the card reveal (`PLAN-CardReveal.md`): a balloon pops and leaves a slip
   with the card written on it.

## 3. Rules

| Rule | Why |
|---|---|
| Coins drop only from level `CoinsFromLevel` (15) on | They reward reaching the stretch where hearts go; earlier levels stay about learning the board |
| Any pop can drop one, an item's blast included | "Per pop", as decided; big combos should feel rich |
| A coin counts only once it lands in the counter | Same as score, so a loss mid-flight can't credit coins that never arrived on screen. Lands even through the game-over pan-in |
| The wallet is saved on game over, on app pause and on quit, not on every coin | `PlayerPrefs.Save` is slow on mobile; `RunMeta` already saves this way |
| A bought card lasts one attempt: dropped when the level is won or on any reset | The same rule as the ad's bonus card, so spending never stacks power across levels |
| One bonus card per attempt: buying with coins and the ad bonus exclude each other | Keeps the retry's power bounded, and the screen simple |
| The coin card is **picked** from three; the ad's card stays random | Gives the coins a reason to exist next to the ad: you choose |
| Never an Extra Ball, never a maxed card | The same deck rules as the ad's bonus card (`Plans/PLAN-FastPaced.md` §8) |
| The price is the same every time | Easy to read. If the bot shows wallets piling up, a price that rises per purchase in a run is the lever (§9) |
| Coins are progression, not telemetry | Kept on the device on purpose. Coins earned and spent per level are *also* reported as metrics, which leave the device (see `Game/Telemetry/README.md`) |

## 4. Architecture

Follows the codebase's MVC split: models and controllers are plain C#, only views touch Unity.

| Piece | Where | Responsibility |
|---|---|---|
| `ICoinSettings` | `Shared/`, authored in `RunConfig` as `Coins` | `CoinsFromLevel` (15), `DropChance` (0.05); phase 4 adds `CardPrice` (25) and `CardsOffered` (3) |
| `ICoinBalance` / `ICoinWallet` / `CoinWallet` | `Game/Coins/` | `ICoinBalance` is the read-only `Balance` views read; `ICoinWallet` adds `Add(int)` and `TrySpend(int)`. Loads from and saves to `PlayerPrefs` through `ICoinStore` (key beside `RunMeta`'s); `CoinWalletSaver` saves at the save points, and a spend saves at once |
| `CoinDropController` | `Game/Coins/` | Subscribes `ActorHitMessage`; on a `Pop` at level ≥ `CoinsFromLevel`, rolls the drop (seeded from the level seed, so a retry of the same level drops the same way for the same pops) and publishes `CoinDroppedMessage(amount, worldPosition)`. `CoinCreditor` adds dropped coins to the wallet; phase 3 switches it to `CoinLandedMessage` |
| `CoinTrailService` + view | `Game/Coins/`, `UI/` | Flies each dropped coin to the counter (reusing the score-trail flight), publishes `CoinLandedMessage`, on which `CoinCreditor` adds 1. Pooled like score trails. When the run ends or resets, coins still flying are credited at once, never dropped: the score trails' cancel-on-reset would destroy them |
| `CoinCounterView` | `UI/HUD/` | Bottom left of the screen: the coin icon and `Balance`. Hidden until coins exist (see below); punches on each landing |
| `FailureOffers` | `Game/Run/` (extended) | `FailureOffer` gains `CanBuyCard` and `CardPrice`; new `TryCardRetryAsync(level, card, ct)` spends the coins and stages the retry with that card |
| `IUpgradeDraft` / `RunUpgrades` | `Game/Upgrades/` (extended) | A shop hand (`DealShopHand`, same draw rules as the bonus card, seeded by level and retries used) and staging a chosen bonus card instead of a random one |
| `CardShopView` | `UI/GameOver/` | The hand on the game-over screen, reusing `UpgradeCardView` slots, with the price, the balance and a disabled state when the wallet is short |

**When the counter shows.** Not before coins exist for this player, so a new player never sees an
empty wallet before the game has said what coins are. It appears the first time either holds: the
run reaches `CoinsFromLevel` (fading in with the level's start), or the wallet already holds coins
(a returning player sees it from level 1, since they can spend at any loss). After that it stays for
the session. It sits bottom left, clear of the cannon and of the heads-up lines, which keep to the
board.

Telemetry gains `coins_earned` and `coins_spent` per level. The playtest bot reports coins earned per
run and per level, so the drop chance and the price can be tuned from runs (§8).

## 5. Phases

| Phase | What | Needs art |
|---|---|---|
| 1 | `ICoinSettings`, `CoinWallet`, `CoinDropController` with coins credited on drop (no flight yet), telemetry and the bot report. EditMode tests for the wallet, the drop gate and the save points | No |
| 2 | Tune `CoinDropChance` and `CardPrice` with the bot (§8) | No |
| 3 | The coin flight, crediting on landing, and the bottom-left counter with its show rule | Drawn: the coin |
| 4 | The shop on the game-over screen: `TryCardRetryAsync`, the shop hand, `CardShopView` | Yes: card frame, card icons, price chip |
| 5 | Sounds for a coin landing and a purchase (`PLAN-Audio.md`) | Audio |

Each phase gets the reviewer pass before the next one starts.

## 6. Edge cases

- **Retry farming.** A retry replays the level reached, so a player can earn coins at level 16, lose,
  and earn them again on the retry. That is the loop working as meant: it costs play, and the coins
  buy only one attempt's card. Watch it in the bot's numbers.
- **Coins from the rainbow self-feed.** A shot that rainbow-converts balloons and pops them again
  (see the memory note on the rainbow multiplier) would also roll more coin drops. It is capped by
  the board, unlike the multiplier, but check the bot's high-skill coins per level for outliers.
- **Losing mid-flight.** Coins still in the air when the run ends land during the game-over pan-in,
  so the screen's balance is complete when it shows.
- **No card can be dealt.** When only Extra Ball and maxed cards are left, the shop hides, as the ad's
  bonus button does.

## 7. Assets

Claude draws them in the existing UI style: flat shapes, a white rim with a thin grey stroke (as
`Sprites/UI/shield.png`), soft gradients in the sun's palette, and a white silhouette beside each
for tinting and flashes (as `shield-fill.png`). Each is drawn by a script in `Tools/art/`, so it can be
redrawn at another size or colour. They live in `Assets/Sprites/UI/Coins/`, which the UI atlas packs
with the rest of `UI/`.

| Asset | Size | Used for | State |
|---|---|---|---|
| `coin.png`, `coin-fill.png` | 512×512 | The flying coin, the counter icon, the price chip | Drawn (`Tools/art/make_coin.py`): gold face, embossed ring, a stamped balloon. Reads at 32 px |
| Counter backing | Reused | Behind the counter | `Sprites/UI/background_counter.png`, as the other HUD counters |
| Card frame | 9-slice, about 360×500 | The shop's card slots, and the level-up pick screen when it returns | To draw (phase 4) |
| Card icons | 4 × 192×192 | Extra Ball, Faster Reload, Shield, More Items; shared with `PLAN-CardReveal.md` | To draw (phase 4) |
| Price chip | 9-slice, about 200×72 | The price under each card: the coin and a number | To draw (phase 4) |
| Buy button, short of coins | The Retry button | A greyed state | A tint of the existing button, no new art |

The heads-up for a coin (`+1 coin`) needs no art: it is handwritten like the others (`UI/HeadsUp/`).

## 8. Tuning targets

From the bot's runs: at levels 13 to 15, high skill pops about 180 to 330 balloons a level, and
average skill about 150 to 250. With `CoinDropChance` 0.05 that's about 8 to 16 coins a level, so
`CardPrice` 25 is two or three levels past 15. A player who reaches 17 can afford one card on their
next loss. Phase 2 checks this with runs and sets the shipped values.

## 9. Open questions

- Can a player buy more than one card for the same retry?
- Should the price rise with each purchase in a run, to stop a large wallet from buying every
  retry?
- Is level 15 right for average skill, which reached 14 in ten minutes in the last runs? Earning
  from 12 would let average skill take part, and keep it a reward for depth.
- Should coins also show in the main menu, so the wallet is visible between runs?

# UI/Coins

The coin counter, bottom left of the screen: a coin and the wallet's balance. Dropped coins fly
here from their pops and count when they land (`Game/Coins/README.md`).

It stays hidden until coins mean something to this player: the first time the run reaches the
level coins start dropping at, or from the start if the wallet already holds coins. Then it
stays for the session, so a retry never hides it again. Coins fly on real time, so they land during
the loss's slow pan-in and the balance is complete when the game-over screen settles. Behind that
screen the counter is blurred and dimmed with the rest of the HUD; showing the balance crisply there
is for the shop (`PLAN-Coins.md` phase 4).

## Contents

| File | What it does |
|---|---|
| `CoinUILifetimeScope` | VContainer child scope on the counter's root: binds the `CoinCounterLabel` to `ICoinBalance.Balance` and injects `CoinCounterView` |
| `CoinCounterLabel` | Marker subclass of `ReactiveCounterLabel`; a `RollingCounterDisplay` sibling rolls the digits |
| `CoinCounterView` | At start, gives `CoinTrailService` the coin icon as the trail target, along with the coin trail prefab. Shows or hides the counter (a `CanvasGroup`'s alpha) from `ICoinCounterVisibility`, fading in when it first turns on, and punches the icon on each `CoinLandedMessage` |

## Building it

`Tools/BalloonParty/Coins/Build Counter` (`Editor/CoinCounterBuilder`) builds
`Prefabs/Coins/UI/CoinsCounter.prefab` and places it in `GameCanvas.prefab`, beside the other
HUD. The backing and the number are copies of the shield counter's, so the HUD
counters match; the number sits right of the coin and reads left to right, since a balance runs to
several digits. The coin takes `Materials/UI/CoinUI.mat`, the HUD icons' shine sweep (as the shield's
`ShieldUI.mat`, offset in time so the two don't flash together). Running it again rebuilds the
counter and replaces the one in the canvas, so change the builder rather than the prefab.

The flying coin is `Prefabs/Coins/CoinTrail.prefab`, a `FlyingTrail` with the coin sprite and a
gold ribbon.

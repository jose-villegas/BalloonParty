# Game/CardReveal

Decides which won card is revealed, and when. A reveal is a balloon that pops over the board, and
two pieces of ribbon fly off it with the card written on them. This folder holds the plain C# side: the queue and
the pick of which baked pop plays. The view that draws it is `UI/CardReveal/CardRevealView`, which
attaches itself as the **stage** when the settings hold at least one baked pop. Until a stage is
attached, nothing is queued, and the cards are announced by the tooltip as before.

## Contents

| File | What it does |
|---|---|
| `ICardReveals` | What the view uses. `Current` is the reveal to play now, or null. `NextPop` is the pop the next reveal will play, chosen a reveal ahead so the view can load its sheet while the game plays (-1 until a stage attaches). `AttachStage` / `DetachStage` say a view is there to play them, and `HasStage` tells the tooltip and the level-up popup to leave the cards to the reveal. The view calls `Finish` with its reveal when done, which starts the next one. A reveal a reset or a detach already dropped finishes nothing, so a view still animating it can't cut off the one that replaced it |
| `CardRevealController` | Queues one reveal per card won: a level-up's card when `RunUpgrades` holds it (`UpgradeCardHeldMessage`, at the `Transitioning` phase) and a retry's bonus card when it is dealt (`BonusCardDealtMessage`). One plays at a time, and only over the board: in play, or during a level-up (navigation stays on `LevelUp` from the ceremony to the end of the ascent, and the level-up's card plays with the ascent). A bonus card is dealt during the retry's reset, behind the game-over screen, so it waits for play to resume. Each reveal gets a pop picked at random from `PopCount`, a reveal ahead (the first when a stage first attaches, the next as each reveal starts, before it publishes), never one of the last `RecentPopsAvoided` shown (with fewer pops than that, as many as can be avoided). A run reset drops the old run's reveals; it runs at `RunResetOrder.Quiesce`, ahead of `RunUpgrades` dealing the retry's bonus card in the same reset, so the new card is kept. The last stage detaching also drops the queue, since nothing would play it |
| `RevealedCard` | One reveal: the card, its stacks once won (`Shield` held twice is 2; a bonus card counts the held stacks plus itself), whether it is a retry's bonus card, and the pop to play |

The settings are `ICardRevealSettings` in `Configuration/CardReveal/`: the baked pops' sheets
(`PopCount` is how many) and how many recent ones to avoid (`RecentPopsAvoided`, 2). With no asset
assigned, `GameLifetimeScope` falls back to a default that has no pops, so no stage attaches.

## Who announces a card

| Stage attached | Level-up with a card | Retry's bonus card |
|---|---|---|
| No (no pops baked) | Tooltip `Level N: Card` (`UI/LevelUp/LevelUpPopUp`) | Tooltip `Bonus card: Card` (`UI/Tooltip/TooltipBanner`) |
| Yes | Tooltip `Level N`, and the reveal | The reveal only |

The level-up popup checks `HasStage` when it takes the card (at `Pending`), and the reveal is queued
when the card is held a moment later (at `Transitioning`). A stage detaching in between would leave
that one card unannounced; the view is expected to stay attached for the whole game scene.

A cheat start (`RunUpgrades.TakeCardsOnTheWayTo`) holds cards without announcing them, so it queues
no reveals.

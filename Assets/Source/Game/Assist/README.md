# Game/Assist

Help that comes only when the player needs it. A rolled help (a Snipe from the Sharpshooter card, a heart
balloon) keeps a low base chance, so a player doing well rarely sees it. While the player is in trouble,
each roll it misses makes the next one likelier, until it hits. Being forgiving where it counts is what lets
the base rates stay low.

## Contents

| File | What it does |
|---|---|
| `IPlayerNeed` / `PlayerNeed` | How much trouble the player is in, 0 to 1, read when a roll asks. It is the larger of two needs. The hearts' is 0 at full and 1 on the last heart, linear between (2 of 3 hearts is 0.5); hearts past full count as full. The board's is `IDangerLevel.Level` (1 when the next line would cost a heart) times `IAssistSettings.DangerNeedWeight` (1). A run of one heart reads no need from its hearts |
| `PityRoll` | One forgiving roll. Each roll it misses adds the need at that moment to its **pity**, so misses out of trouble add nothing. The chance is `base × (1 + GrowthPerMiss × pity × need now)`, capped at `MaxChance` (a base above the cap is kept). Out of trouble that is the base, so the help is there only while it is needed, and pity built earlier comes back with the trouble. A hit clears the pity |

## Where it is used

| Roll | Base chance | Pity (`RunConfig`, `_assist` block) |
|---|---|---|
| Sharpshooter's Snipe (`Item/Snipe/SnipeChanceRoll`), once per ball loaded | The card's: 1% a stack, 3 stacks | `SnipePity`: growth 4, cap 50%. On the last heart, one stack goes 1%, 5%, 9%… a ball; three stacks 3%, 15%, 27%… |
| Heart balloon off a purple level (`Balloon/Reward/RewardBalloonService`), once per line while a heart is missing | `HeartChanceMissingOne` (4%) / `HeartChanceMissingTwo` (12%) | `HeartPity`: growth 0.5, cap 50%. On the last heart: 12%, 18%, 24%… a line |

Each roll keeps its own pity, cleared by a hit and by a run reset. The Snipe's cooldown after a hit still
applies, and loads inside it neither roll nor build pity.

## Adding a roll

Give the roll a `PityRoll` with its own `PitySettings` on `AssistSettings`, read `IPlayerNeed.Need` when it
rolls, and clear the pity on a run reset. A roll that already scales with need (the heart's chance grows
with missing hearts) wants a gentler growth than one that doesn't.

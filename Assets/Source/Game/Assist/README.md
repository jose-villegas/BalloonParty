# Game/Assist

Help that comes only when the player needs it. A rolled help (a Snipe from the Sharpshooter card, a heart
balloon, an item on a wave) keeps a low base chance, so a player doing well rarely sees it. While the player
is in trouble, each roll it misses makes the next one likelier, until it hits. Being forgiving where it
counts is what lets the base rates stay low. The same need also tilts the picks toward the items and
balloons that help, and eases off the threats that come by chance.

## Contents

| File | What it does |
|---|---|
| `IPlayerNeed` / `PlayerNeed` | How much trouble the player is in, 0 to 1, read when a roll asks. It is the larger of two needs. The hearts' is 0 at full and 1 on the last heart, linear between (2 of 3 hearts is 0.5); hearts past full count as full. The board's is `IDangerLevel.Level` (1 when the next line would cost a heart) times `IAssistSettings.DangerNeedWeight` (1). A run of one heart reads no need from its hearts |
| `IArmourNeed` / `ArmourNeed` | A need of the board's armour, read only by the Snipe roll, since a lance is what clears armour. Each tough (or tougher) and unbreakable balloon counts from 0 as it lands to 1 once it has stood `IAssistSettings.ArmourSecondsForFull` (30 s of game time); `ArmourCountForFull` (6) such balloons at full age are full armour. The need is that share times `ArmourNeedWeight` (0.4). A balloon moved between slots keeps its age; a popped one is forgotten |
| `ICoveredBombNeed` / `CoveredBombNeed` | A need from a bomb the player is kept from, read only by the Snipe roll, since a lance is what reaches it. A balloon carrying a bomb is covered while at least `IAssistSettings.CoveredBombMinCover` (3) of the six slots round it hold unbreakables in formation, which is how the item-cover formation closes on it. Its need grows from 0 as the cover closes to `CoveredBombNeedWeight` (0.6) once it has stayed covered `CoveredBombSecondsForFull` (20 s of game time); the longest-covered bomb is the one read. A cover that opens, or a bomb that pops, starts from nothing again. Checked on every change of the board, so the time starts when the cover closes |
| `ThreatEase` | Scales a threat's chance down by `IAssistSettings.ThreatEaseAtFullNeed` (0.5) times the need: half as likely on the last heart, unchanged at full health. Also hands the need to the balloon pick |
| `PityRoll` | One forgiving roll. Each roll it misses adds the need at that moment to its **pity**, so misses out of trouble add nothing. The chance is `base × (1 + GrowthPerMiss × pity × need now)`, capped at `MaxChance` (a base above the cap is kept). Out of trouble that is the base, so the help is there only while it is needed, and pity built earlier comes back with the trouble. A hit clears the pity |

## Where it is used

| Roll | Base chance | Pity (`RunConfig`, `_assist` block) |
|---|---|---|
| Sharpshooter's Snipe (`Item/Snipe/SnipeChanceRoll`), once per ball loaded | The card's: 1% a stack, 3 stacks, tripled on a purple level (pity raises that) | `SnipePity`: growth 4, cap 50%. On the last heart, one stack goes 1%, 5%, 9%… a ball; three stacks 3%, 15%, 27%… It reads the largest of the player's need, the armour's and the covered bomb's, so a bomb kept under a formation's cover builds pity the longer it is kept (at 20 s covered, need 0.6, one stack goes 1%, 2.4%, 3.9%… a ball), and armour left standing too long builds pity at full hearts too, gentler: at full armour (need 0.4) one stack goes 1%, 1.6%, 2.3%… a ball, 7.4% after ten |
| Heart balloon off a purple level (`Balloon/Reward/RewardBalloonService`), once per line while a heart is missing | `HeartChanceMissingOne` (4%) / `HeartChanceMissingTwo` (12%) | `HeartPity`: growth 0.5, cap 50%. On the last heart: 12%, 18%, 24%… a line |
| Any item on a cadence wave (`Item/ItemAssigner.RollWaveCount`) | The level's item-count curve's chance of more than 0 | `ItemPity`: growth 0.5, cap 90%. The curve's draw stands; a wave it gives none is lifted to one item by the share of the raised chance the curve leaves out, so any item comes at the raised chance. The initial board never rolls it, and a level without items builds none |

Each roll keeps its own pity, cleared by a hit and by a run reset. The Snipe's cooldown after a hit still
applies, and loads inside it neither roll nor build pity.

One Snipe needs no roll at all: once the run's first golden balloon has turned away
`FirstGoldenSnipeAfterDeflects` (3) hits, the next ball loaded is a lance (`Item/Snipe/GoldenSnipeGift`, see
`Game/Upgrades/README.md`'s Sharpshooter).

## Other help that follows the need

| Help | Where | How |
|---|---|---|
| Helpful items picked more | `Item/ItemAssigner` → `ILevelParameters.PickItemEntry` | Each item's level weight × (1 + `ItemSettings.NeedWeightBoost` × need): Bomb, Laser and Lightning 1, Shield and Snipe 0.5, Paint 0. On the last heart a Bomb is twice as likely against a Paint |
| Helpful balloons picked more | `Balloon/Spawner/BalloonSpawner`'s line pick → `ILevelParameters.PickBalloonEntry` | The same rule off `BalloonPrefabEntry.NeedWeightBoost`: Rainbow 1, the rest 0. Rejected-balloon re-picks stay neutral |
| Fewer extra balloons from pops | `BalloonSpawner`, `IBalloonsConfiguration.PopSpawnChance` | Eased by `ThreatEase` |
| Fewer reinforcement calls | `Balloon/Formation/UnbreakableFormationService`, `ReinforcementChance` | Eased by `ThreatEase` |
| A breather before the line that would cost a heart | `Balloon/Spawner/SpawnClock`, `ISpawnClockSettings.BreatherDanger` (1) | Danger at or above it starts the breather a big clear earns, under the same cooldown: a full board on the last heart. At 0.5 it played too slow: a board left full with two hearts was slowed nearly half the time |
| Fewer breathers past full hearts | `Balloon/Spawner/SpawnClock`, `ISpawnClockSettings.BreatherChanceLostPerExtraHeart` (0.25) | The other way round: a big clear or a shape burst earns its breather at 75% with one heart past full, 50% with two, never with four. A player that far ahead gets less of the release |
| A retry's bonus card leans defensive | `Game/Upgrades/RunUpgrades.BonusDealWeight` | Each card's deal weight × (1 + `IUpgradeCardSettings.RetryDealBoost`): Thick Skin 1, so twice as likely. A retry follows a lost run, so it reads full need without asking. The deal stays seeded |

## The other side: pressure

Need only ever helps. The push back on a player who is comfortable lives in the spawn clock
(`Balloon/Spawner/SpawnClock`, `ISpawnClockSettings.Pressure*`). While the board stays under its hold
occupancy and the need is low, the clock runs ever faster, up to 2×, until the board climbs. The need slows
that build, and on the last heart it builds none. A struggling player never builds it, and a strong build
is pushed until its board climbs.

## Adding a roll

Give the roll a `PityRoll` with its own `PitySettings` on `AssistSettings`, read `IPlayerNeed.Need` when it
rolls, and clear the pity on a run reset. A roll with more outcomes than hit or miss (the item wave's count)
draws its own result and reports it with `PityRoll.Record`, reading `ChanceFor` for the raised chance. A roll
that already scales with need (the heart's chance grows with missing hearts) wants a gentler growth than one
that doesn't.

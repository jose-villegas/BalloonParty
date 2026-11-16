# Balloon/Reward

Four balloons are not threats, and no level's mix brings them. Three are rewards; the fourth, the nuke, is a
rescue (see The nuke). On a purple level all three rewards
are rolled every few lines, each further one rarer: the **heart balloon** from the level's start, up to five,
even at full hearts, where its pop gives an extra heart past the maximum; the **golden balloon** from 15% done,
up to three; the **disco ball** from 25% done, the first one sure, up to three. Level 5 always gets a single
golden one early on, the player's first, and level 8 a single disco ball; after that, any ordinary level may
get one or the other, about every 5 levels each: a little likelier early in the run, and likelier the longer
since the last. Off a purple level the heart comes to a player who is missing hearts, more often the more are
missing. The Party Favors card raises every one of these rolls' chances by 25% a stack (`IWeaponStats.RewardBalloonOdds`,
`Game/Upgrades/README.md`), all but the nuke's. At most one of each is on the board. The plans are
`Plans/PLAN-RewardBalloons.md` and `Plans/PLAN-DiscoBalloon.md`; the nuke's is `Plans/PLAN-NukeBalloon.md`. The disco
ball's pop paints the board rainbow (see The disco ball's wave).

## Contents

| File | What it does |
|---|---|
| `IRewardBalloonRequests` | What `BalloonSpawner` asks. `TryTakeDue` once per clock line: whether this line brings one, and which. `Placed` once it is on the board: returns the callback to call when it leaves |
| `RewardBalloonService` | The rules behind the answer. Registered as `IRewardBalloonRequests` and `IRunResettable` |
| `NukeSchedule` | When the nuke is due, and whether one is on the board: its gates (`FromLevel`, `PerLevel`, `CooldownLines`), the sure level (`SureLevel`) and the roll against `INukeOdds.Chance`. The service asks it first on a line |
| `INukeOdds`, `NukeOdds` | The nuke's chance on a line: a tiny base raised by the trouble the player is in (board fill, spawn-clock pressure, low hearts, hearts lost in succession, read from `WaveDamageMessage` and fading each line). Registered as an entry point and `IRunResettable` |
| `NukeFuse` | The armed nuke's three-second countdown: publishes `NukeArmedMessage` and a `NukeCountdownMessage` each second, pulls the nearby balloons in, then calls `INukeDetonator.Detonate`. Stops with no blast on a level change, game over or run reset. Registered as an entry point and `IRunResettable` |
| `INukeDetonator`, `NukeBlast` | `Detonate` is the blast itself: pops the nuke, kills the shots in flight, then grows a disc from it that pops every balloon it reaches but the other rewards, scoring nothing, and finally asks the spawn clock for a breather. `NukeBlast` implements it, as an entry point and `IRunResettable`; drawn by `Balloon/View/NukeBlastView` and `NukeCloudView` |
| `BoardDisc` | The disc's shape and the last sweep, shared by `DiscoWave` and `NukeBlast`: the radius at a share of the time, which slots the edge has reached |
| `PlatedRewardSchedule` | When one plated balloon (the golden one, the disco ball) is due, and whether one is on the board: the purple rolls, the ordinary level's single roll, the sure level. The service runs one for each; `RollPurple` is shared with the heart |
| `PlatedRewardTuning` | One plated balloon's numbers, read from `IRewardBalloonSettings` each line (`Golden(settings)`, `Disco(settings)`), so the two share the rules and differ only in these |
| `RewardBalloonPayout` | Pays a reward balloon's pop, read from `ActorHitMessage`. The golden balloon: a free card (`IFreeCardGrant`), or a heart (`IPlayerHealing.Heal`) when every card is at its limit. The heart balloon: a heart, or on a purple level an extra one past the maximum (`HealPastMax`). Pays each balloon once |
| `RewardBalloonPop` | `TryGet(hit, out balloon)`: a reward balloon's pop. The payout and the service both read pops through it, so what one pays the other counts |
| `DiscoWave` | The disco ball's pop: a disc that grows from it across the board and paints every paintable balloon rainbow. Registered as an entry point and `IRunResettable`; drawn by `Balloon/View/DiscoWaveView` |
| `RainbowLines`, `IRainbowLines` | The clock lines a disco ball's pop owes rainbow: `DiscoWave` grants them, and the spawner hands each clock line's new balloons to `PaintLine`. Registered as an entry point and `IRunResettable` |

The golden balloon itself is `Balloon/Model/GoldenBalloonModel`, a `PlatedBalloonModel`, whose hit rules a third
reward balloon, the disco ball (`DiscoBalloonModel`, `Plans/PLAN-DiscoBalloon.md`), shares: the shot's own ordinary hit bounces
off it, and it takes hits as an unbreakable does: any `Piercing` damage pops it. That is a pierce (it is
tough, so the pierce plows it and it shatters at the next wall), a bomb's blast beside it, or a laser beam
(the laser also marks its damage `DamageFlags.Beam`, which pops it even without `Piercing`). An item's plain
damage (lightning, paint) has no `DirectHit`, and its hit is `HitOutcome.None`. It is
colourless and scoreless, resists paint, and carries no item. Its prefab (`Prefabs/Balloon/Golden`) is
built from the tough balloon's, so it looks like a balloon, with a gold-plated body
(`Shaders/BalloonParty/Balloon/GoldenBalloon.shader`) and a dark gold knot. Faint god rays turn and breathe
behind it (`Balloon/Type/GodRaysLook`, the `Shaders/BalloonParty/Sprite/GodRays.shader` fan on the `GoldenRays`
material), so it stands out on a busy board: they sort below every balloon, so they show in the gaps around it.
Their colour and strength are the palette's `GoldenRays` entry; their shape and motion are the material's. The prefab keeps the tough
balloon's variant, which only draws cracks for a balloon with durability, so on the golden one it
does nothing. Its pop shatters it into gold plates (`PSVFX_GoldenPop`, the unbreakable's shatter drawing the
`Sprites/Balloons/Golden/golden_parts` sheet, cut from a capture of the balloon by `Tools/art/shatter`), lit by the time of day like the balloon (`Particle/SceneLit`). It has its own cues (`CombatSoundRouter`): `BalloonDeflectGolden` when the shot bounces off,
the gold pop's chime (`Chime 0010`) a fifth above the board's key with a high gold glint over it, climbing on
each further bounce of the same shot; and `BalloonPopGolden` when it breaks, the gold-band balloon's pop with
a balloon's burst under it and the chime an octave up as a coin ring. Both are layered from clips already in
the sound bank (`SoundBankConfiguration`), so they can be retuned there.

The heart balloon is `Balloon/Model/HeartBalloonModel`: any hit pops it, the shot's or an item's,
whatever its catalogue entry's hit count says. It is colourless and scoreless, resists paint, and
carries no item, so its pop leaves the streak alone. Its pop gives back one heart, never to a player
already out: up to the maximum, or on a purple level past it, as an extra heart (read from the level
being played when it pops, so one popped after a purple level ends heals as on the next level). Its prefab (`Prefabs/Balloon/Heart`) is the balloon's, with a
heart body and bandages drawn by `Tools/art/make_heart_balloon.py`, and its own highlight
(`Shaders/BalloonParty/Balloon/HeartShine.shader`, lit from the heart's baked normals), because the
round balloons' shine is drawn for a circle. Fainter god rays than the golden's turn behind it, white and
shorter (`GodRaysLook` again, on the `HeartRays` material and palette entry). Its pop is a beat: its catalogue entry's `PopPulse` swells it to 1.25 and shrinks it away, while `PSVFX_HeartPop` throws a burst of hearts on along the shot, drawn with the HUD heart's own sprite (`HeartTrail_Combined`: its shine and its soft baked shadow), spinning only in the board's plane so the shadow keeps falling to the lower right (the override's `AlongHit`; a board clear throws them up). Its pop cue is `BalloonPopHeart`, a harp pluck (`harp005` by t1mp, CC BY) the sound bank pitches to the board's key.

## When each one is due

Only while the level is being played (`LevelUpPhase.Playing`), and only for a type the balloon catalogue
has an entry for.

- **On a purple level** (`IsExtraColorLevel`), both kinds roll every `PurpleRollEveryLines` (6) lines, each
  on its own count, and each kind's first roll is on the first line it can. Lines while one of that kind is on
  the board don't count. Each roll's chance is the next entry of the kind's list, so each further one is rarer
  and the list's length caps the level: `PurpleGoldenChances` (35%, 20%, 10%: three at most) and
  `PurpleHeartChances` (40%, 30%, 20%, 12%, 6%: five at most). The golden rolls only once `LevelCompletion`
  reaches `PurpleGoldenAtCompletion` (0.15; each colour's points, capped at the goal, over all the goals); the
  heart from the level's first line, at full hearts too, and the roll while hurt (below) is off there. A won
  roll stays due until it is placed (a line with no room for it passes it on). A new level starts both counts
  over and drops anything owed. The disco ball rolls like the golden one, from `PurpleDiscoAtCompletion`
  (0.25), on `PurpleDiscoChances` (100%, 25%, 15%): the first is sure, so every purple level gets one. A line
  brings one balloon at most: the nuke first (see The nuke), then the golden, then the disco ball, then the heart; the
  others stay due.
- **Golden, on another level.** A single one. Every `OrdinaryGoldenEveryLevels` (1: every) level that
  isn't purple rolls once, on its first line, against `OrdinaryGoldenChance` (10%). The chance eases off
  as the run goes on: it is halved `OrdinaryChanceHalvedAfterLevels` (20) levels past the sure level, a
  third at 40 past, so it never reaches 0. A won level places one golden balloon at `GoldenAtCompletion` (0.5).
  `SureGoldenLevel` (5) needs no roll: it always places its one, and early in the level
  (`SureGoldenAtCompletion`, 10% done), so every player meets a golden balloon with the level still ahead.
  Only levels from the sure level on roll.
- **Soft pity.** Each level since the last golden balloon was placed (on any level, purple included; from the
  sure level before the first) adds `OrdinaryChanceGrowthPerLevel` (4%) to the roll, so an eased chance
  never stays low: early in a run 10%, 14%, 18%... and one comes on average every 5 levels (4.8 early, 5.7
  past level 45, one gap in a hundred reaching 12 or 13, in a 20,000-run simulation). A placed one starts the
  count over; a level that won one but ended before it was placed doesn't.
- **Disco ball, on another level.** The golden balloon's rules on its own numbers, a little more often: every
  `OrdinaryDiscoEveryLevels` (1) level from `SureDiscoLevel` (8) on, against `OrdinaryDiscoChance` (12%,
  eased and grown by the same rules, on its own count), placed at `DiscoAtCompletion` (0.35). Level 8 always
  places its one, at `SureDiscoAtCompletion` (15% done).
- **Heart, elsewhere.** A roll per line while a heart is missing: `HeartChanceMissingOne`, or
  `HeartChanceMissingTwo` with two or more missing. After one is placed, `HeartCooldownLines` lines pass
  before the next roll. Each line rolled without one while the player is in need raises the next line's
  chance (`IAssistSettings.HeartPity`, a `Game/Assist/PityRoll`: on the last heart 12%, 18%, 24%…). A heart
  rolled for a line with no room for it is owed to the next line, unless the player has healed to full in
  between. Extra hearts past the maximum count as full.

The tuning is `IRewardBalloonSettings`, a block on `BalloonsConfiguration`.

## The disco ball's wave

When a disco ball pops, `DiscoWave` grows a disc from where it popped until it covers the whole board, in
`IDiscoWaveSettings.Seconds` (0.9 s), fast at first and slowing at the far side. Every balloon the disc's
edge reaches is nudged away from the pop once (`NudgeType.Wave`). Then `Item/Paint/PaintTargets` decides
the rest, with the same rules as the Paint item:

- a balloon of an ordinary colour turns rainbow. It bursts in its old colour (`IPaintReactive.PlayRecolourBurst`,
  a smaller pop particle, with no pop: nothing scores and it stays on the board), takes the paint drip, and
  stamps the disturbance field;
- a balloon immune to paint (tough, unbreakable, the golden and heart balloons) takes the drip and keeps
  its colour;
- a rainbow balloon, or anything that isn't a balloon, is only nudged.

The disc is fixed where the ball popped, and a balloon that moves into ground it has covered is still
reached, on the next frame. When the time is up a last sweep reaches anything left. A run reset, a level
change or game over stops the wave where it is; what it painted stays painted. It runs on scaled time, so a
pause holds it.

The party outlasts the wave: the pop also owes the next clock lines rainbow (`IDiscoWaveSettings.RainbowLines`,
3 by default). `Balloon/Spawner/BalloonSpawner` hands each clock line's new balloons to `RainbowLines.PaintLine`
once they are placed, before the line's item roll. A line that brings a balloon pays one line, by the wave's rules:
a paintable balloon arrives rainbow and a paint-immune one keeps its look. A line that brings none (a full board)
pays nothing. A second disco pop while lines are still owed tops them up to the setting rather than adding to
them. A level change or a run reset forgets what is still owed.

What the wave paints is booked to the shot that broke the ball (`Projectile/Controller/ShotPaintLedger`).
That shot is still flying (a pierce breaks the ball at the next wall, and the shot behind a laser flies on),
and a rainbow pop would climb its streak whatever the colour. So its own pops of what it painted score at
its streak as it stands, climb nothing, and refund no streak shield (`DamageFlags.OwnPaint`). Any other
shot, and every later throw, pops the rainbow board as usual. The rainbow lines are booked to the same shot
while it still flies, so it can't climb on them either.

`DiscoWaveView` draws the disc (the quad-sizing part is `GrowingDiscView`, which `NukeBlastView` shares): a quad centred on the pop and sized to the disc each frame, on
`IDiscoWaveSettings.DiscMaterial` (`BalloonParty/Sprite/DiscoWave`), which must be assigned or nothing is
drawn. The shader draws a bright edge in the level's own colours, turning by angle, and a soft wash behind it
that thins as the disc grows. It fades out over the last stretch, so it's gone when the view hides it. It
sorts below the balloons, so it sweeps through the gaps, and the balloons show the edge as they turn
rainbow. Each painted balloon's drip is a rainbow one (`IPaintReactive.PlayPaintDrip`'s `rainbow`). The pop
itself takes the rainbow balloon's key-light flash (`BalloonPopPresenter`).

The ball itself shatters into mirror plates: `PSVFX_DiscoPop` is the golden balloon's shatter drawing
`Sprites/Balloons/Disco/disco_parts`, which `Tools/art/shatter` cut from a capture of the ball. Its shards are
sized to the disco ball's wider body.

The wave's sound follows two messages it publishes: `DiscoWaveAdvancedMessage`, the share of its time gone
(0 as it starts), each frame; and `DiscoWaveReachedMessage` for each balloon it paints, the immune ones
included, with how far from the pop it sits over the disc's full radius. `Audio/Routing/DiscoSoundRouter`
plays them as a rising harp glissando through the board's key and a quiet pluck for each balloon, higher the
farther out it is. The ball's own cues are `CombatSoundRouter`'s: `BalloonDeflectDisco`, a glassy tick (a
ping-pong bump under a glassy chime an octave over the board's key), and `BalloonPopDisco`, the golden
balloon's pop with the item-sighted wind chime shimmering over it.

## The nuke

The fourth balloon is a rescue, not a prize. It comes rarely, and more often the more trouble the player is in.
Shooting it three times, or hitting it with anything piercing, does not pop it: it arms it. A three-second fuse
counts down on its body, and then the blast sweeps the board and pops every balloon but the other rewards, and the
spawn clock gives a breather. It scores nothing and is placed by the service like the others
(`Plans/PLAN-NukeBalloon.md`).

**The balloon.** `Balloon/Model/NukeBalloonModel` is a `PlatedBalloonModel` that is also `IHasDurability`, with
its hits from its catalogue entry (3). The shot's ordinary hit bounces off and costs one. The last one, or any
`Piercing` damage or laser beam, arms it (`Armed`) instead of popping it. An item's plain damage does nothing, as on
the golden balloon. Once armed it deflects every ball and takes no more damage, and it is anchored
(`IBalanceAnchor`), so the balancing of the board leaves it where it is. Its prefab (`Prefabs/Balloon/Nuke`) is
the golden one's, with a lead-plated body (`Balloon/NukeBalloon.shader`: bolted plates, a radiation mark on a yellow
placard) whose plates tip and darken as it is dented, and red rays behind it (`NukeRays` palette entry and
material). `Balloon/Type/NukeFuseView` drives the shader's LED counter and status lamp from the fuse's messages: the
lamp is green until the nuke is armed and then red, blinking with each beep, and the rays turn red too. Cues: `BalloonDeflectNuke`, a lead clank a
semitone lower on each dent, and `BalloonPopNuke`, a deep boom.

**When it comes.** `NukeSchedule` asks, first on every clock line while the level is played: from
`INukeOddsSettings.FromLevel` on, `PerLevel` a level, `CooldownLines` lines apart, one on the board at most, and
for sure on `SureLevel` (10), `SureAfterLines` (10) lines in, past the odds and the cooldown. The
roll's chance is `BaseChance` raised toward `MaxChance` by the weighted sum of four signals (`NukeOdds`), capped
at 1: the board's fill (`FillFrom` to `FillFull`), the spawn clock's pressure, `PlayerNeed.HeartsNeed` (full hearts
to the last one), and hearts lost recently (each `WaveDamageMessage` adds its loss, and each line keeps
`LossKeptPerLine` of the sum). Party Favors does not raise it. The settings are `IBalloonsConfiguration.NukeOdds`.

**The fuse.** When the nuke is armed, `NukeFuse` publishes `NukeArmedMessage` and counts down
`INukeFuseSettings.Seconds` (`IBalloonsConfiguration.NukeFuse`, 3 s), publishing a `NukeCountdownMessage` with the
whole seconds left each second. `NukeFuseView` shows the digit and lamp, and `NukeSoundRouter` plays the alarm once
and a beep for each count, each a step higher. Meanwhile the board is pulled in: `NukeBlastView.Implode` stamps an
implosion into the disturbance field (the clouds, grass and specks lean towards the nuke), and every
`PullInterval` the balloons within `PullRadius` get an inward `NudgeType.Wave` nudge, a longer one each time. A level
change, a game over or a run reset stops the fuse with no blast.

**The blast.** When the fuse runs out, `NukeFuse` calls `INukeDetonator.Detonate` (`NukeBlast`). In order: the nuke
pops; `ClearShotsInFlightMessage` makes `ThrowerController` kill every shot in flight (the ball loaded to throw next
stays); `NukeDetonatedMessage` plays the boom. Then a disc grows from the nuke over `INukeBlastSettings.Seconds`
(`IBalloonsConfiguration.NukeBlast`), by `BoardDisc`, as the disco wave does. `NukeBlastView` draws it on
`Sprite/NukeBlast.shader` (a white flood over the board, white-hot core, ragged orange ring, scorch), lights the
scene with a blinding light that fades as the disc spreads, and gives the disturbance field the strongest push on it.
`NukeCloudView` spreads a mushroom cloud seen from above (`Sprite/NukeCloud.shader`, material `NukeCloud`: a
billowing cap that starts as fire and cools to lit smoke, its shadow on the board, and a dust ring racing out). Every balloon and grid actor gets an outward nudge `NudgeLead` ahead of the edge, and
every balloon the edge reaches pops through `IBalloonPopper` (`BalloonControllerRegistry.TryPopSingle`): its pop
effects play and its slot is freed, but no hit is published, so nothing scores, no streak moves, no goal advances
and no item fires. Other reward balloons and grid actors are spared. A last sweep at the end pops what is left,
and then `IBreatherGrant.Grant` (`SpawnClock`) starts the breather a big clear earns, even inside the cooldown of an
earlier one, and `NukeBreatherMessage` has `NukeSoundRouter` play a gust of wind. A level change, a game over or a
run reset stops the blast where it is, with no breather.

## Leaving the board

Every way off the board (a pop, a board clear, a level change) calls the leave callback, which frees the
slot for the next of that type. Only a pop pays, and only a pop rolls for another golden balloon, which
is why the payout and the service read the hit, not the leave. The pop's leave runs before its hit is
published, so the service guards its roll by the placement, not by whether a golden is on the board.
Each placement has its own number, so a callback from an earlier balloon, or from before a run reset,
changes nothing. Anything a reward gives belongs on its pop, not on the leave.

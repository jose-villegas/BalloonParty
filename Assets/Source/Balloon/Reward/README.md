# Balloon/Reward

Two balloons are rewards rather than threats, and no level's mix brings them. On a purple level both are
rolled every few lines, each further one rarer: the **heart balloon** from the level's start, up to five, even
at full hearts, where its pop gives an extra heart past the maximum; the **golden balloon** from 15% done, up
to three. Level 5 always gets a single golden one early on, the player's first, and now and then another
ordinary level does too. Off a purple level the heart comes to a player who is missing hearts, more often the
more are missing. At most one of each is on the board. The
plan is `Plans/PLAN-RewardBalloons.md`.

## Contents

| File | What it does |
|---|---|
| `IRewardBalloonRequests` | What `BalloonSpawner` asks. `TryTakeDue` once per clock line: whether this line brings one, and which. `Placed` once it is on the board: returns the callback to call when it leaves |
| `RewardBalloonService` | The rules behind the answer. Registered as `IRewardBalloonRequests` and `IRunResettable` |
| `RewardBalloonPayout` | Pays a reward balloon's pop, read from `ActorHitMessage`. The golden balloon: a free card (`IFreeCardGrant`), or a heart (`IPlayerHealing.Heal`) when every card is at its limit. The heart balloon: a heart, or on a purple level an extra one past the maximum (`HealPastMax`). Pays each balloon once |
| `RewardBalloonPop` | `TryGet(hit, out balloon)`: a reward balloon's pop. The payout and the service both read pops through it, so what one pays the other counts |

The golden balloon itself is `Balloon/Model/GoldenBalloonModel`, a `PlatedBalloonModel`, whose hit rules a third
reward balloon, the disco ball (`DiscoBalloonModel`, `Plans/PLAN-DiscoBalloon.md`, not placed yet), shares: the shot's own ordinary hit bounces
off it, a pierce pops it (it is tough, so the pierce plows it and it shatters at the next wall), and
so does a laser beam: the laser marks its damage `DamageFlags.Beam`. No other item touches it: an
item's damage has no `DirectHit`, and its hit is `HitOutcome.None`. A bomb's blast on its neighbours
carries `Piercing` like the beam, but never `Beam`, so a bomb beside it hands out no card. It is
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
  over and drops anything owed. The golden wins a line over the heart.
- **Golden, on another level.** Rare, and a single one. Every `OrdinaryGoldenEveryLevels` (5) levels, a
  level that isn't purple rolls once, on its first line, against `OrdinaryGoldenChance` (40%). A won level
  places one golden balloon at `GoldenAtCompletion` (0.5).
  `SureGoldenLevel` (5) needs no roll: it always places its one, and early in the level
  (`SureGoldenAtCompletion`, 10% done), so every player meets a golden balloon with the level still ahead.
- **Heart, elsewhere.** A roll per line while a heart is missing: `HeartChanceMissingOne`, or
  `HeartChanceMissingTwo` with two or more missing. After one is placed, `HeartCooldownLines` lines pass
  before the next roll. Each line rolled without one while the player is in need raises the next line's
  chance (`IAssistSettings.HeartPity`, a `Game/Assist/PityRoll`: on the last heart 12%, 18%, 24%…). A heart
  rolled for a line with no room for it is owed to the next line, unless the player has healed to full in
  between. Extra hearts past the maximum count as full.

The tuning is `IRewardBalloonSettings`, a block on `BalloonsConfiguration`.

## Leaving the board

Every way off the board (a pop, a board clear, a level change) calls the leave callback, which frees the
slot for the next of that type. Only a pop pays, and only a pop rolls for another golden balloon, which
is why the payout and the service read the hit, not the leave. The pop's leave runs before its hit is
published, so the service guards its roll by the placement, not by whether a golden is on the board.
Each placement has its own number, so a callback from an earlier balloon, or from before a run reset,
changes nothing. Anything a reward gives belongs on its pop, not on the leave.

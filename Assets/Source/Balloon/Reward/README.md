# Balloon/Reward

Two balloons are rewards rather than threats, and no level's mix brings them. The **golden balloon** comes
once on a purple level, from the line that finds the level three quarters done. The **heart balloon**
comes to a player who is missing hearts, more often the more are missing. At most one of each is on the
board. The plan is `Plans/PLAN-RewardBalloons.md`.

## Contents

| File | What it does |
|---|---|
| `IRewardBalloonRequests` | What `BalloonSpawner` asks. `TryTakeDue` once per clock line: whether this line brings one, and which. `Placed` once it is on the board: returns the callback to call when it leaves |
| `RewardBalloonService` | The rules behind the answer. Registered as `IRewardBalloonRequests` and `IRunResettable` |
| `RewardBalloonPayout` | Pays a reward balloon's pop, read from `ActorHitMessage`. The golden balloon: a free card (`IFreeCardGrant`), or a heart (`IPlayerHealing`) when every card is at its limit. The heart balloon: a heart. Pays each balloon once |

The golden balloon itself is `Balloon/Model/GoldenBalloonModel`: the shot's own ordinary hit bounces
off it, a pierce pops it (it is tough, so the pierce plows it and it shatters at the next wall), and
no item touches it: an item's damage has no `DirectHit`, and its hit is `HitOutcome.None`. It is
colourless and scoreless, resists paint, and carries no item. Its prefab (`Prefabs/Balloon/Golden`) is
built from the tough balloon's, so it looks like a balloon, with a gold-plated body
(`Shaders/BalloonParty/Balloon/GoldenBalloon.shader`) and a dark gold knot. The prefab keeps the tough
balloon's variant, which only draws cracks for a balloon with durability, so on the golden one it
does nothing. It has its own cues (`CombatSoundRouter`): `BalloonDeflectGolden` when the shot bounces off,
the unbreakable's metallic chime a fifth above the board's key with a high gold glint over it, climbing on
each further bounce of the same shot; and `BalloonPopGolden` when it breaks, the gold-band balloon's pop with
a balloon's burst under it and the chime an octave up as a coin ring. Both are layered from clips already in
the sound bank (`SoundBankConfiguration`), so they can be retuned there.

The heart balloon is `Balloon/Model/HeartBalloonModel`: any hit pops it, the shot's or an item's,
whatever its catalogue entry's hit count says. It is colourless and scoreless, resists paint, and
carries no item, so its pop leaves the streak alone. Its pop gives back one heart, never past the
maximum and never to a player already out. Its prefab (`Prefabs/Balloon/Heart`) is the balloon's, with a
heart body and bandages drawn by `Tools/art/make_heart_balloon.py`, and its own highlight
(`Shaders/BalloonParty/Balloon/HeartShine.shader`, lit from the heart's baked normals), because the
round balloons' shine is drawn for a circle. Its pop cue is `BalloonPopHeart`, a kalimba note for now.

## When each one is due

Only while the level is being played (`LevelUpPhase.Playing`), and only for a type the balloon catalogue
has an entry for.

- **Golden.** On a purple level (`IsExtraColorLevel`), once `LevelCompletion` reaches
  `GoldenAtCompletion` (0.75): each colour's points, capped at the goal, over all the goals. Once per
  level: a new level re-arms it. One that could not be placed stays due for the next line. It wins a line
  over the heart.
- **Heart.** A roll per line while a heart is missing: `HeartChanceMissingOne`, or
  `HeartChanceMissingTwo` with two or more missing, plus `HeartChanceOnPurple` on a purple level. After
  one is placed, `HeartCooldownLines` lines pass before the next roll. A heart rolled for a line with no
  room for it is owed to the next line, unless the player has healed to full in between.

The tuning is `IRewardBalloonSettings`, a block on `BalloonsConfiguration`.

## Leaving the board

Every way off the board (a pop, a board clear, a level change) calls the leave callback, which frees the
slot for the next of that type. Only a pop pays, which is why the payout reads the hit, not the leave. Each placement has its own number, so a callback from an earlier balloon,
or from before a run reset, changes nothing. Anything a reward gives belongs on its pop, not on the leave.

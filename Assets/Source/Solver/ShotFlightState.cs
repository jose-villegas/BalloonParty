using BalloonParty.Shared;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>Seeds a <see cref="ShotFlightState" /> at construction — the test seam standing in for
    /// a mid-flight item grant (Phase C's <c>ShotItemLayer</c> is the real source once it activates
    /// item effects) and, until then, the exact set of starting fields a test needs to pin an end-
    /// condition or scoring branch without a full item layer in place. <c>default</c> reproduces the
    /// pre-fold "no starting state" behavior exactly — see <see cref="ShotFlightState" />'s ctor.</summary>
    internal readonly struct ShotFlightSeed
    {
        public readonly bool RainbowBuffUntilWall;
        public readonly bool RainbowBuffUntilPierceEnd;
        public readonly string ProjectileColor;
        public readonly string StreakColor;
        public readonly int StreakCount;
        public readonly bool IsPiercing;

        // 1f can't express "no speed buff" (it's also the buffless multiplier) — this bool is the
        // seed-side mirror of ShotFlightState.HasSpeedBuff (see its own doc for why it exists).
        public readonly bool HasSpeedBuff;
        public readonly float SpeedBuffMultiplier;

        private ShotFlightSeed(
            bool rainbowBuffUntilWall, bool rainbowBuffUntilPierceEnd, string projectileColor, string streakColor,
            int streakCount, bool isPiercing, bool hasSpeedBuff, float speedBuffMultiplier)
        {
            RainbowBuffUntilWall = rainbowBuffUntilWall;
            RainbowBuffUntilPierceEnd = rainbowBuffUntilPierceEnd;
            ProjectileColor = projectileColor;
            StreakColor = streakColor;
            StreakCount = streakCount;
            IsPiercing = isPiercing;
            HasSpeedBuff = hasSpeedBuff;
            SpeedBuffMultiplier = speedBuffMultiplier;
        }

        /// <summary>No buffs — only a starting colour/streak already in progress (e.g. a grant landing
        /// mid-streak).</summary>
        public static ShotFlightSeed Fresh(string projectileColor = null, string streakColor = null, int streakCount = 0)
        {
            return new ShotFlightSeed(false, false, projectileColor, streakColor, streakCount, false, false, 1f);
        }

        /// <summary><paramref name="untilWall" /> mirrors a Shield-item grant (ends on the next shield-
        /// spending wall bounce); <paramref name="untilPierceEnd" /> mirrors a Snipe-item grant (ends
        /// when the ridden pierce ends — Phase C6).</summary>
        public static ShotFlightSeed WithRainbowBuff(
            bool untilWall = false, bool untilPierceEnd = false, string projectileColor = null,
            string streakColor = null, int streakCount = 0)
        {
            return new ShotFlightSeed(
                untilWall, untilPierceEnd, projectileColor, streakColor, streakCount, false, false, 1f);
        }

        /// <summary>A streak already in progress, no rainbow buff — the seam a refund-gate test needs
        /// (the gate requires <c>StreakCount &gt;= 2</c> before a first buffed pop can ever satisfy it).</summary>
        public static ShotFlightSeed WithStreak(string streakColor, int streakCount, string projectileColor = null)
        {
            return new ShotFlightSeed(false, false, projectileColor, streakColor, streakCount, false, false, 1f);
        }

        /// <summary>A ball already armed before it fires: a Snipe pickup that armed the ball still in the
        /// thrower, or the Sharpshooter card arming it as it loaded. <paramref name="speedBuffMultiplier" />
        /// is the Snipe's speed buff (≤ 0 for none); <paramref name="rainbow" /> the rainbow host's grant,
        /// which rides the pierce.</summary>
        public static ShotFlightSeed Armed(float speedBuffMultiplier, bool rainbow = false)
        {
            var hasSpeedBuff = speedBuffMultiplier > 0f;
            return new ShotFlightSeed(
                false, rainbow, null, null, 0, true, hasSpeedBuff, hasSpeedBuff ? speedBuffMultiplier : 1f);
        }
    }

    /// <summary>One pop's scoring identity — replaces the ad-hoc bool seams <c>ResolvePopScore</c> used
    /// to read straight off <see cref="ShotFlightState" /> (@ref plan_shot_solver_accuracy Phase C §1.5).
    /// <see cref="IsProjectileContact" /> gates BOTH colour adoption and the shield refund — an item-
    /// triggered pop (Bomb/Laser/Lightning) never steals the projectile's colour and never refunds,
    /// mirroring <c>ProjectileHitResolver</c>'s adoption/refund running only off its own direct-contact
    /// path (an item's <c>DamageContext</c> dispatch never touches <c>projectile.ColorName</c> or the
    /// streak tracker's refund gate). <see cref="SourceColorId" /> is the PRE-adoption projectile colour
    /// for a projectile contact, or the item's own host balloon's colour for an item-triggered one
    /// (mirrors <c>BombItemHandler</c>'s <c>balloon.GetColorId()</c> becoming every dispatched hit's
    /// <c>DamageContext.SourceColorId</c>) — the anchor a rainbow target or a source-colour-paying
    /// (Unbreakable) target both read.</summary>
    internal readonly struct ShotPopCause
    {
        public readonly bool IsProjectileContact;
        public readonly string SourceColorId;
        public readonly DamageFlags Flags;

        private ShotPopCause(bool isProjectileContact, string sourceColorId, DamageFlags flags)
        {
            IsProjectileContact = isProjectileContact;
            SourceColorId = sourceColorId;
            Flags = flags;
        }

        /// <summary>Mirrors <c>ProjectileHitResolver.ResolveContactPop</c>'s own flag composition —
        /// <see cref="DamageFlags.DirectHit" /> always; <see cref="DamageFlags.WildcardStreak" /> plus
        /// <see cref="DamageFlags.Piercing" /> under a rainbow buff; <see cref="DamageFlags.Piercing" />
        /// alone under an armed (non-buffed) shot; <see cref="DamageFlags.CarryStreak" /> for the shot popping
        /// a rainbow target absent a buff, coloured yet or not; <see cref="DamageFlags.OwnPaint" /> in its
        /// place for a rainbow the shot painted itself.</summary>
        public static ShotPopCause ProjectileContact(
            bool hasRainbowBuff, bool isPiercing, bool isRainbowTargetCarry,
            string projectileColor, bool isOwnPaint = false)
        {
            var flags = DamageFlags.DirectHit
                | (hasRainbowBuff ? DamageFlags.WildcardStreak : DamageFlags.Normal)
                | (isPiercing ? DamageFlags.Piercing : DamageFlags.Normal)
                | (isRainbowTargetCarry ? DamageFlags.CarryStreak : DamageFlags.Normal)
                | (isOwnPaint ? DamageFlags.OwnPaint : DamageFlags.Normal);
            return new ShotPopCause(true, projectileColor, flags);
        }

        /// <summary>An item's own AOE/effect pop (@ref plan_shot_solver_accuracy Phase C2) — never
        /// adopts the projectile's colour, never refunds a shield (see <see cref="IsProjectileContact" />'s
        /// doc).</summary>
        public static ShotPopCause ItemEffect(string sourceColorId, DamageFlags flags)
        {
            return new ShotPopCause(false, sourceColorId, flags);
        }
    }

    /// <summary>
    ///     What a simulated flight's mushrooms did (@ref plan_meadow_mushrooms), for the result: all zero on a
    ///     board without them.
    /// </summary>
    internal struct ShotMushroomTally
    {
        /// <summary>Every bounce off a mushroom, the ones the chain did not count included.</summary>
        public int Bounces;

        /// <summary>Chains flown to their target: bounces that brought the count to it, whatever they paid.</summary>
        public int ChainsCompleted;

        /// <summary>How many times the flight's mushrooms armed the ball as the lance. The chain rule arms only a
        /// ball that is not a lance, so this passes 1 only when a lance was spent at a wall and a later chain armed
        /// the ball again.</summary>
        public int LancesArmed;

        /// <summary>Caps the flight cut as a lance, whatever armed it.</summary>
        public int CapsCut;

        /// <summary>Caps blown off by the bombs the flight set off.</summary>
        public int CapsBlown;

        /// <summary>Lance charges the flight banked by cutting caps: one a cap.</summary>
        public int ChargesBanked;

        /// <summary>One bit for each cap bounced off, by its id; ids past 31 are
        /// not kept.</summary>
        public int HitMask;

        /// <summary>Whether the mushroom with this id was bounced off at least once.</summary>
        public readonly bool Hit(int bumperId)
        {
            return bumperId >= 0 && bumperId < 32 && (HitMask & (1 << bumperId)) != 0;
        }
    }

    /// <summary>
    ///     The mushroom a simulated ball bounced off last and the direction it left along, mirroring what
    ///     <c>ProjectileFlightState</c> keeps: while the ball still flies that direction it is on its way out of
    ///     that mushroom's circle and no swept test may read it as entered. The default is a ball that has left
    ///     none.
    /// </summary>
    internal struct ShotLeftBumper
    {
        // Kept one above the id, so the default has no mushroom although ids start at 0.
        private int _idPlusOne;
        private Vector2 _direction;

        internal void Leave(int bumperId, Vector2 direction)
        {
            _idPlusOne = bumperId + 1;
            _direction = direction;
        }

        /// <summary>
        ///     The id a ball flying <paramref name="direction" /> is still leaving, or
        ///     <see cref="MushroomChainState.NoBumper" /> once it has turned since that bounce. The comparison
        ///     is the live flight's own (<c>ProjectileFlightState.LeavingBumperId</c>), <see cref="Vector2" />'s
        ///     equality included.
        /// </summary>
        internal readonly int IdWhileFlying(Vector2 direction)
        {
            return _idPlusOne != 0 && direction == _direction ? _idPlusOne - 1 : MushroomChainState.NoBumper;
        }
    }

    /// <summary>Mutable per-flight state threaded through <see cref="ShotSimulator.Simulate" /> — passed
    /// by <c>ref</c> to every helper that mutates it (a by-value pass would silently drop the mutation).
    /// Board/geometry inputs (working set, walls, dynamics, cruise config) and the timeline outputs
    /// (path/timestamps) are deliberately NOT here — this is flight-local scoring/motion state only.</summary>
    internal struct ShotFlightState
    {
        public Vector2 Position;
        public Vector2 Direction;
        public int Shields;
        public float Elapsed;
        public int ConsecutiveWallBounces;
        public bool IsCruising;
        public bool IsPiercing;
        public float PierceSpeedScale;

        // Speed taps earned so far, COUNTED exactly as live counts them rather than derived from shields
        // spent since cruise entry. The derivation only held while "a wall hit is the only way to lose a
        // shield" stayed true, and it silently mis-counted whenever a shield moved for any other reason
        // (a streak refund mid-cruise lowered it). Taps accrue for the whole flight, piercing included —
        // only MaxSpeedMultiplier bounds where the speed ends up.
        public int TotalTaps;

        // Per-segment Sweep bookkeeping, mirroring the live fields of the same names: pops landed since the
        // last wall, whether every contact on the segment was a one-shot kill, the length of the current run
        // of clean clearing passes (SweepTapThreshold is the run length that earns taps), and where the
        // segment began.
        public int SegmentPopCount;
        public bool SegmentSweepValid;
        public int ConsecutiveSweeps;
        public Vector2 LastBouncePosition;

        // Snipe pickups taken while the shot was already piercing (mirrors
        // ProjectileFlightState.BankedPierceCharges): banked whole — pierce, speed and rainbow — and
        // activated one per discharge that spends the running pierce. Rainbow charges are spent first.
        public int BankedPierceCharges;
        public int BankedRainbowPierceCharges;
        public float BankedPierceMultiplier;

        // Phase D-core in-sim buff state (@ref plan_shot_solver_accuracy Phase D-core), split per
        // Phase C's item layer (@ref plan_shot_solver_accuracy Phase C §5) into its two concrete grant
        // sources so each can end independently: RainbowBuffUntilWall is the Shield-item grant (ends on
        // a wall bounce that spends a shield — see HandleWallBounce); RainbowBuffUntilPierceEnd is the
        // Snipe-item grant (ends when the ridden pierce ends — Phase C6/E2). HasRainbowBuff is either.
        public bool RainbowBuffUntilWall;
        public bool RainbowBuffUntilPierceEnd;

        // 1f can't express "no speed buff granted" (it's also the buffless multiplier) — Phase C6's
        // non-stacking Snipe grant ("refresh, don't add") needs this bool to tell the two apart.
        public bool HasSpeedBuff;
        public float SpeedBuffMultiplier;
        public string StreakColor;
        public int StreakCount;
        public bool CarryOnColorChange;
        public string ProjectileColor;

        public int RawScore;
        public int Pops;
        public int GoldenPops;
        public int HeartPops;
        public int DiscoPops;
        public int NukePops;
        public int ToughsCleared;
        public int Events;
        public bool Died;
        public bool Capped;
        public bool Absorbed;

        // The meadow's mushrooms, mirroring ProjectileFlightState: the chain's tally, which MushroomChainRule
        // writes in place, the mushroom the ball is leaving, and what the flight's mushrooms did, for the
        // result. All of it stays at its default on a board without mushrooms.
        public MushroomChainState MushroomChain;
        public ShotLeftBumper LeftBumper;
        public ShotMushroomTally Mushrooms;

        public readonly bool HasRainbowBuff => RainbowBuffUntilWall || RainbowBuffUntilPierceEnd;

        public ShotFlightState(Vector2 position, Vector2 direction, int shields, in ShotFlightSeed seed = default)
        {
            Position = position;
            Direction = direction;
            Shields = shields;
            Elapsed = 0f;
            ConsecutiveWallBounces = 0;
            IsCruising = false;
            IsPiercing = seed.IsPiercing;
            PierceSpeedScale = 1f;
            TotalTaps = 0;
            SegmentPopCount = 0;
            SegmentSweepValid = true;
            ConsecutiveSweeps = 0;
            LastBouncePosition = position;
            BankedPierceCharges = 0;
            BankedRainbowPierceCharges = 0;
            BankedPierceMultiplier = 1f;
            RainbowBuffUntilWall = seed.RainbowBuffUntilWall;
            RainbowBuffUntilPierceEnd = seed.RainbowBuffUntilPierceEnd;
            HasSpeedBuff = seed.HasSpeedBuff;
            // A seed with no speed buff must reproduce the pre-fold default of 1x exactly — only a
            // HasSpeedBuff seed may override it away from that floor.
            SpeedBuffMultiplier = seed.HasSpeedBuff ? seed.SpeedBuffMultiplier : 1f;
            StreakColor = seed.StreakColor;
            StreakCount = seed.StreakCount;
            CarryOnColorChange = false;
            ProjectileColor = seed.ProjectileColor;
            RawScore = 0;
            Pops = 0;
            GoldenPops = 0;
            HeartPops = 0;
            DiscoPops = 0;
            NukePops = 0;
            ToughsCleared = 0;
            Events = 0;
            Died = false;
            Capped = false;
            Absorbed = false;
            MushroomChain = default;
            LeftBumper = default;
            Mushrooms = default;
        }
    }
}

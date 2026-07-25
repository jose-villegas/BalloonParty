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
        public int CruiseStartShields;
        public string StreakColor;
        public int StreakCount;
        public string ProjectileColor;

        // Banks a colourless-projectile rainbow pop until the streak next anchors on a real colour
        // (ColorStreakTracker.RecordDeferred/Record's fold) — see ShotSimulator.RecordColor.
        public int DeferredPops;
        public int RawScore;
        public int Pops;
        public int ToughsCleared;
        public int Events;
        public bool Died;
        public bool Capped;
        public bool Absorbed;

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
            RainbowBuffUntilWall = seed.RainbowBuffUntilWall;
            RainbowBuffUntilPierceEnd = seed.RainbowBuffUntilPierceEnd;
            HasSpeedBuff = seed.HasSpeedBuff;
            // A seed with no speed buff must reproduce the pre-fold default of 1x exactly — only a
            // HasSpeedBuff seed may override it away from that floor.
            SpeedBuffMultiplier = seed.HasSpeedBuff ? seed.SpeedBuffMultiplier : 1f;
            CruiseStartShields = 0;
            StreakColor = seed.StreakColor;
            StreakCount = seed.StreakCount;
            ProjectileColor = seed.ProjectileColor;
            DeferredPops = 0;
            RawScore = 0;
            Pops = 0;
            ToughsCleared = 0;
            Events = 0;
            Died = false;
            Capped = false;
            Absorbed = false;
        }
    }
}

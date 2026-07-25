using UnityEngine;

namespace BalloonParty.Solver
{
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

        // Phase D-core in-sim buff state (@ref plan_shot_solver_accuracy Phase D-core): both end on
        // one of two concrete conditions only — a wall bounce that spends a shield (HasRainbowBuff;
        // mirrors WallBounceEndCondition) or a cruise-pierce end (SpeedBuffMultiplier reset to 1;
        // mirrors PierceEndedEndCondition) — see HandleWallBounce. Grants (Snipe/Shield item
        // activations) are Phase C, not modeled here.
        public bool HasRainbowBuff;
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

        public ShotFlightState(Vector2 position, Vector2 direction, int shields)
        {
            Position = position;
            Direction = direction;
            Shields = shields;
            Elapsed = 0f;
            ConsecutiveWallBounces = 0;
            IsCruising = false;
            IsPiercing = false;
            PierceSpeedScale = 1f;
            HasRainbowBuff = false;
            SpeedBuffMultiplier = 1f;
            CruiseStartShields = 0;
            StreakColor = null;
            StreakCount = 0;
            ProjectileColor = null;
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

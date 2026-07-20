using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>How specks move — Brownian drift, drag, and their response to scenario motion and the
    /// disturbance field.</summary>
    internal interface ISpeckMotionSettings
    {
        float BrownianStrength { get; }
        float Drag { get; }
        float MotionInfluence { get; }
        float DisturbanceInfluence { get; }
        float DisturbanceDamping { get; }
        Vector2 SwirlAngle { get; }
        float FlowInfluence { get; }
        float TeleportThreshold { get; }

        /// <summary>Hard per-speck speed cap (world units/sec) applied after drag each step, so a steep
        /// gradient off a fresh disturbance stamp can't fling a speck into an unbounded streak. A missing/0
        /// serialized value falls back to a built-in default, so the clamp is always active.</summary>
        float MaxSpeed { get; }

        /// <summary>Upper bound on the per-frame dt fed to the sim (a hitch/pause-resume spike would otherwise
        /// take an oversized accel/position step).</summary>
        float MaxDeltaTime { get; }

        /// <summary>Per-palette-colour motion overrides blended into by a speck's heat; colours without an entry
        /// keep the base motion above. (TeleportThreshold is CPU-side and not per-colour.)</summary>
        IReadOnlyList<SpeckMotionProfile> ColorProfiles { get; }
    }

    [Serializable]
    internal class SpeckMotionSettings : ISpeckMotionSettings
    {
        // Fallbacks for the two safety clamps. Both fields were added after SpeckFieldSettings.asset was
        // last saved, and Unity does not reliably apply a nested [Serializable] field's initializer for a
        // key absent from existing serialized data — so the getters below fall back to these, keeping the
        // clamps active on every build regardless of whether the asset carries the keys.
        private const float DefaultMaxSpeed = 25f;
        private const float DefaultMaxDeltaTime = 1f / 30f;

        [SerializeField] private float _brownianStrength = 0.6f;
        [SerializeField] private float _drag = 2f;
        [SerializeField] private float _motionInfluence = 1f;
        [SerializeField] private float _disturbanceInfluence = 1f;

        [Tooltip("Extra velocity damping applied where the disturbance is active, so the push settles.")]
        [SerializeField] private float _disturbanceDamping = 4f;

        [Tooltip("Per-speck swirl angle range (degrees) rotating the disturbance push — 0 = straight out, " +
                 "90 = pure orbit. Same rotational sense for all (a coherent vortex); each picks a random " +
                 "angle in this range.")]
        [SerializeField] private Vector2 _swirlAngle = new(30f, 90f);

        [Tooltip("Speed specks advance along the disturbance's own motion (the white direction), so the " +
                 "vortex travels with the flow. Bounded advection — high values go faster but don't run away.")]
        [SerializeField] private float _flowInfluence = 1f;

        [Tooltip("Per-frame root move (world units) above which it's treated as a teleport (e.g. the " +
                 "Ascent snapping the root to its start height) and ignored, not matched.")]
        [SerializeField] private float _teleportThreshold = 1f;

        [Tooltip("Hard per-speck speed cap (world units/sec), applied after drag each step, so a steep " +
                 "gradient off a fresh disturbance stamp can't fling a speck into an unbounded streak. " +
                 "Left at 0 falls back to a built-in default (the clamp is always active). Generous by " +
                 "design — tune down if flings still slip through, or up if fast bursts look visibly capped.")]
        [SerializeField] private float _maxSpeed = DefaultMaxSpeed;

        [Tooltip("Upper bound on the per-frame dt fed to the sim, so a hitch or a pause/resume doesn't take " +
                 "an oversized accel/position step. Left at 0 falls back to a built-in default.")]
        [SerializeField] private float _maxDeltaTime = DefaultMaxDeltaTime;

        [Tooltip("Per-palette-colour motion overrides. A speck showing a colour blends from the base motion " +
                 "above toward the profile tagged with that colour, scaled by its heat; colours without a " +
                 "profile keep the base motion.")]
        [SerializeField] private SpeckMotionProfile[] _colorProfiles = Array.Empty<SpeckMotionProfile>();

        public float BrownianStrength => _brownianStrength;
        public float Drag => _drag;
        public float MotionInfluence => _motionInfluence;
        public float DisturbanceInfluence => _disturbanceInfluence;
        public float DisturbanceDamping => _disturbanceDamping;
        public Vector2 SwirlAngle => _swirlAngle;
        public float FlowInfluence => _flowInfluence;
        public float TeleportThreshold => _teleportThreshold;
        public float MaxSpeed => _maxSpeed > 0f ? _maxSpeed : DefaultMaxSpeed;
        public float MaxDeltaTime => _maxDeltaTime > 0f ? _maxDeltaTime : DefaultMaxDeltaTime;
        public IReadOnlyList<SpeckMotionProfile> ColorProfiles => _colorProfiles;
    }
}

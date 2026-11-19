using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     What a mushroom's cap does while nothing touches it: it breathes, and it rocks in the disturbance field
    ///     the grass and the bush leaves answer. Authored on the mushroom settings (<c>IMushroomSettings.Idle</c>).
    ///     The look only: the cap the ball bounces off is the same circle whatever these say.
    /// </summary>
    /// <remarks>
    ///     All of it is the shader's work, a vertex: <c>MushroomView</c> hands these over once, as the vectors
    ///     below, and stays switched off while its cap is at rest.
    /// </remarks>
    [Serializable]
    internal struct MushroomIdleTuning
    {
        private const float MinSeconds = 0.5f;
        private const float MinRockSeconds = 0.1f;

        [Header("The breath")]
        [Tooltip("How much the cap swells at the top of a breath, as a share of its radius. It only ever swells: " +
            "the cap is never drawn smaller than the circle the ball bounces off. 0 turns the breath off.")]
        [SerializeField] [Range(0f, 0.15f)] private float _breath;

        [Tooltip("Seconds a breath takes, in and out.")]
        [SerializeField] [Range(MinSeconds, 10f)] private float _breathSeconds;

        [Tooltip("How far apart two mushrooms' breaths are: 0 every cap breathes in step, 1 each is anywhere in " +
            "the breath, by its own seed.")]
        [SerializeField] [Range(0f, 1f)] private float _phaseSpread;

        [Tooltip("Breaths a world unit across the board puts two mushrooms apart, so a breath runs along a wall " +
            "as a wave. Slots are 0.75 apart. 0 for none.")]
        [SerializeField] [Range(0f, 1f)] private float _waveAcross;

        [Tooltip("How much further off the cap its shadow lies at the top of a breath, as a share of the " +
            "shadow's distance: the cap reads as rising toward the eye.")]
        [SerializeField] [Range(0f, 2f)] private float _shadowLift;

        [Tooltip("How far the warts' glow at night swings either side of its own strength with the breath: 0.4 " +
            "is from 0.6 of it breathed out to 1.4 breathed in.")]
        [SerializeField] [Range(0f, 1f)] private float _glowSwing;

        [Tooltip("How much the warts spread over the cap at the top of a breath, as the skin of a thing blown " +
            "up does, so the swell is not a flat scale of the picture.")]
        [SerializeField] [Range(0f, 0.3f)] private float _wartSpread;

        [Header("The rocking")]
        [Tooltip("How far the cap leans for a unit of the disturbance field's displacement squared, in world " +
            "units. A ball flying close past leaves about 0.9 of displacement at a cap, a pop beside it 0.6, " +
            "and the field at rest holds about 0.05. 0 turns the rocking off.")]
        [SerializeField] [Min(0f)] private float _rockPerDisplacement;

        [Tooltip("The furthest the field ever leans the cap, in world units. A shot off the cannon leans it 0.14.")]
        [SerializeField] [Min(0f)] private float _rockMost;

        [Tooltip("Seconds one rock takes, there and back.")]
        [SerializeField] [Range(MinRockSeconds, 3f)] private float _rockSeconds;

        [Tooltip("The share of the lean that swings; the rest is held, away from what passed. 0 a lean that " +
            "only eases back as the field does, 1 a rocking about the foot.")]
        [SerializeField] [Range(0f, 1f)] private float _rockSwing;

        /// <summary>The swell at the top of a breath, as a share of the radius. Never under 0: the cap is never
        /// drawn smaller than its collider.</summary>
        public float Breath => Mathf.Max(0f, _breath);

        public float BreathSeconds => Mathf.Max(MinSeconds, _breathSeconds);
        public float PhaseSpread => Mathf.Clamp01(_phaseSpread);
        public float WaveAcross => Mathf.Max(0f, _waveAcross);
        public float ShadowLift => Mathf.Max(0f, _shadowLift);
        public float GlowSwing => Mathf.Clamp01(_glowSwing);
        public float WartSpread => Mathf.Clamp(_wartSpread, 0f, 0.3f);
        public float RockPerDisplacement => Mathf.Max(0f, _rockPerDisplacement);

        /// <summary>The furthest the field leans the cap, in world units.</summary>
        public float RockMost => Mathf.Max(0f, _rockMost);

        public float RockSeconds => Mathf.Max(MinRockSeconds, _rockSeconds);
        public float RockSwing => Mathf.Clamp01(_rockSwing);

        /// <summary>The shader's <c>_Breath</c>: the swell, the breath's pace in radians a second, and how far
        /// apart a seed of 1 and a world unit put two mushrooms' breaths, in radians.</summary>
        public Vector4 BreathVector
        {
            get
            {
                const float turn = 2f * Mathf.PI;
                return new Vector4(Breath, turn / BreathSeconds, turn * PhaseSpread, turn * WaveAcross);
            }
        }

        /// <summary>The shader's <c>_BreathLook</c>: what rides the breath.</summary>
        public Vector4 LookVector => new(ShadowLift, GlowSwing, WartSpread, 0f);

        internal MushroomIdleTuning(
            float breath, float breathSeconds, float phaseSpread, float waveAcross, float shadowLift, float glowSwing,
            float wartSpread, float rockPerDisplacement, float rockMost, float rockSeconds, float rockSwing)
        {
            _breath = breath;
            _breathSeconds = breathSeconds;
            _phaseSpread = phaseSpread;
            _waveAcross = waveAcross;
            _shadowLift = shadowLift;
            _glowSwing = glowSwing;
            _wartSpread = wartSpread;
            _rockPerDisplacement = rockPerDisplacement;
            _rockMost = rockMost;
            _rockSeconds = rockSeconds;
            _rockSwing = rockSwing;
        }

        /// <summary>The shader's <c>_Rock</c>, for a cap of <paramref name="radius" />: the lean for a unit of
        /// the field's displacement squared and the most, both in radii, the rocking's pace in radians a second
        /// and the share that swings. Nothing for a cap with no size.</summary>
        public Vector4 RockVector(float radius)
        {
            return radius > 1e-4f
                ? new Vector4(RockPerDisplacement / radius, RockMost / radius, 2f * Mathf.PI / RockSeconds, RockSwing)
                : Vector4.zero;
        }
    }
}

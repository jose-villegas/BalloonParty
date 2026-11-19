using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     How a mushroom's cap answers a hit (<c>MushroomCapSpring</c>): how far it leans, how it wobbles
    ///     back, and how hard it swells when the bounce pays the lance. Authored on the mushroom settings
    ///     (<c>IMushroomSettings.Lean</c>).
    /// </summary>
    /// <remarks>
    ///     The reach of a lean and of a swell are authored as the furthest the spring gets, not as the push that
    ///     sends it there, so changing how fast or how long the cap wobbles leaves how far it goes alone.
    /// </remarks>
    [Serializable]
    internal struct MushroomLeanTuning
    {
        // A spring slower than this would still be wobbling when the ball comes back; a faster one is a blur.
        private const float MinFrequency = 0.5f;
        private const float MaxFrequency = 12f;

        // At 1 the cap would creep back with no wobble at all, and the peak below divides by what is left of 1.
        private const float MaxDamping = 0.95f;

        [Tooltip("How far the cap leans at its furthest for each unit of the ball's speed, in world units. The " +
            "ball leaves the cannon at about 10 units a second.")]
        [SerializeField] [Min(0f)] private float _leanPerSpeed;

        [Tooltip("The furthest the cap ever leans, in world units, however fast the ball and however many hits " +
            "pile up. A mushroom's radius is 0.3.")]
        [SerializeField] [Min(0f)] private float _maxLean;

        [Tooltip("Wobbles a second.")]
        [SerializeField] [Range(MinFrequency, MaxFrequency)] private float _frequency;

        [Tooltip("How fast the wobble dies: 0 never, 1 with no wobble at all. Low reads as elastic.")]
        [SerializeField] [Range(0.02f, MaxDamping)] private float _damping;

        [Tooltip("How far the cap swells when a bounce arms the lance or banks a charge: 1 is the material's full " +
            "swell and glow, 0 none.")]
        [SerializeField] [Range(0f, 2f)] private float _rewardPulse;

        [Tooltip("The lean, in world units, under which a cap that has also stopped moving counts as still and " +
            "its view switches itself off. Well under a pixel.")]
        [SerializeField] [Min(0.0001f)] private float _restLean;

        [Tooltip("How far a bomb's blast leans a cap it sways at full strength, in world units, before the cap " +
            "swings back toward the blast. A shot off the cannon leans it 0.14; a blast sways the caps it does " +
            "not take off, the ones two slots out and further, at well under full strength.")]
        [SerializeField] [Min(0f)] private float _swayLean;

        public float LeanPerSpeed => Mathf.Max(0f, _leanPerSpeed);
        public float MaxLean => Mathf.Max(0f, _maxLean);
        public float Frequency => Mathf.Clamp(_frequency, MinFrequency, MaxFrequency);
        public float Damping => Mathf.Clamp(_damping, 0.02f, MaxDamping);
        public float RewardPulse => Mathf.Max(0f, _rewardPulse);
        public float RestLean => Mathf.Max(0.0001f, _restLean);

        /// <summary>The lean of a full-strength sway, never more than the furthest a cap leans.</summary>
        public float SwayLean => Mathf.Clamp(_swayLean, 0f, MaxLean);

        /// <summary>The spring's angular frequency, in radians a second.</summary>
        public float AngularFrequency => Frequency * 2f * Mathf.PI;

        /// <summary>
        ///     The speed a spring at rest must be given to swing out to 1 before it turns back: what a reach is
        ///     multiplied by to get the push that sends the cap that far.
        /// </summary>
        public float KickPerReach
        {
            get
            {
                // An underdamped spring kicked from rest peaks a quarter wobble in, less what the damping has
                // taken by then.
                var damping = Damping;
                var ringing = Mathf.Sqrt(1f - (damping * damping));
                var decay = Mathf.Exp(-damping * Mathf.Atan2(ringing, damping) / ringing);
                return AngularFrequency / decay;
            }
        }

        internal MushroomLeanTuning(
            float leanPerSpeed, float maxLean, float frequency, float damping, float rewardPulse, float restLean,
            float swayLean)
        {
            _swayLean = swayLean;
            _leanPerSpeed = leanPerSpeed;
            _maxLean = maxLean;
            _frequency = frequency;
            _damping = damping;
            _rewardPulse = rewardPulse;
            _restLean = restLean;
        }
    }
}

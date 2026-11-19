using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     How a mushroom's cap flies when a blast blows it off whole (<c>MushroomCutTimeline</c>): how far it is
    ///     thrown, how long it is in the air, how high it is lifted and how far it tumbles. Authored with the
    ///     cut's numbers (<see cref="MushroomCutTuning.Blown" />).
    /// </summary>
    /// <remarks>
    ///     The look only. Which caps a blast takes off, and when they grow back, are the rule's.
    /// </remarks>
    [Serializable]
    internal struct MushroomThrowTuning
    {
        // Under this a throw is over before a frame has shown it.
        private const float MinSeconds = 0.05f;

        // How much further off the cap its shadow lies at the top of the throw, for each share of size the lift
        // adds: the shadow parting from the cap is what reads as height, more than the cap growing does.
        private const float ShadowPerLift = 5f;

        [Tooltip("How long the cap is in the air, from the blast until it has landed and faded out, in seconds.")]
        [SerializeField] [Range(MinSeconds, 3f)] private float _seconds;

        [Tooltip("How far the cap is thrown, away from the blast, in world units. A cap is 0.6 across.")]
        [SerializeField] [Min(0f)] private float _distance;

        [Tooltip("How much larger the cap is drawn at the top of its throw, as a share of its size: seen from " +
            "above, a thing that comes nearer grows. 0.2 is a fifth.")]
        [SerializeField] [Range(0f, 0.6f)] private float _lift;

        [Tooltip("How far the cap turns as it flies, in degrees.")]
        [SerializeField] [Range(0f, 120f)] private float _turnDegrees;

        public float Seconds => Mathf.Max(MinSeconds, _seconds);
        public float Distance => Mathf.Max(0f, _distance);
        public float Lift => Mathf.Clamp(_lift, 0f, 0.6f);
        public float TurnRadians => Mathf.Clamp(_turnDegrees, 0f, 120f) * Mathf.Deg2Rad;

        /// <summary>How many times its resting distance the cap's shadow lies from it at the top of the
        /// throw.</summary>
        public float ShadowLift => 1f + (ShadowPerLift * Lift);

        internal MushroomThrowTuning(float seconds, float distance, float lift, float turnDegrees)
        {
            _seconds = seconds;
            _distance = distance;
            _lift = lift;
            _turnDegrees = turnDegrees;
        }
    }
}

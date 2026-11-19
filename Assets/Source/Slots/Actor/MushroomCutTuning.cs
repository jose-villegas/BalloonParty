using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     How a mushroom's cap falls when a lance cuts it and how it pops back (<c>MushroomCutTimeline</c>): how
    ///     far the two pieces part, turn and shrink, how long they take, and how the cap grows back. Authored on
    ///     the mushroom settings (<c>IMushroomSettings.Cut</c>).
    /// </summary>
    /// <remarks>
    ///     These are the look's numbers only. When a cap is cut and when it grows back are the rule's to say: it
    ///     publishes <c>MushroomCutMessage</c> and <c>MushroomRegrownMessage</c>.
    /// </remarks>
    [Serializable]
    internal struct MushroomCutTuning
    {
        // Under this a fall or a regrow is over before a frame has shown it.
        private const float MinSeconds = 0.05f;

        [Tooltip("How long the two pieces take from the cut until they have faded out, in seconds.")]
        [SerializeField] [Range(MinSeconds, 2f)] private float _fallSeconds;

        [Tooltip("How far each piece ends up from the cut line, to its own side, in world units. A mushroom's " +
            "radius is 0.3.")]
        [SerializeField] [Min(0f)] private float _partDistance;

        [Tooltip("How far the lance carries both pieces along its own way, in world units.")]
        [SerializeField] [Min(0f)] private float _carryDistance;

        [Tooltip("How far each piece turns as it parts, in degrees, one each way.")]
        [SerializeField] [Range(0f, 60f)] private float _turnDegrees;

        [Tooltip("A piece's size when it reaches the ground, as a share of its size on the stem: under 1, since " +
            "from above a thing that falls gets smaller.")]
        [SerializeField] [Range(0.2f, 1f)] private float _endSize;

        [Tooltip("How long the cap takes to pop back, in seconds.")]
        [SerializeField] [Range(MinSeconds, 2f)] private float _regrowSeconds;

        [Tooltip("How far past its own size the cap swells as it pops back, before it settles: 0.1 is a tenth.")]
        [SerializeField] [Range(0f, 0.5f)] private float _regrowOvershoot;

        [Tooltip("A cap a blast blows off is not cut: it is thrown off whole.")]
        [SerializeField] private MushroomThrowTuning _blown;

        public float FallSeconds => Mathf.Max(MinSeconds, _fallSeconds);
        public float PartDistance => Mathf.Max(0f, _partDistance);
        public float CarryDistance => Mathf.Max(0f, _carryDistance);
        public float TurnRadians => Mathf.Clamp(_turnDegrees, 0f, 60f) * Mathf.Deg2Rad;
        public float EndSize => Mathf.Clamp(_endSize, 0.2f, 1f);
        public float RegrowSeconds => Mathf.Max(MinSeconds, _regrowSeconds);
        public float RegrowOvershoot => Mathf.Clamp(_regrowOvershoot, 0f, 0.5f);

        /// <summary>How a cap a blast blows off flies: it comes off whole and is thrown.</summary>
        public MushroomThrowTuning Blown => _blown;

        /// <summary>How many times its resting distance a cap's shadow may lie from it: 1 for a cut cap's
        /// pieces, more for a cap a blast has lifted.</summary>
        public float ShadowLift => Mathf.Max(1f, _blown.ShadowLift);

        /// <summary>
        ///     The room a cap that has come off needs beyond the cap's own radius, in world units, for a mushroom
        ///     of <paramref name="radius" />: what the quad the mushroom is drawn on has to hold, for a lance's
        ///     two pieces or a blast's whole cap, whichever goes further. It is the bound the shader boxes them
        ///     by (its vertex stage's <c>flown</c>: the radius grown by the lift and the turn, plus how far they
        ///     are moved), a sum and not the true furthest point, so the mesh is never smaller than the box the
        ///     shader asks for.
        /// </summary>
        public float RoomBeyond(float radius)
        {
            radius = Mathf.Max(0f, radius);
            var cut = (radius * TurnRadians) + PartDistance + CarryDistance;
            var blown = (radius * (_blown.Lift + _blown.TurnRadians)) + _blown.Distance;
            return Mathf.Max(cut, blown);
        }

        internal MushroomCutTuning(
            float fallSeconds, float partDistance, float carryDistance, float turnDegrees, float endSize,
            float regrowSeconds, float regrowOvershoot, in MushroomThrowTuning blown)
        {
            _blown = blown;
            _fallSeconds = fallSeconds;
            _partDistance = partDistance;
            _carryDistance = carryDistance;
            _turnDegrees = turnDegrees;
            _endSize = endSize;
            _regrowSeconds = regrowSeconds;
            _regrowOvershoot = regrowOvershoot;
        }
    }
}

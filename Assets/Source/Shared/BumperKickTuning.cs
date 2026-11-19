using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The numbers of the forgiving kick (<see cref="BumperKick" />): how wide its cone is, and how far beside a
    ///     mushroom's centre it may land the ball. Authored on the mushroom settings (<c>IMushroomSettings.Kick</c>).
    /// </summary>
    /// <remarks>
    ///     An offset is the ball's impact parameter on a cap as a share of the radius it bounces at
    ///     (<see cref="BumperCircle.Radius" />): 0 is dead centre, 1 a graze. A ball landing at an offset turns by
    ///     <see cref="TurnDegrees" /> there, so the two offsets are also the range of turns a kicked ball can make.
    /// </remarks>
    [Serializable]
    internal struct BumperKickTuning
    {
        // Whatever is authored, the kick never lands dead centre and never aims a graze it could miss.
        private const float OffsetFloor = 0.05f;
        private const float OffsetCeiling = 0.95f;

        [Tooltip("How far from the mirror reflection, in degrees, a cap of another wall may lie and still pull " +
            "the bounce toward it. Wider is more forgiving: it is the dial for how much skill a rally asks. Two " +
            "walls trading the ball need it wider than twice the arcsine of the smallest offset (17 degrees at " +
            "0.15). The baked wall catalogue holds for the cone it was baked with.")]
        [SerializeField] [Range(0f, 90f)] private float _coneDegrees;

        [Tooltip("The nearest to a cap's centre a kicked ball lands, as a share of the radius it bounces at. " +
            "Never the centre: a dead-centre hit comes straight back and two caps would trade the ball forever. " +
            "It sets the sharpest turn a kicked ball can make: 163 degrees at 0.15.")]
        [SerializeField] [Range(OffsetFloor, OffsetCeiling)] private float _minOffset;

        [Tooltip("The farthest from a cap's centre a kicked ball lands, as a share of the radius it bounces " +
            "at. Short of a graze, which a small error would miss. It sets the gentlest turn a kicked ball can " +
            "make: 52 degrees at 0.9.")]
        [SerializeField] [Range(OffsetFloor, OffsetCeiling)] private float _maxOffset;

        public float ConeDegrees => Mathf.Max(0f, _coneDegrees);
        public float MinOffset => Mathf.Clamp(_minOffset, OffsetFloor, OffsetCeiling);
        public float MaxOffset => Mathf.Clamp(_maxOffset, MinOffset, OffsetCeiling);

        /// <summary>The gentlest turn a kicked ball makes at a mushroom, in degrees: the one at
        /// <see cref="MaxOffset" />.</summary>
        public float MinTurnDegrees => TurnDegrees(MaxOffset);

        /// <summary>The sharpest turn a kicked ball makes at a mushroom, in degrees: the one at
        /// <see cref="MinOffset" />.</summary>
        public float MaxTurnDegrees => TurnDegrees(MinOffset);

        internal BumperKickTuning(float coneDegrees, float minOffset, float maxOffset)
        {
            _coneDegrees = coneDegrees;
            _minOffset = minOffset;
            _maxOffset = maxOffset;
        }

        /// <summary>
        ///     How far a ball landing at <paramref name="offset" /> turns, in degrees between the way it came in
        ///     and the way it leaves: 180 (straight back) at the centre, 0 (straight on) at a graze.
        /// </summary>
        internal static float TurnDegrees(float offset)
        {
            return 180f - (2f * Mathf.Asin(Mathf.Clamp01(offset)) * Mathf.Rad2Deg);
        }
    }
}

using NaughtyAttributes;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     A <see cref="SightRampReaction"/> that orbits a transform around a pivot — a local-space offset
    ///     from its rest position — spinning at one end of the aim and settling on a damper at the other:
    ///     by default it free-spins off-aim and settles as the shot's aim centres, or set
    ///     <see cref="_spinOnSight"/> to invert (still off-aim, spins up as the aim centres). Two settle
    ///     styles (<see cref="_returnToRest"/>): HALT eases the spin rate to zero so it stops wherever it
    ///     is; RETURN eases the orbit angle home so it unwinds to its original rotation. The object turns
    ///     rigidly about the pivot (position circles it, orientation turns with it); a zero offset spins in
    ///     place.
    /// </summary>
    internal sealed class SightSpinReaction : SightRampReaction
    {
        [Tooltip("Transform that orbits. Defaults to this object if left unset.")]
        [SerializeField] private Transform _spinTarget;

        [Tooltip("Pivot as a local-space offset from the target's rest position (0 = spins in place).")]
        [SerializeField] private Vector2 _pivotOffset;

        [Tooltip("Spin rate in degrees/second at full spin authority.")]
        [SerializeField] private float _spinSpeed = 90f;

        [Tooltip("Spin WHEN sighted instead of when off-aim — inverts which end of the aim spins vs settles.")]
        [SerializeField] private bool _spinOnSight;

        [Tooltip("While settling, unwind to the ORIGINAL rotation (return) instead of halting wherever it is (off).")]
        [SerializeField] private bool _returnToRest;

        [Tooltip("Seconds to ease the spin rate to a halt while settling (SmoothDamp).")]
        [HideIf(nameof(_returnToRest))]
        [SerializeField] private float _smoothTime = 0.25f;

        [Tooltip("Seconds to ease the unwind home to the original rotation while settling (SmoothDamp).")]
        [ShowIf(nameof(_returnToRest))]
        [SerializeField] private float _returnTime = 0.4f;

        private Vector3 _restLocalPosition;
        private Quaternion _restLocalRotation;
        private float _angle;
        private float _currentSpeed;
        private float _speedVelocity;
        private float _angleVelocity;

        protected override void Awake()
        {
            base.Awake();

            if (_spinTarget == null)
            {
                _spinTarget = transform;
            }

            _restLocalPosition = _spinTarget.localPosition;
            _restLocalRotation = _spinTarget.localRotation;
        }

        protected override void OnSightTick(float centrality)
        {
            if (_spinTarget == null)
            {
                return;
            }

            // Spin authority: how much it should be spinning right now (1 = full, 0 = settled). Off-aim by
            // default, inverted to spin-on-sight by _spinOnSight; `settle` is the complement.
            var authority = _spinOnSight ? centrality : 1f - centrality;
            var settle = 1f - authority;

            if (_returnToRest)
            {
                // Advance fades out as it settles, and the accumulated angle eases home to rest — so it
                // unwinds to its original rotation rather than freezing mid-orbit.
                _angle += _spinSpeed * authority * Time.unscaledDeltaTime;
                _angle = WrapAngle(_angle);

                // Home over _returnTime at full settle; scale the time UP (weaker homing) as spin authority
                // returns, so it fades toward the free spin. _returnTime governs the pace directly.
                if (settle > 1e-4f)
                {
                    _angle = Mathf.SmoothDamp(_angle, 0f, ref _angleVelocity, _returnTime / settle);
                }
            }
            else
            {
                // Halt: the spin RATE damps to zero as it settles — it stops wherever it is.
                var targetSpeed = _spinSpeed * authority;
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, _smoothTime);
                _angle = WrapAngle(_angle + _currentSpeed * Time.unscaledDeltaTime);
            }

            ApplyAngle(_angle);
        }

        protected override void ResetReaction()
        {
            _angle = 0f;
            _currentSpeed = 0f;
            _speedVelocity = 0f;
            _angleVelocity = 0f;

            if (_spinTarget != null)
            {
                _spinTarget.localPosition = _restLocalPosition;
                _spinTarget.localRotation = _restLocalRotation;
            }
        }

        // Absolute angle from rest → rigid orbit about the pivot. Position circles the pivot; orientation
        // turns with it. Angle 0 restores the exact rest pose, which is what RETURN mode eases toward.
        private void ApplyAngle(float angle)
        {
            var pivot = _restLocalPosition + (Vector3)_pivotOffset;
            var rotation = Quaternion.Euler(0f, 0f, angle);
            _spinTarget.localPosition = pivot + rotation * (_restLocalPosition - pivot);
            _spinTarget.localRotation = rotation * _restLocalRotation;
        }

        // Keep the angle in (-180, 180] so it stays bounded (seamless — same orientation mod 360) and
        // RETURN homes to 0 the short way rather than unwinding every accumulated turn.
        private static float WrapAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }
    }
}

using NaughtyAttributes;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     A <see cref="SightRampReaction"/> that orbits a transform around a pivot — a local-space offset
    ///     from its rest position — spinning freely when off-aim and settling on a damper as the shot's aim
    ///     centres on the item. Two settle styles (<see cref="_returnToRestOnSight"/>): HALT eases the spin
    ///     rate to zero so it stops wherever it is; RETURN eases the orbit angle home so it unwinds to its
    ///     original rotation. The object turns rigidly about the pivot (its position circles it and its
    ///     orientation turns with it); a zero offset spins it in place.
    /// </summary>
    internal sealed class SightSpinReaction : SightRampReaction
    {
        [Tooltip("Transform that orbits. Defaults to this object if left unset.")]
        [SerializeField] private Transform _spinTarget;

        [Tooltip("Pivot as a local-space offset from the target's rest position (0 = spins in place).")]
        [SerializeField] private Vector2 _pivotOffset;

        [Tooltip("Free spin rate in degrees/second when off-aim.")]
        [SerializeField] private float _spinSpeed = 90f;

        [Tooltip("Seconds to ease the spin rate to a halt as the aim centres (SmoothDamp).")]
        [HideIf(nameof(_returnToRestOnSight))]
        [SerializeField] private float _smoothTime = 0.25f;

        [Tooltip("On sight, unwind to the ORIGINAL rotation (return) instead of halting wherever it is (off).")]
        [SerializeField] private bool _returnToRestOnSight;

        [Tooltip("Seconds to ease the unwind home to the original rotation (SmoothDamp) — the return time.")]
        [ShowIf(nameof(_returnToRestOnSight))]
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

            if (_returnToRestOnSight)
            {
                // Free advance fades out as the aim centres, and the accumulated angle eases home to rest —
                // so a sighted item unwinds to its original rotation rather than freezing mid-orbit.
                _angle += _spinSpeed * (1f - centrality) * Time.unscaledDeltaTime;
                _angle = WrapAngle(_angle);

                // Home over _returnTime when dead-centre; scale the time UP (weaker homing) as the aim
                // drifts off so it fades toward the free spin. _returnTime governs the pace directly
                // instead of being swamped by a fixed slack — a small value snaps home as authored.
                if (centrality > 1e-4f)
                {
                    _angle = Mathf.SmoothDamp(_angle, 0f, ref _angleVelocity, _returnTime / centrality);
                }
            }
            else
            {
                // Halt: the spin RATE damps to zero as the aim centres — it stops wherever it is.
                var targetSpeed = _spinSpeed * (1f - centrality);
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

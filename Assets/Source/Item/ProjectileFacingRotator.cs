using BalloonParty.Prediction;
using BalloonParty.Projectile;
using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Rotates an item icon to face the loaded/flying shot — aim direction while it's being lined up,
    ///     the strike point while it's in flight, an idle spin otherwise. Sits on a pooled item visual
    ///     (like <see cref="LaserItemRotation"/>), so it isn't DI-injected: the host (ItemDisplayService)
    ///     hands it an <see cref="IProjectileFacingSource"/> via <see cref="Configure"/> after activation.
    /// </summary>
    public class ProjectileFacingRotator : MonoBehaviour
    {
        [Tooltip("Transform to rotate. Defaults to this object if left unset.")]
        [SerializeField] private Transform _target;

        [Tooltip("Seconds to ease into the target facing (SmoothDampAngle smoothing time; ~0 = near-instant).")]
        [SerializeField] private float _timeToAlign;

        [Tooltip("World radius for the Proximity align-timing gate — only aligns while the shot is within this distance.")]
        [SerializeField] private float _proximityRadius;

        [Tooltip("World radius of the circle tested against the aim prediction trace for AlignPredictionHit — " +
                 "the aim must pass within this of the target to align.")]
        [SerializeField] private float _predictionHitRadius;

        [Tooltip("Idle spin rate in degrees/second (Spin uses it directly; SpinRandom jitters sign and magnitude around it).")]
        [SerializeField] private float _idleSpinSpeed;

        [Tooltip("Degrees added after atan2(direction) — set this to match the sprite's authored " +
                 "forward axis (0 = local +X faces the direction; the thrower uses -90 for a sprite " +
                 "drawn nose-up).")]
        [SerializeField] private float _facingOffsetDegrees;

        [SerializeField] private AimMode _aimMode;
        [SerializeField] private FlightMode _flightMode;
        [SerializeField] private AlignTiming _alignTiming;
        [SerializeField] private IdleMode _idleMode;
        [SerializeField] private SpinSpace _spinSpace;

        private IProjectileFacingSource _source;
        private float _currentAngleDeg;
        private float _angleVelocity;
        private bool _wasIdle;
        private float _idleSpinSpeedActual;
        private Vector3 _idleAxis;

        private void Awake()
        {
            if (_target == null)
            {
                _target = transform;
            }
        }

        // Re-armed on every pooled reactivation, mirroring LaserItemRotation's OnEnable reset — starting
        // from the icon's current facing (rather than snapping to zero) keeps the very first align smooth.
        private void OnEnable()
        {
            _currentAngleDeg = _target.localEulerAngles.z;
            _angleVelocity = 0f;
            _wasIdle = false;
        }

        private void LateUpdate()
        {
            if (_source == null)
            {
                RunIdle();
                return;
            }

            if (_source.IsFlying && _flightMode == FlightMode.AlignProjectileDirection && PassesAlignTiming())
            {
                Align(_source.Direction);
                return;
            }

            if (_source.IsAiming && TryGetAimDirection(out var aimDirection))
            {
                Align(aimDirection);
                return;
            }

            RunIdle();
        }

        // Called by the host each time the icon is shown (the pool channel doesn't DI-inject). Null-safe:
        // clearing the source (or never setting one) just leaves the rotator idling.
        internal void Configure(IProjectileFacingSource source)
        {
            _source = source;
        }

        private bool PassesAlignTiming()
        {
            switch (_alignTiming)
            {
                case AlignTiming.Always:
                    return true;

                case AlignTiming.Proximity:
                    return _target.position.WithinRadius(_source.ProjectilePosition, _proximityRadius);

                case AlignTiming.Incoming:
                default:
                    var towardTarget = (Vector2)_target.position - (Vector2)_source.ProjectilePosition;
                    return Vector2.Dot(_source.Direction, towardTarget) > 0f;
            }
        }

        private bool TryGetAimDirection(out Vector2 direction)
        {
            direction = Vector2.zero;

            switch (_aimMode)
            {
                case AimMode.AlignAimDirection:
                    direction = _source.Direction;
                    return true;

                case AimMode.AlignPredictionHit:
                    return TryGetPredictionHitDirection(out direction);

                case AimMode.None:
                default:
                    return false;
            }
        }

        // The hit test reports both WHERE the trace crosses our circle and the exact travel direction of
        // the segment it struck, so we align straight to that strike direction — no separate lookup.
        private bool TryGetPredictionHitDirection(out Vector2 direction)
        {
            return TraceHitGeometry.TryFindSurfaceHit(
                _source.PredictionPoints, _target.position, _predictionHitRadius, out _, out _, out direction);
        }

        private void Align(Vector2 direction)
        {
            _wasIdle = false;

            if (direction.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            var targetAngleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + _facingOffsetDegrees;
            _currentAngleDeg = Mathf.SmoothDampAngle(_currentAngleDeg, targetAngleDeg, ref _angleVelocity, _timeToAlign);
            ApplyAngle(_currentAngleDeg);
        }

        private void RunIdle()
        {
            if (_idleMode == IdleMode.None)
            {
                return;
            }

            if (!_wasIdle)
            {
                EnterIdle();
                _wasIdle = true;
            }

            var deltaAngle = _idleSpinSpeedActual * Time.deltaTime;

            if (_spinSpace == SpinSpace.ThreeD)
            {
                _target.Rotate(_idleAxis, deltaAngle, Space.Self);
                return;
            }

            _currentAngleDeg += deltaAngle;
            ApplyAngle(_currentAngleDeg);
        }

        // Fixes the idle spin's speed/direction (SpinRandom) and 3D axis (SpinSpace.ThreeD) once per
        // idle entry, rather than re-rolling every frame, so the spin reads as one continuous motion.
        private void EnterIdle()
        {
            if (_idleMode == IdleMode.SpinRandom)
            {
                var sign = Random.value < 0.5f ? -1f : 1f;
                var magnitude = Random.Range(0.5f, 1.5f) * Mathf.Abs(_idleSpinSpeed);
                _idleSpinSpeedActual = sign * magnitude;
            }
            else
            {
                _idleSpinSpeedActual = _idleSpinSpeed;
            }

            _idleAxis = _spinSpace == SpinSpace.ThreeD ? Random.onUnitSphere : Vector3.forward;
        }

        private void ApplyAngle(float angleDeg)
        {
            _target.localRotation = Quaternion.AngleAxis(angleDeg, Vector3.forward);
        }
    }
}

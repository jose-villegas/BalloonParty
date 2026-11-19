using System.Collections.Generic;
using BalloonParty.Prediction;
using BalloonParty.Scenario;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.SceneLight;
using DG.Tweening;
using UnityEngine;
using VContainer;

namespace BalloonParty.Thrower
{
    public class ThrowerView : MonoBehaviour
    {
        [SerializeField] private Transform _projectileSpawnPoint;
        [SerializeField] private float _entranceDuration = 1f;
        [SerializeField] private Vector3 _entranceOffset = new(0f, 0.5f, 0f);

        [Header("Fire recoil")]
        [SerializeField] private float _recoilDistance = 0.15f;
        [SerializeField] private float _recoilDuration = 0.18f;
        [SerializeField] private int _recoilVibrato = 8;

        [Tooltip("The whirlwind that plays when a ball loads Snipe-armed (the Sharpshooter card).")]
        [SerializeField] private SnipeWhirlwindView _snipeWhirlwind;

        private PredictionTraceView _traceView;
        private Camera _camera;
        private Tween _recoilTween;

        public Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public Vector3 SpawnPointPosition => _projectileSpawnPoint.position;

        internal Transform SpawnPoint => _projectileSpawnPoint;

        /// <summary>
        ///     Where the cannon rests once <see cref="AnimateEntrance" /> has played: where it stands now, moved by
        ///     the entrance's offset. Only true before the entrance starts, which is when the controller reads it.
        /// </summary>
        internal Vector3 PositionAfterEntrance => transform.position + _entranceOffset;

        public Quaternion Rotation => transform.rotation;

        public bool IsAiming => Input.GetMouseButton(0);

        public bool FireReleased => Input.GetMouseButtonUp(0) && Input.touchCount < 3;

        private void Awake()
        {
            _traceView = GetComponentInChildren<PredictionTraceView>(true);
        }

        [Inject]
        internal void Construct(
            SceneLightFieldService lightField, DisturbanceFieldService disturbanceField, SmokeFieldService smokeField)
        {
            if (_snipeWhirlwind != null)
            {
                _snipeWhirlwind.Bind(lightField, disturbanceField, smokeField);
            }
        }

        public Tween AnimateEntrance()
        {
            return transform.DOMove(transform.position + _entranceOffset, _entranceDuration);
        }

        public void ClearTrace()
        {
            _traceView?.Clear();
        }

        // A short positional kick back along the fire heading, settling to rest. Complete() any in-flight kick
        // first so rapid fire doesn't drift the rest position; SetLink kills it if the thrower is destroyed.
        public void PlayRecoil(Vector3 fireDirection)
        {
            _recoilTween?.Complete();
            _recoilTween = transform
                .DOPunchPosition(-fireDirection.normalized * _recoilDistance, _recoilDuration, _recoilVibrato)
                .SetLink(gameObject);
        }

        public void PlaySnipeWhirlwind(Color color, int skyLightPaletteIndex, float reloadSeconds)
        {
            if (_snipeWhirlwind != null)
            {
                _snipeWhirlwind.Play(color, skyLightPaletteIndex, reloadSeconds);
            }
        }

        public void RotateTo(Vector3 direction)
        {
            // Signed angle (Atan2), so aiming below the horizontal rotates the correct way instead of
            // mirroring — Vector3.Angle is unsigned and can't tell up from down.
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        /// <summary>Returns false when no camera is available.</summary>
        public bool TryGetAimDirection(out Vector3 direction)
        {
            _camera ??= Camera.main;
            if (_camera == null)
            {
                direction = Vector3.zero;
                return false;
            }

            var screenPos = _camera.WorldToScreenPoint(transform.position);
            var rawDir = (Input.mousePosition - screenPos).normalized;
            rawDir.z = 0f;
            direction = rawDir;
            return true;
        }

        public void SetTrace(IReadOnlyList<Vector3> points)
        {
            if (_traceView == null)
            {
                return;
            }

            _traceView.SetTrace(points);
        }

        public void SetTraceColor(Color color)
        {
            _traceView?.SetColor(color);
        }
    }
}

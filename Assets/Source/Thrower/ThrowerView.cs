using System.Collections.Generic;
using BalloonParty.Prediction;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Thrower
{
    public class ThrowerView : MonoBehaviour
    {
        [SerializeField] private Transform _projectileSpawnPoint;
        [SerializeField] private float _entranceDuration = 1f;
        [SerializeField] private Vector3 _entranceOffset = new(0f, 0.5f, 0f);

        private PredictionTraceView _traceView;
        private Camera _camera;

        public Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public Vector3 SpawnPointPosition => _projectileSpawnPoint.position;

        public Quaternion Rotation => transform.rotation;

        public bool IsAiming => Input.GetMouseButton(0);

        public bool FireReleased => Input.GetMouseButtonUp(0);

        private void Awake()
        {
            _traceView = GetComponentInChildren<PredictionTraceView>(true);
        }

        public Tween AnimateEntrance()
        {
            return transform.DOMove(transform.position + _entranceOffset, _entranceDuration);
        }

        public void ClearTrace()
        {
            _traceView?.Clear();
        }

        public void RotateTo(Vector3 direction)
        {
            var angle = Vector3.Angle(direction, Vector3.right) - 90f;
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
    }
}

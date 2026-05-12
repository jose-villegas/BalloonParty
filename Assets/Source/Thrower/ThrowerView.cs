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

        public Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public Vector3 SpawnPointPosition => _projectileSpawnPoint.position;

        public Quaternion Rotation => transform.rotation;

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

        public void SetTrace(List<Vector3> points)
        {
            if (_traceView == null)
            {
                return;
            }

            _traceView.SetTrace(points);
        }
    }
}

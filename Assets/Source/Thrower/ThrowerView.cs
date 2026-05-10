using BalloonParty.Prediction;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Thrower
{
    public class ThrowerView : MonoBehaviour
    {
        private PredictionTraceView _traceView;

        public Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        private void Awake()
        {
            _traceView = GetComponentInChildren<PredictionTraceView>(true);
        }

        public void RotateTo(Vector3 direction)
        {
            var angle = Vector3.Angle(direction, Vector3.right) - 90f;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public Tween AnimateEntrance(Vector2 target, float duration)
        {
            return transform.DOMove(target, duration);
        }

        public void SetTrace(System.Collections.Generic.List<Vector3> points)
        {
            if (_traceView == null)
            {
                return;
            }

            _traceView.SetTrace(points);
        }

        public void ClearTrace()
        {
            _traceView?.Clear();
        }
    }
}


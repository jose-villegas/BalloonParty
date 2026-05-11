using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Prediction
{
    [RequireComponent(typeof(LineRenderer))]
    public class PredictionTraceView : MonoBehaviour
    {
        private LineRenderer _lineRenderer;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        public void SetTrace(List<Vector3> points)
        {
            if (points == null || points.Count == 0)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            _lineRenderer.positionCount = points.Count;
            _lineRenderer.SetPositions(points.ToArray());
        }

        public void Clear()
        {
            _lineRenderer.positionCount = 0;
        }
    }
}

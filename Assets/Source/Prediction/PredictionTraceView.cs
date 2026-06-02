using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Prediction
{
    [RequireComponent(typeof(LineRenderer))]
    public class PredictionTraceView : MonoBehaviour
    {
        private LineRenderer _lineRenderer;
        private Vector3[] _positionBuffer = Array.Empty<Vector3>();

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        public void SetTrace(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            if (_positionBuffer.Length < points.Count)
            {
                _positionBuffer = new Vector3[points.Count];
            }

            for (var i = 0; i < points.Count; i++)
            {
                _positionBuffer[i] = points[i];
            }
            _lineRenderer.positionCount = points.Count;
            _lineRenderer.SetPositions(_positionBuffer);
        }

        public void Clear()
        {
            _lineRenderer.positionCount = 0;
        }
    }
}

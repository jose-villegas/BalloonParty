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
        private GradientAlphaKey[] _baseAlphaKeys;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();

            // Cached before SetColor ever touches the renderer, so re-tinting can recover the
            // authored alpha fade over the line's length instead of losing it — startColor/endColor
            // would otherwise overwrite colorGradient with a flat, alpha-less 2-key gradient.
            _baseAlphaKeys = _lineRenderer.colorGradient.alphaKeys;
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

        // RGB is the flat config colour, same as startColor/endColor used to give; alpha is the
        // authored gradient's own shape, recovered from Awake's cache rather than flattened.
        public void SetColor(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                _baseAlphaKeys);
            _lineRenderer.colorGradient = gradient;
        }

        public void Clear()
        {
            _lineRenderer.positionCount = 0;
        }
    }
}

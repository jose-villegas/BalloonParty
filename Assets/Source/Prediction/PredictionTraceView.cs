using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Prediction
{
    /// <summary>
    /// Draws the prediction trace using a <see cref="LineRenderer"/>.
    /// Attach to the same GameObject that holds the LineRenderer (e.g. the Thrower).
    /// Replaces the legacy Entitas <c>TraceDrawerController</c>.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PredictionTraceView : MonoBehaviour
    {
        private LineRenderer _lineRenderer;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        /// <summary>
        /// Updates the line renderer with the given trace points.
        /// Pass null or empty to hide the line.
        /// </summary>
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

        /// <summary>Hides the prediction line.</summary>
        public void Clear()
        {
            _lineRenderer.positionCount = 0;
        }
    }
}


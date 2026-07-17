#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Draws a polyline (plus a ring at every vertex) as gizmos, so editor tooling can show a
    ///     world-space path in the GAME view — EditorWindow Handles reach the Scene view only, and
    ///     Unity refuses to AddComponent editor-assembly scripts, so this lives here behind the
    ///     UNITY_EDITOR guard. Spawn it on a hidden play-mode object and feed it via SetPath.
    /// </summary>
    internal sealed class WorldPolylineGizmo : MonoBehaviour
    {
        private const float VertexRadius = 0.1f;

        private readonly List<Vector3> _points = new();

        private Color _color = Color.red;
        private float _worldThickness;

        private void OnDrawGizmos()
        {
            GizmoDrawingHelper.DrawWorldPolyline(_points, _color, _worldThickness);

            // Ring every event point (bounces, hits) — a path's direction changes are what callers
            // are showing off, so make them unmissable.
            for (var i = 0; i < _points.Count; i++)
            {
                GizmoDrawingHelper.DrawWireSphere(_points[i], VertexRadius, _color);
            }
        }

        internal void SetPath(IReadOnlyList<Vector2> path, Color color, float worldThickness)
        {
            _points.Clear();
            for (var i = 0; i < path.Count; i++)
            {
                _points.Add(path[i]);
            }

            _color = color;
            _worldThickness = worldThickness;
        }
    }
}
#endif

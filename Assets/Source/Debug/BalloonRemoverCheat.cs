#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace BalloonParty.Debug
{
    public class BalloonRemoverCheat : MonoBehaviour, ICheat
    {
        private const float PickRadius = 0.25f;
        private const float PathSampleDistance = 0.05f;

        [Inject] private SlotGrid _grid;
        [Inject] private IPublisher<BalloonHitMessage> _hitPublisher;
        [Inject] private IPublisher<BalanceBalloonsMessage> _publisher;

        private readonly List<Vector3> _path = new();
        private bool _active;
        private bool _dragging;
        private Material _lineMaterial;

        public string Name => _active ? "Remove Balloons  [ON]" : "Remove Balloons";
        public string Section => "Grid";
        public IReadOnlyList<string> Tags => new[] { "balloons", "grid" };

        public void Execute()
        {
            _active = !_active;
        }

        private void Awake()
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        private void Update()
        {
            if (!_active) return;

            if (Input.GetMouseButtonDown(0))
            {
                _path.Clear();
                _dragging = true;
            }

            if (_dragging && Input.GetMouseButton(0))
                SampleMousePosition();

            if (_dragging && Input.GetMouseButtonUp(0))
            {
                RemoveBalloonsAlongPath();
                _path.Clear();
                _dragging = false;
            }
        }

        private void OnRenderObject()
        {
            if (!_active || _path.Count < 2) return;

            _lineMaterial.SetPass(0);

            var hitSlots = CollectHitSlots();
            var cam = Camera.main;
            if (cam == null) return;

            GL.PushMatrix();

            DrawThickPath(_path, new Color(1f, 0.3f, 0.1f, 0.8f), 0.06f);

            foreach (var slot in hitSlots)
                DrawThickCircle(_grid.IndexToWorldPosition(slot), PickRadius, 24, new Color(1f, 0.1f, 0.1f, 0.9f),
                    0.05f);

            GL.PopMatrix();
        }

        private void SampleMousePosition()
        {
            var pos = MouseWorldPosition();
            if (pos == null) return;

            if (_path.Count == 0 || Vector3.Distance(pos.Value, _path[_path.Count - 1]) >= PathSampleDistance)
                _path.Add(pos.Value);
        }

        private void RemoveBalloonsAlongPath()
        {
            var hitSlots = CollectHitSlots();
            if (hitSlots.Count == 0) return;

            foreach (var slot in hitSlots)
            {
                var model = _grid.At(slot);
                if (model != null)
                    _hitPublisher.Publish(new BalloonHitMessage(model, _grid.IndexToWorldPosition(slot)));
            }

            _publisher.Publish(default);
        }

        private HashSet<Vector2Int> CollectHitSlots()
        {
            var hit = new HashSet<Vector2Int>();

            for (var col = 0; col < _grid.Columns; col++)
            for (var row = 0; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row)) continue;

                var balloonWorld = _grid.IndexToWorldPosition(new Vector2Int(col, row));

                foreach (var point in _path)
                    if (Vector2.Distance(point, balloonWorld) <= PickRadius)
                    {
                        hit.Add(new Vector2Int(col, row));
                        break;
                    }
            }

            return hit;
        }

        private static void DrawThickPath(List<Vector3> path, Color color, float halfWidth)
        {
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);

            for (var i = 0; i < path.Count - 1; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                var dir = (b - a).normalized;
                // perpendicular offset in world space scaled by camera ortho size
                var perp = new Vector3(-dir.y, dir.x, 0f) * halfWidth;

                GL.Vertex(a - perp);
                GL.Vertex(a + perp);
                GL.Vertex(b + perp);
                GL.Vertex(a - perp);
                GL.Vertex(b + perp);
                GL.Vertex(b - perp);
            }

            GL.End();
        }

        private static void DrawThickCircle(Vector3 center, float radius, int segments, Color color, float halfWidth)
        {
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);

            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var p1 = center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                var out0 = center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * (radius + halfWidth);
                var out1 = center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * (radius + halfWidth);

                GL.Vertex(p0);
                GL.Vertex(out0);
                GL.Vertex(out1);
                GL.Vertex(p0);
                GL.Vertex(out1);
                GL.Vertex(p1);
            }

            GL.End();
        }

        private static Vector3? MouseWorldPosition()
        {
            var cam = Camera.main;
            if (cam == null) return null;

            // For 2D orthographic: XY is correct with any z input; flatten result to z=0.
            var world = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
            world.z = 0f;
            return world;
        }
    }
}
#endif

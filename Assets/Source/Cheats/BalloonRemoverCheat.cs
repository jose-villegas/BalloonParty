#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

using System.Collections.Generic;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace BalloonParty.Cheats
{
    public class BalloonRemoverCheat : MonoBehaviour, ICheat
    {
        private const float PickRadius = 0.25f;
        private const float PathSampleDistance = 0.05f;

        // Cheat overlay is always-on-top by design: topmost gameplay sorting layer, above the GI overlay (32000).
        private const string OverlaySortingLayer = "Sky";
        private const int OverlaySortingOrder = 32700;

        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        [Inject] private IPublisher<BalanceBalloonsMessage> _publisher;
        [Inject] private IHitDispatcher _hitDispatcher;
        [Inject] private SlotGrid _grid;

        private readonly List<Vector3> _path = new();
        private readonly List<Vector3> _overlayVertices = new();
        private readonly List<Color> _overlayColors = new();
        private readonly List<int> _overlayIndices = new();

        private bool _active;
        private bool _dragging;
        private Material _lineMaterial;
        private Mesh _overlayMesh;
        private MeshRenderer _overlayRenderer;

        public string Name => _active ? "Remove Balloons  [ON]" : "Remove Balloons";
        public string Section => "Grid";
        public IReadOnlyList<string> Tags => new[] { "balloons", "grid" };

        private void Awake()
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _lineMaterial.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt(CullId, (int)CullMode.Off);
            _lineMaterial.SetInt(ZWriteId, 0);

            // Sorting layer/order on the renderer put the overlay on top; the queue only keeps it late within that bucket.
            _lineMaterial.renderQueue = 4000;
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                _path.Clear();
                _dragging = true;
            }

            if (_dragging && Input.GetMouseButton(0))
            {
                SampleMousePosition();
            }

            if (_dragging && Input.GetMouseButtonUp(0))
            {
                RemoveBalloonsAlongPath();
                _path.Clear();
                _dragging = false;
            }
        }

        private void LateUpdate()
        {
            if (!_active || _path.Count < 2)
            {
                if (_overlayRenderer != null)
                {
                    _overlayRenderer.enabled = false;
                }

                return;
            }

            EnsureOverlayRenderer();
            RebuildOverlayMesh();
            _overlayRenderer.enabled = true;
        }

        private void OnDisable()
        {
            // LateUpdate stops running with the component; hide the overlay so it can't freeze on-screen.
            if (_overlayRenderer != null)
            {
                _overlayRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_overlayMesh != null)
            {
                Destroy(_overlayMesh);
            }

            if (_overlayRenderer != null)
            {
                Destroy(_overlayRenderer.gameObject);
            }
        }

        public void Execute()
        {
            _active = !_active;
        }

        private void SampleMousePosition()
        {
            var pos = CheatInput.MouseWorldPosition();
            if (pos == null)
            {
                return;
            }

            if (_path.Count == 0 || Vector3.Distance(pos.Value, _path[_path.Count - 1]) >= PathSampleDistance)
            {
                _path.Add(pos.Value);
            }
        }

        private void RemoveBalloonsAlongPath()
        {
            var hitSlots = CollectHitSlots();
            if (hitSlots.Count == 0)
            {
                return;
            }

            foreach (var slot in hitSlots)
            {
                var actor = _grid.At(slot);
                if (actor != null)
                {
                    _hitDispatcher.Dispatch(new ActorHitMessage(actor,
                        _grid.IndexToWorldPosition(slot),
                        Vector3.zero,
                        HitOutcome.Pop,
                        new DamageContext(1)));
                }
            }

            _publisher.Publish(default);
        }

        private HashSet<Vector2Int> CollectHitSlots()
        {
            var hit = new HashSet<Vector2Int>();

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var balloonWorld = _grid.IndexToWorldPosition(new Vector2Int(col, row));
                    if (PathHits(balloonWorld))
                    {
                        hit.Add(new Vector2Int(col, row));
                    }
                }
            }

            return hit;
        }

        private bool PathHits(Vector3 balloonWorld)
        {
            foreach (var point in _path)
            {
                if (Vector2.Distance(point, balloonWorld) <= PickRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureOverlayRenderer()
        {
            if (_overlayRenderer != null)
            {
                return;
            }

            _overlayMesh = new Mesh { name = "BalloonRemoverOverlay" };
            _overlayMesh.MarkDynamic();

            // Unparented, identity transform: mesh vertices are world-space and must not be re-transformed.
            var overlay = new GameObject("BalloonRemoverOverlay")
            {
                layer = gameObject.layer
            };

            var filter = overlay.AddComponent<MeshFilter>();
            filter.sharedMesh = _overlayMesh;

            _overlayRenderer = overlay.AddComponent<MeshRenderer>();
            _overlayRenderer.sharedMaterial = _lineMaterial;
            _overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
            _overlayRenderer.sortingLayerName = OverlaySortingLayer;
            _overlayRenderer.sortingOrder = OverlaySortingOrder;
        }

        private void RebuildOverlayMesh()
        {
            _overlayVertices.Clear();
            _overlayColors.Clear();
            _overlayIndices.Clear();

            AppendThickPath(_path, new Color(1f, 0.3f, 0.1f, 0.8f), 0.06f);

            foreach (var slot in CollectHitSlots())
            {
                AppendThickCircle(_grid.IndexToWorldPosition(slot),
                    PickRadius,
                    24,
                    new Color(1f, 0.1f, 0.1f, 0.9f),
                    0.05f);
            }

            _overlayMesh.Clear();
            _overlayMesh.SetVertices(_overlayVertices);
            _overlayMesh.SetColors(_overlayColors);
            _overlayMesh.SetTriangles(_overlayIndices, 0);
            _overlayMesh.RecalculateBounds();
        }

        private void AppendThickPath(IReadOnlyList<Vector3> path, Color color, float halfWidth)
        {
            for (var i = 0; i < path.Count - 1; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                var dir = (b - a).normalized;
                var perp = dir.PerpendicularXY() * halfWidth;

                AppendQuad(a - perp, a + perp, b + perp, b - perp, color);
            }
        }

        private void AppendThickCircle(Vector3 center, float radius, int segments, Color color, float halfWidth)
        {
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 dir0 = VectorMathExtensions.DirectionFromAngle(a0);
                Vector3 dir1 = VectorMathExtensions.DirectionFromAngle(a1);
                var p0 = center + dir0 * radius;
                var p1 = center + dir1 * radius;
                var out0 = center + dir0 * (radius + halfWidth);
                var out1 = center + dir1 * (radius + halfWidth);

                AppendQuad(p0, out0, out1, p1, color);
            }
        }

        private void AppendQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            var baseIndex = _overlayVertices.Count;

            _overlayVertices.Add(a);
            _overlayVertices.Add(b);
            _overlayVertices.Add(c);
            _overlayVertices.Add(d);

            _overlayColors.Add(color);
            _overlayColors.Add(color);
            _overlayColors.Add(color);
            _overlayColors.Add(color);

            _overlayIndices.Add(baseIndex);
            _overlayIndices.Add(baseIndex + 1);
            _overlayIndices.Add(baseIndex + 2);
            _overlayIndices.Add(baseIndex);
            _overlayIndices.Add(baseIndex + 2);
            _overlayIndices.Add(baseIndex + 3);
        }

    }
}
#endif

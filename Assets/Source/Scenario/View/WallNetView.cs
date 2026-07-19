using System.Collections.Generic;
using BalloonParty.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     Builds and owns the four procedural net-strip meshes that frame the play area — one per edge
    ///     of the logical play rectangle (<see cref="IGameConfiguration.LimitsClockwise" />, the same
    ///     rectangle the projectile billiard reflects off). Each strip is a flat quad band laid OUTWARD
    ///     from its wall (away from the play area, so its reveal never covers the balloons), tessellated
    ///     finely enough that the shared net material can billow it in the vertex stage without a CPU sim.
    ///     The meshes are built once at startup and never rewritten from C#; every vertex carries its
    ///     edge's outward normal so the shader can un-extrude to a thin line at rest and displace along it.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class WallNetView : MonoBehaviour
    {
        private static readonly int StripWidthId = Shader.PropertyToID("_StripWidth");

        [SerializeField] private Material _netMaterial;

        [Tooltip("Band width laid inward from each edge, in world units.")]
        [SerializeField] private float _stripWidth = 0.5f;

        [Tooltip("Quad rows along the edge per world unit — geometry tessellation for a smooth billow.")]
        [SerializeField] private float _cellsPerUnitAlong = 2f;

        [Tooltip("Quad rows across the strip width — geometry tessellation.")]
        [SerializeField] private int _cellsAcross = 4;

        [Tooltip("Net-pattern cells across the strip width; the along-tiling is derived to keep cells square.")]
        [SerializeField] private int _netCellsAcross = 3;

        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Sky";
        [SerializeField] private int _sortingOrder;

        private readonly List<Mesh> _meshes = new();

        private IGameConfiguration _config;

        private void Start()
        {
            if (_netMaterial == null || _config == null)
            {
                Debug.LogWarning("WallNetView disabled: net material or game configuration is missing.", this);
                enabled = false;
                return;
            }

            // The shader un-extrudes each row back to the wall's edge line at rest by this much, so it
            // must match the geometry width the meshes were built with (single source of truth in C#).
            _netMaterial.SetFloat(StripWidthId, _stripWidth);
            BuildStrips(new WallLimits(_config.LimitsClockwise));
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }

            _meshes.Clear();
        }

        [Inject]
        private void Construct(IGameConfiguration config)
        {
            _config = config;
        }

        private void BuildStrips(WallLimits limits)
        {
            // Run the horizontal strips past both corners by the strip width so the four edges join into a
            // continuous frame: the top/bottom bands cover the outer corner squares that the perpendicular
            // side bands leave open when they unfurl. The side strips stay at the exact edge length, so the
            // corners are owned by the horizontals — filled, without overlapping the sides.
            var horizontal = new Vector2((limits.Right - limits.Left) + 2f * _stripWidth, 0f);
            var vertical = new Vector2(0f, limits.Top - limits.Bottom);

            BuildStrip("WallNet_Top", new Vector2(limits.Left - _stripWidth, limits.Top), horizontal, Vector2.up);
            BuildStrip("WallNet_Bottom", new Vector2(limits.Left - _stripWidth, limits.Bottom), horizontal, Vector2.down);
            BuildStrip("WallNet_Left", new Vector2(limits.Left, limits.Bottom), vertical, Vector2.left);
            BuildStrip("WallNet_Right", new Vector2(limits.Right, limits.Bottom), vertical, Vector2.right);
        }

        // Lays one band from `corner` along `alongEdge` (its full world length), extruded OUTWARD (away from
        // the play area) by `_stripWidth` along `outwardDir` (unit). u runs 0->1 across the width; a second
        // UV set carries the net-pattern tiling; every vertex's normal is the outward direction the shader
        // un-extrudes and billows along.
        private void BuildStrip(string childName, Vector2 corner, Vector2 alongEdge, Vector2 outwardDir)
        {
            var length = alongEdge.magnitude;
            if (length <= Mathf.Epsilon || _stripWidth <= Mathf.Epsilon)
            {
                return;
            }

            var cellsAlong = Mathf.Max(1, Mathf.RoundToInt(length * _cellsPerUnitAlong));
            var cellsAcross = Mathf.Max(1, _cellsAcross);
            var netAcross = Mathf.Max(1, _netCellsAcross);
            // Net cells are square: the across cell size (width / netAcross) sets the along tiling.
            var netAlong = Mathf.Max(1, Mathf.RoundToInt(length * netAcross / _stripWidth));

            var outwardUnit = outwardDir.normalized;
            var outward = outwardUnit * _stripWidth;
            var normal = new Vector3(outwardUnit.x, outwardUnit.y, 0f);

            var vertsAlong = cellsAlong + 1;
            var vertsAcross = cellsAcross + 1;
            var vertexCount = vertsAlong * vertsAcross;

            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv0 = new Vector2[vertexCount];
            var uv1 = new Vector2[vertexCount];
            var triangles = new int[cellsAlong * cellsAcross * 6];

            var v = 0;
            for (var ia = 0; ia < vertsAlong; ia++)
            {
                var fAlong = ia / (float)cellsAlong;
                for (var ic = 0; ic < vertsAcross; ic++)
                {
                    var fAcross = ic / (float)cellsAcross;
                    var p = corner + alongEdge * fAlong + outward * fAcross;
                    vertices[v] = new Vector3(p.x, p.y, 0f);
                    normals[v] = normal;
                    uv0[v] = new Vector2(fAcross, fAlong);
                    uv1[v] = new Vector2(fAcross * netAcross, fAlong * netAlong);
                    v++;
                }
            }

            var t = 0;
            for (var ia = 0; ia < cellsAlong; ia++)
            {
                for (var ic = 0; ic < cellsAcross; ic++)
                {
                    var bottomLeft = ia * vertsAcross + ic;
                    var topLeft = bottomLeft + vertsAcross;
                    triangles[t++] = bottomLeft;
                    triangles[t++] = topLeft;
                    triangles[t++] = bottomLeft + 1;
                    triangles[t++] = bottomLeft + 1;
                    triangles[t++] = topLeft;
                    triangles[t++] = topLeft + 1;
                }
            }

            var mesh = new Mesh { name = childName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            var child = new GameObject(childName);
            child.transform.SetParent(transform, worldPositionStays: false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _netMaterial;
            renderer.sortingLayerName = _sortingLayerName;
            renderer.sortingOrder = _sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}

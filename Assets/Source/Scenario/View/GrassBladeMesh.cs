using System.Runtime.InteropServices;
using BalloonParty.Shared.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonParty.Scenario.View
{
    /// <summary>The tile of grass blades <see cref="GrassBladeMesh" /> scatters: its size, blade spacing and seed.</summary>
    internal readonly struct GrassBladeLayout
    {
        internal Vector2 TileSize { get; }
        internal float Spacing { get; }
        internal int Seed { get; }

        internal GrassBladeLayout(Vector2 tileSize, float spacing, int seed)
        {
            TileSize = tileSize;
            Spacing = spacing;
            Seed = seed;
        }
    }

    /// <summary>
    ///     Builds the one mesh that holds every grass blade of the ground: a jittered grid over a tile, each blade a
    ///     tapered five-vertex strip. The mesh carries no blade shape: every vertex of a blade sits on its root, and
    ///     <c>BalloonParty/Scenario/GrassBlades</c> places, bends and wraps it per frame. A jittered grid tiles
    ///     seamlessly, so the shader can wrap the roots as the meadow scrolls.
    /// </summary>
    internal static class GrassBladeMesh
    {
        internal const int VerticesPerBlade = 5;
        internal const int IndicesPerBlade = 9;

        // As many blades as 16-bit indices can address: the vertex budget in one place, and no silent switch to
        // 32-bit indices (twice the index bandwidth) when the spacing is tuned down.
        internal const int MaxBlades = ushort.MaxValue / VerticesPerBlade;

        // Per blade: root left, root right, middle left, middle right, tip, as SNorm8: x = side across the blade
        // (-1 / +1, 0 at the tip), y = how far along it (0 root, about 0.5 middle, 1 tip).
        private static readonly sbyte[] CornerSide = { -127, 127, -127, 127, 0 };
        private static readonly sbyte[] CornerAlong = { 0, 0, 64, 64, 127 };

        private static readonly ushort[] Triangles = { 0, 2, 1, 1, 2, 3, 2, 4, 3 };

        // 12 bytes a vertex, read every frame by the vertex stage: the root as a fraction of the tile (UNorm16 x2),
        // the corner (SNorm8 x4) and four per-blade randoms for lean, length, shade and sway phase (UNorm8 x4).
        private static readonly VertexAttributeDescriptor[] Layout =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.UNorm16, 2),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.SNorm8, 4),
            new(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm8, 4)
        };

        internal static Mesh Build(GrassBladeLayout layout)
        {
            var size = new Vector2(Mathf.Max(layout.TileSize.x, 0.01f), Mathf.Max(layout.TileSize.y, 0.01f));
            var spacing = Mathf.Max(layout.Spacing, Mathf.Sqrt(size.x * size.y / MaxBlades));

            // A whole number of cells each way, so the tile repeats without a seam. The spacing clamp above keeps
            // the count near the cap; rounding, and a tile too thin for one row at that spacing, can still pass
            // it, so the cap is enforced on the counts themselves.
            var columns = Mathf.Max(1, Mathf.RoundToInt(size.x / spacing));
            var rows = Mathf.Max(1, Mathf.RoundToInt(size.y / spacing));
            if (columns * rows > MaxBlades)
            {
                Log.Warn("Ground", $"blade spacing {layout.Spacing} would pass {MaxBlades} blades; capped.");
                columns = Mathf.Min(columns, Mathf.Max(1, MaxBlades / rows));
                rows = Mathf.Min(rows, Mathf.Max(1, MaxBlades / columns));
            }

            var bladeCount = columns * rows;
            var vertexCount = bladeCount * VerticesPerBlade;
            var indexCount = bladeCount * IndicesPerBlade;
            var vertices = new NativeArray<BladeVertex>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var indices = new NativeArray<ushort>(indexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var random = new System.Random(layout.Seed);

            var blade = 0;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var rootX = ToUNorm16((column + (float)random.NextDouble()) / columns);
                    var rootY = ToUNorm16((row + (float)random.NextDouble()) / rows);
                    var randoms = new Color32(
                        (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256),
                        (byte)random.Next(256));

                    var firstVertex = blade * VerticesPerBlade;
                    for (var corner = 0; corner < VerticesPerBlade; corner++)
                    {
                        vertices[firstVertex + corner] = new BladeVertex(
                            rootX, rootY, CornerSide[corner], CornerAlong[corner], randoms);
                    }

                    var firstIndex = blade * IndicesPerBlade;
                    for (var i = 0; i < IndicesPerBlade; i++)
                    {
                        indices[firstIndex + i] = (ushort)(firstVertex + Triangles[i]);
                    }

                    blade++;
                }
            }

            // The shader moves every blade, so the stored roots say nothing about where they draw: bound the tile
            // itself, centred where the shader centres it, so the renderer is never culled.
            var bounds = new Bounds(Vector3.zero, new Vector3(size.x, size.y, 1f));
            const MeshUpdateFlags NoRecompute = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

            var mesh = new Mesh
            {
                name = "GrassBlades"
            };
            mesh.SetVertexBufferParams(vertexCount, Layout);
            mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, NoRecompute);
            mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
            mesh.SetIndexBufferData(indices, 0, 0, indexCount, NoRecompute);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount) { bounds = bounds, vertexCount = vertexCount }, NoRecompute);
            mesh.bounds = bounds;
            mesh.UploadMeshData(markNoLongerReadable: true);

            vertices.Dispose();
            indices.Dispose();
            return mesh;
        }

        private static ushort ToUNorm16(float value01)
        {
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(value01 * ushort.MaxValue), 0, ushort.MaxValue);
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct BladeVertex
        {
            private readonly ushort _rootX;
            private readonly ushort _rootY;
            private readonly sbyte _side;
            private readonly sbyte _along;
            private readonly sbyte _cornerPadZ;
            private readonly sbyte _cornerPadW;
            private readonly Color32 _randoms;

            internal BladeVertex(ushort rootX, ushort rootY, sbyte side, sbyte along, Color32 randoms)
            {
                _rootX = rootX;
                _rootY = rootY;
                _side = side;
                _along = along;
                _cornerPadZ = 0;
                _cornerPadW = 0;
                _randoms = randoms;
            }
        }
    }
}

using BalloonParty.Scenario.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonParty.Tests.Scenario
{
    // Build() marks the mesh no longer readable, so only what the GPU-side mesh still reports is asserted
    // here: counts, index format and bounds. Per-vertex content (roots on one point, jitter inside its cell,
    // determinism per seed, seamless wrap) needs a readable seam in the builder to be pinned.
    [TestFixture]
    public class GrassBladeMeshTests
    {
        private Mesh _mesh;

        [TearDown]
        public void TearDown()
        {
            if (_mesh != null)
            {
                Object.DestroyImmediate(_mesh);
            }
        }

        [Test]
        public void Build_WholeCells_ProducesFiveVerticesAndNineIndicesPerBlade()
        {
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(4f, 2f), 1f, 1));

            Assert.AreEqual(8 * GrassBladeMesh.VerticesPerBlade, _mesh.vertexCount);
            Assert.AreEqual(8 * GrassBladeMesh.IndicesPerBlade, (int)_mesh.GetIndexCount(0));
        }

        [Test]
        public void Build_SpacingNotDividingTile_RoundsToWholeCellsEachWay()
        {
            // 4.4 / 1 -> 4 columns, 2.6 / 1 -> 3 rows: the cell stretches so the tile still wraps.
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(4.4f, 2.6f), 1f, 1));

            Assert.AreEqual(12 * GrassBladeMesh.VerticesPerBlade, _mesh.vertexCount);
        }

        [Test]
        public void Build_SpacingLargerThanTile_StillBuildsOneBlade()
        {
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(1f, 1f), 50f, 1));

            Assert.AreEqual(GrassBladeMesh.VerticesPerBlade, _mesh.vertexCount);
        }

        [Test]
        public void Build_SpacingTinyOnSquareTile_ClampsBladeCountToMax()
        {
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(100f, 100f), 0.001f, 1));

            var blades = _mesh.vertexCount / GrassBladeMesh.VerticesPerBlade;
            Assert.LessOrEqual(blades, GrassBladeMesh.MaxBlades);
            Assert.Greater(blades, GrassBladeMesh.MaxBlades / 2, "the clamp should land near the cap, not collapse");
        }

        // Regression guard: on a very wide, thin tile the row count floors at 1 while the column count
        // follows the clamped spacing, so the blade count can pass MaxBlades.
        [Test]
        public void Build_SpacingTinyOnThinTile_ClampsBladeCountToMax()
        {
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(1000f, 0.01f), 0.001f, 1));

            Assert.LessOrEqual(_mesh.vertexCount / GrassBladeMesh.VerticesPerBlade, GrassBladeMesh.MaxBlades);
        }

        [Test]
        public void Build_ZeroOrNegativeInputs_DoNotThrowAndBuildAtLeastOneBlade()
        {
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(0f, -3f), 0f, 1));

            Assert.GreaterOrEqual(_mesh.vertexCount, GrassBladeMesh.VerticesPerBlade);
            Assert.LessOrEqual(_mesh.vertexCount / GrassBladeMesh.VerticesPerBlade, GrassBladeMesh.MaxBlades);
        }

        [Test]
        public void Build_SmallMesh_UsesUInt16Indices()
        {
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(4f, 2f), 1f, 1));

            Assert.AreEqual(IndexFormat.UInt16, _mesh.indexFormat);
        }

        [Test]
        public void Build_VertexCountAtUShortMax_StaysUInt16()
        {
            // 3 x 4369 = 13107 blades -> 65535 vertices, the last count a 16-bit index can address.
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(3f, 4369f), 1f, 1));

            Assert.AreEqual(ushort.MaxValue, _mesh.vertexCount);
            Assert.AreEqual(IndexFormat.UInt16, _mesh.indexFormat);
        }

        [Test]
        public void Build_CountPastUShortMax_IsCappedAndStaysUInt16()
        {
            // 4 x 3277 = 13108 blades would be 65540 vertices: one blade past what a 16-bit index addresses.
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(4f, 3277f), 1f, 1));

            Assert.LessOrEqual(_mesh.vertexCount, ushort.MaxValue);
            Assert.AreEqual(IndexFormat.UInt16, _mesh.indexFormat);
        }

        [Test]
        public void Build_AnyLayout_BoundsAreCentredOnOriginAndCoverTile()
        {
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(6f, 3f), 0.5f, 7));

            Assert.AreEqual(Vector3.zero, _mesh.bounds.center);
            Assert.AreEqual(6f, _mesh.bounds.size.x, 1e-4f);
            Assert.AreEqual(3f, _mesh.bounds.size.y, 1e-4f);
        }

        [Test]
        public void Build_TileSmallerThanMinimum_BoundsUseMinimumSize()
        {
            _mesh = GrassBladeMesh.Build(new GrassBladeLayout(new Vector2(0f, 0f), 1f, 1));

            Assert.AreEqual(0.01f, _mesh.bounds.size.x, 1e-5f);
            Assert.AreEqual(0.01f, _mesh.bounds.size.y, 1e-5f);
        }
    }
}

using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Score.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ShapeInkMeshBuilderTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<Mesh> _meshes = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var mesh in _meshes)
            {
                Object.DestroyImmediate(mesh);
            }

            _meshes.Clear();
        }

        [Test]
        public void Build_EveryCatalogShape_PutsEachVertexWhereAPenPassesAtThatDistance()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                var shape = Shape(denomination);
                var baked = Bake(shape);

                for (var i = 0; i < baked.RibbonVertices; i++)
                {
                    var walk = Mathf.RoundToInt(baked.Walk[i].z);
                    var distance = Mathf.Repeat(baked.Walk[i].x, shape.Perimeters[walk]);
                    var segment = 0;
                    var pen = shape.PointOnWalk(walk, distance, ref segment);

                    Assert.Less(Vector3.Distance(pen, baked.Positions[i]), Tolerance,
                        $"shape {denomination}, vertex {i}: walk {walk} at distance {baked.Walk[i].x}");
                }
            }
        }

        [Test]
        public void Build_EveryCatalogShape_GivesEachWalkTheGapBetweenItsPens()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                var shape = Shape(denomination);
                var baked = Bake(shape);

                for (var i = 0; i < baked.RibbonVertices; i++)
                {
                    var walk = Mathf.RoundToInt(baked.Walk[i].z);
                    var expected = shape.Perimeters[walk] / shape.PensPerWalk[walk];

                    Assert.AreEqual(expected, baked.Walk[i].y, Tolerance, $"shape {denomination}, vertex {i}");
                }
            }
        }

        [Test]
        public void Build_EveryCatalogShape_CoversEachWalkFromItsStartRoundToItsPerimeterOnce()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                var shape = Shape(denomination);
                var baked = Bake(shape);
                var covered = new float[shape.Walks.Length];

                // Four vertices a segment: the near end's two, then the far end's two.
                for (var first = 0; first < baked.RibbonVertices; first += 4)
                {
                    var walk = Mathf.RoundToInt(baked.Walk[first].z);
                    var from = baked.Walk[first].x;
                    var to = baked.Walk[first + 2].x;

                    Assert.AreEqual(covered[walk], from, Tolerance,
                        $"shape {denomination}: a segment of walk {walk} starts where the last one ended");
                    Assert.Greater(to, from - Tolerance, $"shape {denomination}: distance runs forward");
                    covered[walk] = to;
                }

                for (var w = 0; w < shape.Walks.Length; w++)
                {
                    Assert.AreEqual(shape.Perimeters[w], covered[w], Tolerance, $"shape {denomination}, walk {w}");
                }
            }
        }

        [Test]
        public void Build_EveryCatalogShape_ChainsEachSegmentToItsNeighbours()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                var baked = Bake(Shape(denomination));

                for (var first = 0; first < baked.RibbonVertices; first += 4)
                {
                    var near = baked.Positions[first];
                    var far = baked.Positions[first + 2];

                    Assert.Less(Vector3.Distance((Vector3)baked.Other[first], far), Tolerance,
                        $"shape {denomination}: the near end knows the far end");
                    Assert.Less(Vector3.Distance((Vector3)baked.Other[first + 2], near), Tolerance,
                        $"shape {denomination}: the far end knows the near end");
                    Assert.AreEqual(-1f, baked.Other[first].w, $"shape {denomination}: first vertex is one side");
                    Assert.AreEqual(1f, baked.Other[first + 1].w, $"shape {denomination}: second is the other");
                    Assert.AreEqual(0f, baked.Neighbour[first].w, $"shape {denomination}: near end flag");
                    Assert.AreEqual(1f, baked.Neighbour[first + 2].w, $"shape {denomination}: far end flag");

                    // The next segment of the same walk starts at this one's far end, and each names the
                    // other's outer point as its neighbour, so both work out the same corner.
                    var next = first + 4;
                    if (next < baked.RibbonVertices && Mathf.Approximately(baked.Walk[next].z, baked.Walk[first].z))
                    {
                        Assert.Less(Vector3.Distance(baked.Positions[next], far), Tolerance,
                            $"shape {denomination}: the next segment starts at this far end");
                        Assert.Less(
                            Vector3.Distance((Vector3)baked.Neighbour[first + 2], baked.Positions[next + 2]), Tolerance,
                            $"shape {denomination}: this far end's neighbour is the next segment's far end");
                        Assert.Less(Vector3.Distance((Vector3)baked.Neighbour[next], near), Tolerance,
                            $"shape {denomination}: the next near end's neighbour is this near end");
                    }
                }
            }
        }

        [Test]
        public void Build_EveryCatalogShape_TellsEachRibbonEndItsOtherEndsDistance()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                var mesh = ShapeInkMeshBuilder.Build(Shape(denomination));
                _meshes.Add(mesh);
                var walk = new List<Vector4>();
                var extra = new List<Vector4>();
                mesh.GetUVs(0, walk);
                mesh.GetUVs(3, extra);

                Assert.AreEqual(walk.Count, extra.Count, $"shape {denomination}");
                for (var first = 0; first < walk.Count && Mathf.Approximately(walk[first].w, 0f); first += 4)
                {
                    Assert.AreEqual(walk[first + 2].x, extra[first].x, Tolerance, $"shape {denomination}: near end");
                    Assert.AreEqual(walk[first].x, extra[first + 2].x, Tolerance, $"shape {denomination}: far end");
                }
            }
        }

        [Test]
        public void Build_ArcWalks_AreCutNoCoarserThanTheArcStep()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                var shape = Shape(denomination);
                var baked = Bake(shape);

                for (var first = 0; first < baked.RibbonVertices; first += 4)
                {
                    var walk = Mathf.RoundToInt(baked.Walk[first].z);
                    if (!shape.Walks[walk].Arc)
                    {
                        continue;
                    }

                    var angle = Vector3.Angle(baked.Positions[first], baked.Positions[first + 2]);
                    Assert.LessOrEqual(angle, ShapeInkMeshBuilder.MaxArcStepDegrees + 0.01f,
                        $"shape {denomination}, walk {walk}");
                }
            }
        }

        [Test]
        public void Build_StraightWalks_AreOneSegmentAnEdge()
        {
            ShapeCatalog.TryGet(8, out var cube);
            var baked = Bake(cube);
            var edges = 0;
            foreach (var walk in cube.Walks)
            {
                edges += walk.Vertices.Length;
            }

            Assert.AreEqual(edges * 4, baked.RibbonVertices);
        }

        [Test]
        public void Build_EveryCatalogShape_GivesEverySegmentASquareForEachPenItCanHold()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                var baked = Bake(Shape(denomination));
                var expected = 0;
                for (var first = 0; first < baked.RibbonVertices; first += 4)
                {
                    var length = baked.Walk[first + 2].x - baked.Walk[first].x;
                    if (length > Mathf.Epsilon)
                    {
                        expected += Mathf.Max(1, Mathf.CeilToInt(length / baked.Walk[first].y - 1e-3f));
                    }
                }

                Assert.AreEqual(expected * 4, baked.Positions.Length - baked.BallStart, $"shape {denomination}");

                for (var first = baked.BallStart; first < baked.Positions.Length; first += 4)
                {
                    for (var corner = 0; corner < 4; corner++)
                    {
                        Assert.GreaterOrEqual(baked.Walk[first + corner].w, ShapeInkMeshBuilder.FirstBallKind,
                            $"shape {denomination}: a square's vertices are all pen balls");
                        Assert.AreEqual(baked.Positions[first], baked.Positions[first + corner],
                            $"shape {denomination}: a square's vertices all sit on the segment's near end");
                        Assert.Greater(baked.Neighbour[first + corner].x, baked.Walk[first + corner].x,
                            $"shape {denomination}: the far end lies further along the walk");
                    }

                    // The four corners: both ways along the segment, both ways across it.
                    Assert.AreEqual(-1f, baked.Neighbour[first].y, $"shape {denomination}");
                    Assert.AreEqual(1f, baked.Neighbour[first + 2].y, $"shape {denomination}");
                    Assert.AreEqual(-1f, baked.Other[first].w, $"shape {denomination}");
                    Assert.AreEqual(1f, baked.Other[first + 1].w, $"shape {denomination}");
                }
            }
        }

        [Test]
        public void Build_EveryCatalogShape_PutsAJointOnEachSharpCornerAndNowhereElse()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                var baked = Bake(Shape(denomination));
                var joints = new List<Vector3>();
                for (var first = baked.RibbonVertices; first < baked.BallStart; first += 4)
                {
                    joints.Add(baked.Positions[first]);
                    Assert.AreEqual(-1f, baked.Neighbour[first].y, $"shape {denomination}");
                    Assert.AreEqual(1f, baked.Neighbour[first + 2].y, $"shape {denomination}");
                    Assert.AreEqual(-1f, baked.Other[first].w, $"shape {denomination}");
                    Assert.AreEqual(1f, baked.Other[first + 1].w, $"shape {denomination}");
                }

                // Every ribbon piece's near end is a point of its walk; the piece before it ends there.
                var sharp = 0;
                for (var first = 0; first < baked.RibbonVertices; first += 4)
                {
                    var here = baked.Positions[first];
                    var before = (Vector3)baked.Neighbour[first];
                    var after = baked.Positions[first + 2];
                    var turn = Vector3.Angle(here - before, after - here);
                    var walk = baked.Walk[first].z;
                    var jointed = joints.Exists(joint => Vector3.Distance(joint, here) < Tolerance);

                    if (turn > ShapeInkMeshBuilder.JointTurnDegrees)
                    {
                        sharp++;
                        Assert.IsTrue(jointed, $"shape {denomination}, walk {walk}: a {turn:0} degree corner has no joint");
                    }
                }

                Assert.AreEqual(sharp, joints.Count, $"shape {denomination}: one joint for each sharp corner");
            }
        }

        [Test]
        public void Build_GivesTwoSubmeshesWithTheSameTriangles()
        {
            ShapeCatalog.TryGet(8, out var cube);
            var mesh = ShapeInkMeshBuilder.Build(cube);
            _meshes.Add(mesh);

            Assert.AreEqual(2, mesh.subMeshCount);
            CollectionAssert.AreEqual(mesh.GetTriangles(0), mesh.GetTriangles(1));
        }

        [Test]
        public void RampFill_ScalesTheWidthToItsWidestAndReturnsIt()
        {
            var width = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(0.5f, 0.4f), new Keyframe(1f, 0f));
            var settings = new ShapeInkSettings(
                true, null, width, AnimationCurve.Linear(0f, 1f, 1f, 0f), AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var pixels = new Color32[ShapeInkRamp.Size];

            var widest = ShapeInkRamp.Fill(in settings, pixels);

            Assert.AreEqual(0.4f, widest, 0.005f);
            Assert.AreEqual(0.25f, pixels[0].r / 255f, 0.01f, "the pen end is a quarter of the widest");
            Assert.AreEqual(0, pixels[ShapeInkRamp.Size - 1].r, "the oldest ink has no width");
            Assert.AreEqual(255, pixels[0].g, "opaque at the pen");
            Assert.AreEqual(0, pixels[ShapeInkRamp.Size - 1].g, "faded out at the tail");
            Assert.AreEqual(0, pixels[0].b, "the shape's colour at the pen");
            Assert.AreEqual(255, pixels[ShapeInkRamp.Size - 1].b, "the tail's colour at the end");
        }

        [Test]
        public void Settings_WithoutAMaterial_AreNotBaked()
        {
            var curve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

            Assert.IsFalse(new ShapeInkSettings(true, null, curve, curve, curve).Baked);
            Assert.IsFalse(default(ShapeInkSettings).Baked);
        }

        private static FormationShape Shape(int denomination)
        {
            Assert.IsTrue(ShapeCatalog.TryGet(denomination, out var shape), $"no shape for {denomination}");
            return shape;
        }

        private BakedShape Bake(FormationShape shape)
        {
            var mesh = ShapeInkMeshBuilder.Build(shape);
            _meshes.Add(mesh);

            var walk = new List<Vector4>();
            var other = new List<Vector4>();
            var neighbour = new List<Vector4>();
            mesh.GetUVs(0, walk);
            mesh.GetUVs(1, other);
            mesh.GetUVs(2, neighbour);
            return new BakedShape(mesh.vertices, walk, other, neighbour);
        }

        private readonly struct BakedShape
        {
            internal readonly Vector3[] Positions;
            internal readonly List<Vector4> Walk;
            internal readonly List<Vector4> Other;
            internal readonly List<Vector4> Neighbour;

            // The ribbon pieces come first, then the joints, then the pen balls' squares.
            internal readonly int RibbonVertices;
            internal readonly int BallStart;

            internal BakedShape(Vector3[] positions, List<Vector4> walk, List<Vector4> other, List<Vector4> neighbour)
            {
                Positions = positions;
                Walk = walk;
                Other = other;
                Neighbour = neighbour;

                RibbonVertices = 0;
                while (RibbonVertices < walk.Count
                       && Mathf.Approximately(walk[RibbonVertices].w, ShapeInkMeshBuilder.RibbonKind))
                {
                    RibbonVertices++;
                }

                BallStart = RibbonVertices;
                while (BallStart < walk.Count && Mathf.Approximately(walk[BallStart].w, ShapeInkMeshBuilder.JointKind))
                {
                    BallStart++;
                }
            }
        }
    }
}

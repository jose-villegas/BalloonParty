using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Bakes a <see cref="FormationShape" /> into the wire mesh its ink is drawn on (the
    ///     <c>BalloonParty/Sprite/ShapeInk</c> shader, through <c>ShapeInkView</c>), in the shape's own unit space.
    ///     Every closed walk is cut into segments, and the mesh is three kinds of square, each four vertices:
    ///     <list type="bullet">
    ///         <item>A ribbon piece for each segment: two vertices at each end, one for each side of the ribbon.
    ///         They sit on the walk itself; the shader pushes them apart to the ribbon's width, facing the camera.</item>
    ///         <item>A joint at each corner of a walk that turns more than <see cref="JointTurnDegrees" />: a
    ///         square on the corner, in which the shader draws the ink round, so two ribbon ends meeting at a
    ///         sharp corner close without a notch or a spike.</item>
    ///         <item>A pen's ball for each segment: a square on the segment's near end, which the shader moves to
    ///         wherever on the segment a pen is and folds away while none is. A segment longer than the gap
    ///         between two pens carries a square for each pen it can hold.</item>
    ///     </list>
    ///     They are listed in that order, which is the order they draw in: joints over ribbons, balls over both.
    ///     What each vertex carries:
    ///     <list type="bullet">
    ///         <item>UV0: its distance along the walk (x), the gap between two pens on that walk (y), the walk's
    ///         index (z), and its kind (w): <see cref="RibbonKind" />, <see cref="JointKind" />, or
    ///         <see cref="FirstBallKind" /> for the segment's first pen ball, one more for its second, and so on.</item>
    ///         <item>UV1: the segment's other end, or for a joint the walk's next point (xyz), and the vertex's
    ///         side of the ribbon or of the square (w, -1 or 1).</item>
    ///         <item>UV2, ribbon: the walk's next point past this end (xyz), for the corner, and whether this is
    ///         the segment's far end (w). UV2, joint and ball: the segment's far end's distance along the walk
    ///         (x, balls only) and the square's corner along the segment (y, -1 or 1).</item>
    ///         <item>UV3, ribbon: the segment's other end's distance along the walk (x), so a pixel past the end
    ///         of a stroke that runs on beyond its corner still knows the corner's distance.</item>
    ///     </list>
    ///     Straight edges are one segment each. Arc edges are cut so no piece turns more than
    ///     <see cref="MaxArcStepDegrees" />, at points a pen passes through (<see cref="FormationShape.PointOnWalk" />).
    ///     The mesh has <see cref="Layers" /> submeshes with the same triangles, one for each time the view draws
    ///     it: the drop shadow, then the shape's far half, then its near half over that.
    /// </summary>
    internal static class ShapeInkMeshBuilder
    {
        internal const float MaxArcStepDegrees = 6f;

        // A gentler corner than this the ribbon's own miter closes (ShapeInk.shader's MITER_MIN_COS is its cosine).
        internal const float JointTurnDegrees = 20f;
        internal const float RibbonKind = 0f;
        internal const float JointKind = -1f;
        internal const float FirstBallKind = 1f;

        // The shadow, the far half and the near half, in the order they draw.
        internal const int Layers = 3;

        // Past this an arc's middle would need cutting without end; no catalog arc comes near it.
        private const float WidestArcDegrees = 170f;

        // The shapes are unit-radius; the waving sphere swells past that, and the bounds never follow it.
        private const float BoundsSize = 3f;

        // A segment exactly as long as the gap holds one pen, not two.
        private const float GapSlack = 1e-3f;

        internal static Mesh Build(FormationShape shape)
        {
            var buffers = new Buffers();
            var walk = new Walk();

            for (var w = 0; w < shape.Walks.Length; w++)
            {
                if (walk.Sample(shape, w))
                {
                    AddRibbons(buffers, walk);
                }
            }

            for (var w = 0; w < shape.Walks.Length; w++)
            {
                if (walk.Sample(shape, w))
                {
                    AddJoints(buffers, walk);
                }
            }

            for (var w = 0; w < shape.Walks.Length; w++)
            {
                if (walk.Sample(shape, w))
                {
                    AddBalls(buffers, walk);
                }
            }

            return buffers.ToMesh($"ShapeInk{shape.Denomination}");
        }

        private static void AddRibbons(Buffers buffers, Walk walk)
        {
            for (var i = 0; i < walk.Segments; i++)
            {
                var first = buffers.Positions.Count;
                for (var end = 0; end < 2; end++)
                {
                    var here = walk.Points[i + end];
                    var other = walk.Points[i + 1 - end];
                    var neighbour = end == 0 ? walk.Point(i - 1) : walk.Point(i + 2);
                    for (var side = -1; side <= 1; side += 2)
                    {
                        buffers.Positions.Add(here);
                        buffers.WalkData.Add(new Vector4(walk.Distances[i + end], walk.Gap, walk.Index, RibbonKind));
                        buffers.Others.Add(new Vector4(other.x, other.y, other.z, side));
                        buffers.Neighbours.Add(new Vector4(neighbour.x, neighbour.y, neighbour.z, end));
                        buffers.Extras.Add(new Vector4(walk.Distances[i + 1 - end], 0f, 0f, 0f));
                    }
                }

                buffers.AddQuad(first);
            }
        }

        private static void AddJoints(Buffers buffers, Walk walk)
        {
            foreach (var corner in walk.Corners)
            {
                var here = walk.Points[corner];
                var before = walk.Point(corner - 1);
                var after = walk.Point(corner + 1);
                if (Vector3.Angle(here - before, after - here) <= JointTurnDegrees)
                {
                    continue;
                }

                var data = new Vector4(walk.Distances[corner], walk.Gap, walk.Index, JointKind);
                buffers.AddSquare(here, after, data, 0f);
            }
        }

        private static void AddBalls(Buffers buffers, Walk walk)
        {
            for (var i = 0; i < walk.Segments; i++)
            {
                var length = walk.Distances[i + 1] - walk.Distances[i];
                if (length <= Mathf.Epsilon)
                {
                    continue;
                }

                var pens = Mathf.Max(1, Mathf.CeilToInt(length / walk.Gap - GapSlack));
                for (var pen = 0; pen < pens; pen++)
                {
                    var data = new Vector4(walk.Distances[i], walk.Gap, walk.Index, FirstBallKind + pen);
                    buffers.AddSquare(walk.Points[i], walk.Points[i + 1], data, walk.Distances[i + 1]);
                }
            }
        }

        // Even steps along an arc (FormationShape.Interpolate) turn fastest at its middle, by 2·tan(angle/2)
        // over the whole arc against the angle itself on average, so the count is taken from the middle.
        private static int ArcSteps(Vector3 a, Vector3 b)
        {
            var angle = Mathf.Min(Vector3.Angle(a, b), WidestArcDegrees);
            var fastest = 2f * Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad) * Mathf.Rad2Deg;
            return Mathf.Max(1, Mathf.CeilToInt(fastest / MaxArcStepDegrees));
        }

        // One walk cut into segments: its points in order, each with its distance along the walk, the first
        // repeated at the end with the whole perimeter as its distance, so there are as many segments as points
        // but one. Corners are the points where one of the walk's own edges ends and the next begins.
        private sealed class Walk
        {
            internal readonly List<Vector3> Points = new();
            internal readonly List<float> Distances = new();
            internal readonly List<int> Corners = new();

            internal int Index;
            internal float Gap;

            internal int Segments => Points.Count - 1;

            // False for a walk no pen travels, which has no ink to draw.
            internal bool Sample(FormationShape shape, int w)
            {
                var pens = shape.PensPerWalk[w];
                var perimeter = shape.Perimeters[w];
                if (pens <= 0 || perimeter <= Mathf.Epsilon)
                {
                    return false;
                }

                Points.Clear();
                Distances.Clear();
                Corners.Clear();
                Index = w;
                Gap = perimeter / pens;

                var walk = shape.Walks[w];
                var loop = walk.Vertices;
                var cumulative = shape.Cumulative[w];
                for (var seg = 0; seg < loop.Length; seg++)
                {
                    var a = shape.Vertices[loop[seg]];
                    var b = shape.Vertices[loop[(seg + 1) % loop.Length]];
                    var steps = walk.Arc ? ArcSteps(a, b) : 1;
                    var length = cumulative[seg + 1] - cumulative[seg];
                    Corners.Add(Points.Count);
                    for (var i = 0; i < steps; i++)
                    {
                        var t = (float)i / steps;
                        Points.Add(FormationShape.Interpolate(a, b, t, walk.Arc));
                        Distances.Add(cumulative[seg] + t * length);
                    }
                }

                Points.Add(Points[0]);
                Distances.Add(perimeter);
                return true;
            }

            // The point at an index that may run off either end of the loop.
            internal Vector3 Point(int index)
            {
                return Points[(index % Segments + Segments) % Segments];
            }
        }

        private sealed class Buffers
        {
            internal readonly List<Vector3> Positions = new();
            internal readonly List<Vector4> WalkData = new();
            internal readonly List<Vector4> Others = new();
            internal readonly List<Vector4> Neighbours = new();
            internal readonly List<Vector4> Extras = new();

            private readonly List<int> _indices = new();

            // A square the shader places and sizes: all four vertices on one point, told apart by their corner.
            internal void AddSquare(Vector3 here, Vector3 other, Vector4 walk, float farDistance)
            {
                var first = Positions.Count;
                for (var along = -1; along <= 1; along += 2)
                {
                    for (var side = -1; side <= 1; side += 2)
                    {
                        Positions.Add(here);
                        WalkData.Add(walk);
                        Others.Add(new Vector4(other.x, other.y, other.z, side));
                        Neighbours.Add(new Vector4(farDistance, along, 0f, 0f));
                        Extras.Add(Vector4.zero);
                    }
                }

                AddQuad(first);
            }

            internal void AddQuad(int first)
            {
                _indices.Add(first);
                _indices.Add(first + 1);
                _indices.Add(first + 2);
                _indices.Add(first + 2);
                _indices.Add(first + 1);
                _indices.Add(first + 3);
            }

            internal Mesh ToMesh(string name)
            {
                var mesh = new Mesh
                {
                    name = name,
                    hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = Positions.Count > ushort.MaxValue
                        ? UnityEngine.Rendering.IndexFormat.UInt32
                        : UnityEngine.Rendering.IndexFormat.UInt16
                };
                mesh.SetVertices(Positions);
                mesh.SetUVs(0, WalkData);
                mesh.SetUVs(1, Others);
                mesh.SetUVs(2, Neighbours);
                mesh.SetUVs(3, Extras);
                mesh.subMeshCount = Layers;
                for (var layer = 0; layer < Layers; layer++)
                {
                    mesh.SetTriangles(_indices, layer, false);
                }

                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * BoundsSize);
                return mesh;
            }
        }
    }
}

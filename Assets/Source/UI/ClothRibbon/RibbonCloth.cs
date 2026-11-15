using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.UI.ClothRibbon
{
    /// <summary>
    ///     A line hanging from an anchor with a ribbon of cloth tied to its end, simulated as points joined by
    ///     springs (position-based, at fixed steps). The line's last point is the ribbon's pivot: the middle of its
    ///     head, in the notch. The ribbon is three rows of points (top edge, middle, bottom edge) by
    ///     <see cref="Columns" />, held upright so it turns about the vertical. A breeze streams it out behind the
    ///     anchor's sideways travel and a wave runs down it toward the tail; when the travel reverses, the breeze turns
    ///     round and a push out of the screen swings the tail through depth to the other side, so its back shows on the
    ///     way. Space is the caller's: x right, y up, z toward the viewer; the ribbon's front faces +z while it streams
    ///     to +x. Obstacles push points out sideways, never in depth.
    /// </summary>
    internal sealed class RibbonCloth
    {
        private const int Rows = 3;
        private const int Top = 0;
        private const int Middle = 1;
        private const int Bottom = 2;
        private const int MaxStepsPerFrame = 8;
        private const float LongestFrame = 0.1f;
        private const float RoomMargin = 1.25f;

        private readonly RibbonClothSettings _settings;
        private readonly Vector3[] _position;
        private readonly Vector3[] _previous;
        private readonly float[] _inverseMass;
        private readonly List<Spring> _springs = new();
        private readonly int _lineEnd;
        private readonly int _clothStart;
        private readonly float _notch;

        private Vector3 _anchor;
        private float _accumulator;
        private float _time;
        private float _heading = 1f;
        private float _headingTarget = 1f;
        private float _turning;
        private bool _placed;
        private float _roomLeft = float.NegativeInfinity;
        private float _roomRight = float.PositiveInfinity;

        /// <summary>Points along the line, from the anchor to the ribbon's pivot.</summary>
        internal int LinePoints => _lineEnd + 1;

        /// <summary>Columns of points along the ribbon, from its head to its tail.</summary>
        internal int Columns => _settings.Columns + 1;

        internal float Length { get; }

        internal float Height => _settings.Height;

        /// <summary>Which way the breeze streams the ribbon: 1 toward +x, -1 toward -x, between while it turns.</summary>
        internal float Heading => _heading;

        internal RibbonCloth(RibbonClothSettings settings, float length)
        {
            _settings = settings;
            Length = Mathf.Max(length, settings.Height);
            _notch = Mathf.Min(settings.Notch * settings.Height, 0.9f * Length / settings.Columns);
            _lineEnd = settings.LineSegments;
            _clothStart = _lineEnd + 1;
            var count = _clothStart + Columns * Rows - 1;
            _position = new Vector3[count];
            _previous = new Vector3[count];
            _inverseMass = new float[count];
            for (var i = 1; i < count; i++)
            {
                _inverseMass[i] = i <= _lineEnd ? 1f : 1f / settings.RibbonWeight;
            }
            Lay(Vector3.zero);
            BuildSprings();
        }

        internal Vector3 LinePoint(int index)
        {
            return _position[index];
        }

        internal Vector3 ClothPoint(int column, int row)
        {
            return _position[ClothIndex(column, row)];
        }

        /// <summary>
        ///     The sideways room between <paramref name="left" /> and <paramref name="right" />: the breeze streams the
        ///     ribbon toward the side that has room for it, even against the travel, rather than off the edge.
        /// </summary>
        internal void SetRoom(float left, float right)
        {
            _roomLeft = left;
            _roomRight = right;
        }

        /// <summary>Lays the line straight down from <paramref name="anchor" /> and the ribbon out flat, at rest.</summary>
        internal void Place(Vector3 anchor)
        {
            _anchor = anchor;
            _accumulator = 0f;
            Lay(anchor);
            _placed = true;
        }

        /// <summary>
        ///     Moves the anchor to <paramref name="anchor" /> over <paramref name="deltaTime" /> seconds and lets the
        ///     line and ribbon follow, kept out of <paramref name="obstacles" />.
        /// </summary>
        internal void Step(Vector3 anchor, float deltaTime, IReadOnlyList<KeepOut> obstacles)
        {
            if (!_placed)
            {
                Place(anchor);
            }

            deltaTime = Mathf.Min(deltaTime, LongestFrame);
            if (deltaTime <= 0f)
            {
                return;
            }

            Steer(anchor.x, (anchor.x - _anchor.x) / deltaTime, deltaTime);
            var from = _anchor;
            var step = 1f / _settings.StepsPerSecond;
            var steps = 0;
            var elapsed = 0f;
            _accumulator += deltaTime;
            while (_accumulator >= step && steps < MaxStepsPerFrame)
            {
                _accumulator -= step;
                elapsed += step;
                steps++;
                Substep(Vector3.Lerp(from, anchor, Mathf.Clamp01(elapsed / deltaTime)), step, obstacles);
            }

            if (steps == MaxStepsPerFrame)
            {
                _accumulator = 0f;
            }

            _anchor = anchor;
            _position[0] = anchor;
            _previous[0] = anchor;
        }

        /// <summary>
        ///     The ribbon's surface at <paramref name="along" /> (0 head to 1 tail, over its full length) and
        ///     <paramref name="across" /> (-1 bottom edge, 0 middle, 1 top edge).
        /// </summary>
        internal Vector3 Surface(float along, float across)
        {
            var column = Mathf.Clamp01(along) * _settings.Columns;
            var first = Mathf.Min(Mathf.FloorToInt(column), _settings.Columns - 1);
            var t = column - first;
            var edge = across >= 0f ? Top : Bottom;
            var middle = Vector3.Lerp(Centre(first), Centre(first + 1), t);
            var rim = Vector3.Lerp(ClothPoint(first, edge), ClothPoint(first + 1, edge), t);
            return Vector3.LerpUnclamped(middle, rim, Mathf.Abs(across));
        }

        /// <summary>The ribbon's unit normal at <paramref name="along" />, pointing out of its front face.</summary>
        internal Vector3 Normal(float along)
        {
            var column = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(along) * _settings.Columns), 0, _settings.Columns);
            var ahead = Centre(Mathf.Min(column + 1, _settings.Columns));
            var behind = Centre(Mathf.Max(column - 1, 0));
            var up = ClothPoint(column, Top) - ClothPoint(column, Bottom);
            var normal = Vector3.Cross(ahead - behind, up);
            return normal.sqrMagnitude > 1e-10f ? normal.normalized : Vector3.forward;
        }

        /// <summary>How squarely the front faces the viewer at <paramref name="along" />: 1 front on, -1 its back.</summary>
        internal float Facing(float along)
        {
            return Normal(along).z;
        }

        // The middle of a column, between its edges at the notched ends (whose middle points sit in the notch).
        private Vector3 Centre(int column)
        {
            return column == 0 || column == _settings.Columns
                ? (ClothPoint(column, Top) + ClothPoint(column, Bottom)) * 0.5f
                : ClothPoint(column, Middle);
        }

        // The line's end doubles as the middle of the ribbon's head; the other ribbon points follow it.
        private int ClothIndex(int column, int row)
        {
            var flat = column * Rows + row;
            if (flat == Middle)
            {
                return _lineEnd;
            }

            return _clothStart + (flat < Middle ? flat : flat - 1);
        }

        private Vector3 RestOffset(int column, int row)
        {
            var along = (float)column / _settings.Columns * Length;
            if (row == Middle)
            {
                along = column == 0 ? _notch : column == _settings.Columns ? Length - _notch : along;
            }

            var up = row == Top ? 0.5f : row == Bottom ? -0.5f : 0f;
            return new Vector3(_heading * (along - _notch), up * _settings.Height, 0f);
        }

        private void Lay(Vector3 anchor)
        {
            var segment = _settings.LineLength / _settings.LineSegments;
            for (var i = 0; i <= _lineEnd; i++)
            {
                _position[i] = anchor + Vector3.down * (segment * i);
            }

            var pivot = _position[_lineEnd];
            for (var column = 0; column < Columns; column++)
            {
                for (var row = 0; row < Rows; row++)
                {
                    _position[ClothIndex(column, row)] = pivot + RestOffset(column, row);
                }
            }

            System.Array.Copy(_position, _previous, _position.Length);
        }

        private void BuildSprings()
        {
            var segment = _settings.LineLength / _settings.LineSegments;
            for (var i = 0; i < _lineEnd; i++)
            {
                _springs.Add(new Spring(i, i + 1, segment, _settings.LineStiffness));
            }

            var stiffness = _settings.ClothStiffness;
            for (var column = 0; column < Columns; column++)
            {
                Link(column, Top, column, Middle, stiffness);
                Link(column, Middle, column, Bottom, stiffness);
                Link(column, Top, column, Bottom, stiffness);
                if (column + 1 < Columns)
                {
                    for (var row = 0; row < Rows; row++)
                    {
                        Link(column, row, column + 1, row, stiffness);
                    }

                    Link(column, Top, column + 1, Middle, stiffness);
                    Link(column, Middle, column + 1, Top, stiffness);
                    Link(column, Middle, column + 1, Bottom, stiffness);
                    Link(column, Bottom, column + 1, Middle, stiffness);
                }

                if (column + 2 < Columns)
                {
                    for (var row = 0; row < Rows; row++)
                    {
                        Link(column, row, column + 2, row, _settings.BendStiffness);
                    }
                }
            }
        }

        private void Link(int columnA, int rowA, int columnB, int rowB, float stiffness)
        {
            var rest = Vector3.Distance(RestOffset(columnA, rowA), RestOffset(columnB, rowB));
            _springs.Add(new Spring(ClothIndex(columnA, rowA), ClothIndex(columnB, rowB), rest, stiffness));
        }

        // The breeze follows the anchor's sideways travel, streaming the ribbon behind it; a turn of the breeze is
        // what swings the tail round, so how fast it turns is kept for the push.
        private void Steer(float x, float sidewaysSpeed, float deltaTime)
        {
            var wanted = Mathf.Abs(sidewaysSpeed) > _settings.HeadingSpeed ? -Mathf.Sign(sidewaysSpeed) : _headingTarget;

            // A side without room for the ribbon gives way to the other, if that has more. Turning back takes a
            // margin over the ribbon's length, so it doesn't flutter at the edge of room.
            var need = Mathf.Approximately(wanted, _headingTarget) ? Length : Length * RoomMargin;
            if (RoomToward(wanted, x) < need && RoomToward(-wanted, x) > RoomToward(wanted, x))
            {
                wanted = -wanted;
            }

            _headingTarget = wanted;
            var before = _heading;
            var fullTurn = 2f * _settings.TurnRate;
            _heading = Mathf.MoveTowards(_heading, _headingTarget, fullTurn * deltaTime);
            _turning = Mathf.Abs(_heading - before) / (fullTurn * deltaTime);
        }

        private float RoomToward(float side, float x)
        {
            return side > 0f ? _roomRight - x : x - _roomLeft;
        }

        private void Substep(Vector3 anchor, float step, IReadOnlyList<KeepOut> obstacles)
        {
            _time += step;
            _position[0] = anchor;
            _previous[0] = anchor;
            var keep = Mathf.Clamp01(1f - _settings.Drag * step);
            var squared = step * step;
            for (var i = 1; i < _position.Length; i++)
            {
                var velocity = (_position[i] - _previous[i]) * keep;
                _previous[i] = _position[i];
                _position[i] += velocity + Force(i) * squared;
            }

            for (var pass = 0; pass < _settings.Iterations; pass++)
            {
                SolveSprings();
                HoldUpright();
                Collide(obstacles);
                _position[0] = anchor;
            }
        }

        private Vector3 Force(int index)
        {
            var gravity = Vector3.down * _settings.Gravity;
            if (index <= _lineEnd)
            {
                return gravity;
            }

            var column = (index - _clothStart + 1) / Rows;
            var along = (float)column / _settings.Columns;
            var phase = 2f * Mathf.PI * (along * Length / _settings.WaveLength - _settings.WaveSpeed * _time);
            var wave = _settings.WaveForce * along * Mathf.Sin(phase);
            return gravity * _settings.RibbonGravity +
                   new Vector3(_heading * _settings.Stream, wave * _settings.WaveLift,
                       wave * (1f - _settings.WaveLift) + _settings.SwingKick * _turning * along);
        }

        private void SolveSprings()
        {
            for (var i = 0; i < _springs.Count; i++)
            {
                var spring = _springs[i];
                var a = _position[spring.A];
                var b = _position[spring.B];
                var gap = b - a;
                var length = gap.magnitude;
                if (length < 1e-6f)
                {
                    continue;
                }

                // Each end gives way by how light it is; the anchor, weightless here, never does.
                var giveA = _inverseMass[spring.A];
                var giveB = _inverseMass[spring.B];
                var give = giveA + giveB;
                if (give <= 0f)
                {
                    continue;
                }

                var correction = gap * ((length - spring.Rest) / length * spring.Stiffness / give);
                _position[spring.A] = a + correction * giveA;
                _position[spring.B] = b - correction * giveB;
            }
        }

        // Each column's edges over each other, the column's height apart: the ribbon can turn about the vertical
        // and droop, but not roll over onto its side.
        private void HoldUpright()
        {
            var amount = _settings.Upright;
            for (var column = 0; column < Columns; column++)
            {
                var top = ClothIndex(column, Top);
                var bottom = ClothIndex(column, Bottom);
                var t = _position[top];
                var b = _position[bottom];
                var middleX = (t.x + b.x) * 0.5f;
                var middleZ = (t.z + b.z) * 0.5f;
                var stretch = (_settings.Height - (t.y - b.y)) * 0.5f * amount;
                t.x = Mathf.Lerp(t.x, middleX, amount);
                t.z = Mathf.Lerp(t.z, middleZ, amount);
                b.x = Mathf.Lerp(b.x, middleX, amount);
                b.z = Mathf.Lerp(b.z, middleZ, amount);
                t.y += stretch;
                b.y -= stretch;
                _position[top] = t;
                _position[bottom] = b;
            }
        }

        private void Collide(IReadOnlyList<KeepOut> obstacles)
        {
            if (obstacles == null)
            {
                return;
            }

            for (var o = 0; o < obstacles.Count; o++)
            {
                var obstacle = obstacles[o];
                for (var i = 1; i < _position.Length; i++)
                {
                    var point = _position[i];
                    var pushed = obstacle.PushOut(point, out _);
                    _position[i] = new Vector3(pushed.x, pushed.y, point.z);
                }
            }
        }

        private readonly struct Spring
        {
            internal readonly int A;
            internal readonly int B;
            internal readonly float Rest;
            internal readonly float Stiffness;

            internal Spring(int a, int b, float rest, float stiffness)
            {
                A = a;
                B = b;
                Rest = rest;
                Stiffness = stiffness;
            }
        }
    }
}

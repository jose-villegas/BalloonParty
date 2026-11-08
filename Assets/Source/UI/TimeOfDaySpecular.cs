using System.Collections.Generic;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI
{
    /// <summary>
    ///     A rounded UI shape's specular, lit like the balloons: a soft stroke that runs along the inside of
    ///     the shape's rim, centred where the rim faces the time-of-day light (gliding along the edges as the
    ///     light sweeps, as on a curved skin), following the corner's curve
    ///     onto the straight edges and tapering away at both ends. Its colour leans toward the light's.
    ///     Stretch it over the shape it lights. The mesh is rebuilt only when the light or the size changes.
    /// </summary>
    internal class TimeOfDaySpecular : MaskableGraphic
    {
        private const int CornerSegments = 12;
        private const int Steps = 32;

        // The scene light's rest direction (up-left), used before injection and in the editor.
        private static readonly Vector2 RestDirection = new(-0.70710677f, 0.70710677f);

        [Tooltip("The corner radius of the shape it lights, in canvas units.")]
        [SerializeField] [Min(0f)] private float _cornerRadius = 11f;

        [Tooltip("How far inside the shape's edge the highlight runs.")]
        [SerializeField] [Min(0f)] private float _inset = 2.5f;

        [Tooltip("Its length along the rim, in canvas units.")]
        [SerializeField] [Min(0f)] private float _length = 30f;

        [Tooltip("Its width at the middle; it tapers to nothing at both ends and fades at both sides.")]
        [SerializeField] [Min(0f)] private float _width = 2.5f;

        [Tooltip("How far the colour leans toward the light's. 0 = untinted, 1 = multiplied by it.")]
        [SerializeField] [Range(0f, 1f)] private float _tint = 0.35f;

        private readonly List<Vector2> _rim = new();
        private readonly List<float> _distance = new();

        private ISceneLightRuntime _lightRuntime;
        private Color _baseColor;
        private Vector2 _direction = RestDirection;
        private Color _light = Color.white;

        [Inject]
        internal void Construct(ISceneLightRuntime lightRuntime)
        {
            _lightRuntime = lightRuntime;
        }

        protected override void Awake()
        {
            base.Awake();
            _baseColor = color;
            raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (_lightRuntime == null)
            {
                return;
            }

            var direction = _lightRuntime.CurrentDirection;
            var light = _lightRuntime.CurrentColor;
            if (direction == _direction && light == _light)
            {
                return;
            }

            _direction = direction;
            _light = light;
            var lit = Color.Lerp(_baseColor, _baseColor * light, _tint);
            lit.a = _baseColor.a;
            color = lit;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var half = rect.size * 0.5f - Vector2.one * _inset;
            if (half.x <= 0f || half.y <= 0f || _length <= 0f || _width <= 0f)
            {
                return;
            }

            var radius = Mathf.Clamp(_cornerRadius - _inset, 0f, Mathf.Min(half.x, half.y));
            BuildRim(rect.center, half, radius);
            var total = _distance[_distance.Count - 1];
            var toLight = _direction.sqrMagnitude > 1e-8f ? _direction.normalized : RestDirection;

            // Where the rim faces the light, as if the shape bulged like a balloon's skin: a flat edge would
            // only catch a light square to it, parking the highlight on a corner for most of the sweep.
            var square = Mathf.Max(Mathf.Abs(toLight.x), Mathf.Abs(toLight.y));
            var middle = DistanceAt(rect.center + Vector2.Scale(toLight / square, half));
            var length = Mathf.Min(_length, total);

            var solid = color;
            var clear = new Color(solid.r, solid.g, solid.b, 0f);
            for (var i = 0; i <= Steps; i++)
            {
                var t = (float)i / Steps;
                Sample(middle + (t - 0.5f) * length, total, out var point, out var normal);
                var reach = normal * (_width * 0.5f * Mathf.Sin(Mathf.PI * t));
                vh.AddVert(point + reach, clear, Vector4.zero);
                vh.AddVert(point, solid, Vector4.zero);
                vh.AddVert(point - reach, clear, Vector4.zero);
                if (i == 0)
                {
                    continue;
                }

                var v = 3 * i;
                vh.AddTriangle(v - 3, v - 2, v + 1);
                vh.AddTriangle(v - 3, v + 1, v);
                vh.AddTriangle(v - 2, v - 1, v + 2);
                vh.AddTriangle(v - 2, v + 2, v + 1);
            }
        }

        // The rim as a closed polyline, counter-clockwise from the top-right corner, with the distance
        // along it at each point.
        private void BuildRim(Vector2 center, Vector2 half, float radius)
        {
            _rim.Clear();
            _distance.Clear();
            for (var quarter = 0; quarter < 4; quarter++)
            {
                var signX = quarter is 0 or 3 ? 1f : -1f;
                var signY = quarter < 2 ? 1f : -1f;
                var arcCenter = center + new Vector2(signX * (half.x - radius), signY * (half.y - radius));
                for (var k = 0; k <= CornerSegments; k++)
                {
                    var angle = (quarter + (float)k / CornerSegments) * 0.5f * Mathf.PI;
                    _rim.Add(arcCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }

            _rim.Add(_rim[0]);
            _distance.Add(0f);
            for (var i = 1; i < _rim.Count; i++)
            {
                _distance.Add(_distance[i - 1] + Vector2.Distance(_rim[i - 1], _rim[i]));
            }
        }

        private float DistanceAt(Vector2 point)
        {
            var nearest = 0;
            var best = float.MaxValue;
            for (var i = 0; i < _rim.Count; i++)
            {
                var gap = (_rim[i] - point).sqrMagnitude;
                if (gap < best)
                {
                    best = gap;
                    nearest = i;
                }
            }

            return _distance[nearest];
        }

        // The rim's point at a distance along it (wrapping round), and its outward normal there.
        private void Sample(float distance, float total, out Vector2 point, out Vector2 normal)
        {
            distance = Mathf.Repeat(distance, total);
            var segment = 1;
            while (segment < _distance.Count - 1 && _distance[segment] < distance)
            {
                segment++;
            }

            // Skip the zero-length steps a sharp (radius 0) corner leaves.
            var from = segment - 1;
            while (from > 0 && _distance[from] >= _distance[segment])
            {
                from--;
            }

            var a = _rim[from];
            var b = _rim[segment];
            var span = _distance[segment] - _distance[from];
            var t = span > 1e-5f ? (distance - _distance[from]) / span : 0f;
            point = Vector2.Lerp(a, b, t);
            var along = span > 1e-5f ? (b - a) / span : Vector2.left;
            normal = new Vector2(along.y, -along.x);
        }
    }
}

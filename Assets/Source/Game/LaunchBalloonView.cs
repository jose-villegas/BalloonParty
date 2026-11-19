using System.Collections.Generic;
using BalloonParty.UI.ClothRibbon;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.Game
{
    /// <summary>
    ///     A launch screen balloon drifting about the screen (<see cref="BalloonWander" />), carrying a ribbon that
    ///     says what it does on a spring line (<see cref="RibbonCloth" />). Play and Continue each have one; each
    ///     keeps clear of the other while it shows, with room for what hangs from either
    ///     (<see cref="BalloonReach" />), and each one's line and ribbon are pushed off the other's balloon if they
    ///     swing into it. Sits stretched over the launch canvas, centred on it, so its own space is the canvas's and
    ///     the two balloons share it; the line, ribbon and writing sit at its centre. The balloon it moves is a child
    ///     or a sibling: Play's is the Play button itself, where the launch canvas has it.
    /// </summary>
    public class LaunchBalloonView : MonoBehaviour
    {
        private static readonly int RotationPivotId = Shader.PropertyToID("_RotationPivot");

        [Tooltip("The balloon this moves: a child, or a sibling under the same parent.")]
        [SerializeField] private RectTransform _balloon;

        [Tooltip("The launch screen's other balloon, which this one keeps clear of while it shows. Optional.")]
        [SerializeField] private LaunchBalloonView _other;

        [SerializeField] private SpringLineGraphic _line;
        [SerializeField] private ClothRibbonGraphic _ribbon;
        [SerializeField] private ClothRibbonWriting _writing;

        [Tooltip("Where the line is tied on, in the balloon's own units from its centre.")]
        [SerializeField] private Vector2 _knot = new(0f, -56f);

        [Tooltip("The round body of the balloon art, shared by both balloons: its centre in the balloon's own units.")]
        [SerializeField] private Vector2 _bodyCentre = new(0f, 5f);

        [Tooltip("The body's radius, in the balloon's own units.")]
        [SerializeField] [Min(1f)] private float _bodyRadius = 53f;

        [Tooltip("Room kept from the screen's edges, in canvas units.")]
        [SerializeField] [Min(0f)] private float _clearance = 4f;

        [Tooltip("Room kept between this balloon's body and the other's, in canvas units. The larger of the two " +
                 "balloons' gaps is the one kept.")]
        [SerializeField] [Min(0f)] private float _gap = 16f;

        [Tooltip("Where the balloon starts, as shares of the room it drifts in: x from the left edge, y from the " +
                 "lowest it can hang. If the other balloon is in the way it starts at the nearest spot clear of it.")]
        [SerializeField] private Vector2 _start = new(0.62f, 0.16f);

        [SerializeField] private BalloonWanderSettings _wander = new();
        [SerializeField] private RibbonClothSettings _cloth = new();

        private readonly List<LitMaterial> _lit = new();
        private readonly KeepOut[] _obstacles = new KeepOut[1];

        private BalloonWander _wanderer;
        private RibbonCloth _ribbonCloth;
        private Transform _canvas;
        private Rect _area;
        private Vector2 _balloonAt;

        /// <summary>The room this balloon takes up now, in the launch canvas's space; where it will start, before it has.</summary>
        internal BalloonReach Reach => new(_wanderer != null ? _wanderer.Position : StartIn(Room()), Radius, Hang);

        private float Scale => _balloon.localScale.x;

        private float Radius => _bodyRadius * Scale;

        // From the body's centre down to the bottom of the ribbon hanging still below it.
        private float Hang => (_bodyCentre.y - _knot.y) * Scale + _cloth.LineLength + _cloth.Height * 0.5f;

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>(true);
            _canvas = canvas != null ? canvas.rootCanvas.transform : transform;
            CopyLitMaterials();
        }

        private void OnEnable()
        {
            Begin();
        }

        private void Update()
        {
            // The room is the canvas's, which can take its size after this is enabled and change it later: start
            // over in the new room.
            if (((RectTransform)transform).rect != _area)
            {
                Begin();
            }

            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            foreach (var lit in _lit)
            {
                Destroy(lit.Material);
            }
        }

        private void Begin()
        {
            _area = ((RectTransform)transform).rect;
            _ribbonCloth = new RibbonCloth(_cloth, _writing.Layout(_cloth.Height));
            var room = Room();
            _wanderer = new BalloonWander(
                _wander, room, KeepOutOfOther(), StartIn(room), Random.Range(int.MinValue, int.MaxValue));
            _ribbonCloth.SetRoom(_area.xMin + _clearance, _area.xMax - _clearance);
            PlaceBalloon();
            _ribbonCloth.Place(Knot());
            Draw();
        }

        private void Tick(float deltaTime)
        {
            if (_wanderer == null)
            {
                return;
            }

            _wanderer.KeepClearOf(KeepOutOfOther());
            _wanderer.Step(deltaTime);
            PlaceBalloon();
            _obstacles[0] = TryGetOther(out var other) ? new KeepOut(other.Centre, other.Centre, other.Radius) : default;
            _ribbonCloth.Step(Knot(), deltaTime, _obstacles);
            Draw();
        }

        // Where the body's centre may be: on screen, with room below for what hangs from it.
        private Rect Room()
        {
            var area = ((RectTransform)transform).rect;
            var edge = Radius + _clearance;
            return Rect.MinMaxRect(
                area.xMin + edge, area.yMin + Hang + _clearance, area.xMax - edge, area.yMax - edge);
        }

        private Vector2 StartIn(Rect room)
        {
            return new Vector2(
                Mathf.Lerp(room.xMin, room.xMax, _start.x), Mathf.Lerp(room.yMin, room.yMax, _start.y));
        }

        private bool TryGetOther(out BalloonReach reach)
        {
            var showing = _other != null && _other.isActiveAndEnabled;
            reach = showing ? _other.Reach : default;
            return showing;
        }

        // No other balloon showing leaves an empty keep-out, which holds nothing out.
        private KeepOut KeepOutOfOther()
        {
            return TryGetOther(out var other)
                ? other.KeepOutFor(new BalloonReach(default, Radius, Hang), Mathf.Max(_gap, _other._gap))
                : default;
        }

        private void PlaceBalloon()
        {
            _balloonAt = _wanderer.Shown - _bodyCentre * Scale;
            _balloon.position = transform.TransformPoint(_balloonAt);
            foreach (var lit in _lit)
            {
                var pivot = _canvas.InverseTransformPoint(_balloon.TransformPoint(lit.Pivot));
                lit.Material.SetVector(RotationPivotId, new Vector4(pivot.x, pivot.y, 0f, 0f));
            }
        }

        private Vector3 Knot()
        {
            return _balloonAt + _knot * Scale;
        }

        private void Draw()
        {
            _line.Draw(_ribbonCloth);
            _ribbon.Draw(_ribbonCloth);
            _writing.Bend(_ribbonCloth);
        }

        // The balloon art's light-driven materials turn their highlights about a pivot in the canvas's space, authored
        // for a balloon at the canvas's centre. This balloon moves, so it gets its own copies whose pivot follows it;
        // the authored pivot is kept as a point in the balloon's own units.
        private void CopyLitMaterials()
        {
            foreach (var graphic in _balloon.GetComponentsInChildren<Graphic>(true))
            {
                var shared = graphic.material;
                if (shared == null || shared == graphic.defaultMaterial || !shared.HasProperty(RotationPivotId))
                {
                    continue;
                }

                var copy = new Material(shared) { name = $"{shared.name} ({name})" };
                graphic.material = copy;
                _lit.Add(new LitMaterial(copy, shared.GetVector(RotationPivotId)));
            }
        }

        private readonly struct LitMaterial
        {
            internal readonly Material Material;
            internal readonly Vector2 Pivot;

            internal LitMaterial(Material material, Vector2 pivot)
            {
                Material = material;
                Pivot = pivot;
            }
        }
    }
}

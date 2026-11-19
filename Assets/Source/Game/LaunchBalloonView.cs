using System.Collections.Generic;
using BalloonParty.UI.ClothRibbon;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.Game
{
    /// <summary>
    ///     A launch screen balloon drifting about the screen, carrying a ribbon that says what it does on a spring
    ///     line (<see cref="BalloonDrift" />). Play and Continue each have one. Alone, a balloon has the whole
    ///     screen; while both show they share it one above the other (<see cref="BalloonReach" />), a gap between
    ///     everything of the upper one and everything of the lower, so neither balloon nor its ribbon can cover the
    ///     other's ribbon wherever the two drift and however their ribbons swing. Sits stretched over the launch
    ///     canvas, centred on it, so its own space is the canvas's and the two balloons share it; the line, ribbon
    ///     and writing sit at its centre. The balloon it moves is a child or a sibling: Play's is the Play button
    ///     itself, where the launch canvas has it.
    /// </summary>
    public class LaunchBalloonView : MonoBehaviour
    {
        private static readonly int RotationPivotId = Shader.PropertyToID("_RotationPivot");

        [Tooltip("The balloon this moves: a child, or a sibling under the same parent.")]
        [SerializeField] private RectTransform _balloon;

        [Tooltip("The launch screen's other balloon, which this one shares the screen with while it shows. Optional.")]
        [SerializeField] private LaunchBalloonView _other;

        [Tooltip("Sharing the screen, one balloon keeps to its upper part and the other to its lower: whether this " +
                 "is the upper one. One of the two must be.")]
        [SerializeField] private bool _above;

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

        [Tooltip("Sharing the screen, the room kept between the bottom of the upper balloon's ribbon and the top " +
                 "of the lower balloon, in canvas units, however far each has drifted. The larger of the two " +
                 "balloons' gaps is the one kept.")]
        [SerializeField] [Min(0f)] private float _gap = 16f;

        [Tooltip("Where the balloon starts, as shares of the room it drifts in: x from the left edge, y from the " +
                 "lowest it can hang.")]
        [SerializeField] private Vector2 _start = new(0.62f, 0.16f);

        [SerializeField] private BalloonWanderSettings _wander = new();
        [SerializeField] private RibbonClothSettings _cloth = new();

        private readonly List<LitMaterial> _lit = new();

        private BalloonDrift _drift;
        private Transform _canvas;
        private Rect _area;
        private bool _sharing;
        private Vector2 _balloonAt;

        /// <summary>The room this balloon takes up around its body's centre, in canvas units.</summary>
        internal BalloonReach Reach => new(Radius, Radius + _wander.Bob, Hang + _wander.Bob);

        /// <summary>From the body's centre to where the line is tied on, in canvas units.</summary>
        internal Vector2 KnotFromCentre => (_knot - _bodyCentre) * Scale;

        private float Scale => _balloon.localScale.x;

        private float Radius => _bodyRadius * Scale;

        // From the body's centre down to the bottom of the ribbon hanging below it. Swinging, the ribbon reaches no
        // lower than hanging still: the breeze and its wave lift it.
        private float Hang => (_bodyCentre.y - _knot.y) * Scale + _cloth.LineLength + _cloth.Height * 0.5f;

        // Read from the objects, not from this component's own callbacks, so it holds before the other has begun.
        private bool OtherShows => _other != null && _other.enabled && _other.gameObject.activeInHierarchy;

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
            // The room is the canvas's, which can take its size after this is enabled and change it later, less the
            // other balloon's part once that shows: start over in the new room. A balloon that has given up part of
            // the screen keeps to its own after the other goes.
            if (((RectTransform)transform).rect != _area || (OtherShows && !_sharing))
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

        /// <summary>
        ///     This balloon's drift on a screen of <paramref name="area" />, from its own settings and, if the other
        ///     balloon shows, the part of the screen that leaves it. What <see cref="Begin" /> starts; tests run the
        ///     launch screen's own numbers through it at any screen shape.
        /// </summary>
        internal BalloonDrift NewDrift(Rect area, int seed)
        {
            var cloth = new RibbonCloth(_cloth, _writing.Layout(_cloth.Height));
            cloth.SetRoom(area.xMin + _clearance, area.xMax - _clearance);
            var room = Room(area);
            return new BalloonDrift(_wander, cloth, room, StartIn(room), KnotFromCentre, seed);
        }

        private void Begin()
        {
            _area = ((RectTransform)transform).rect;
            _sharing = OtherShows;
            _drift = NewDrift(_area, Random.Range(int.MinValue, int.MaxValue));
            PlaceBalloon();
            Draw();
        }

        private void Tick(float deltaTime)
        {
            if (_drift == null)
            {
                return;
            }

            _drift.Step(deltaTime, KnotFromCentre);
            PlaceBalloon();
            Draw();
        }

        // Where the body's centre may be: on screen with room below for what hangs from it, and in this balloon's
        // part of the screen while the other shows.
        private Rect Room(Rect area)
        {
            var reach = Reach;
            if (!OtherShows)
            {
                return reach.RoomAlone(area, _clearance);
            }

            var clearance = Mathf.Max(_clearance, _other._clearance);
            var gap = Mathf.Max(_gap, _other._gap);
            return _above
                ? reach.RoomAbove(_other.Reach, area, clearance, gap)
                : reach.RoomBelow(_other.Reach, area, clearance, gap);
        }

        private Vector2 StartIn(Rect room)
        {
            return new Vector2(
                Mathf.Lerp(room.xMin, room.xMax, _start.x), Mathf.Lerp(room.yMin, room.yMax, _start.y));
        }

        private void PlaceBalloon()
        {
            _balloonAt = _drift.Centre - _bodyCentre * Scale;
            _balloon.position = transform.TransformPoint(_balloonAt);
            foreach (var lit in _lit)
            {
                var pivot = _canvas.InverseTransformPoint(_balloon.TransformPoint(lit.Pivot));
                lit.Material.SetVector(RotationPivotId, new Vector4(pivot.x, pivot.y, 0f, 0f));
            }
        }

        private void Draw()
        {
            _line.Draw(_drift.Cloth);
            _ribbon.Draw(_drift.Cloth);
            _writing.Bend(_drift.Cloth);
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

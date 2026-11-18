using System;
using System.Collections.Generic;
using BalloonParty.UI.ClothRibbon;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace BalloonParty.Game
{
    /// <summary>
    ///     The launch screen's Continue: a white balloon drifting about the screen, shown only while a run is saved,
    ///     carrying a ribbon that says so on a spring line. It keeps clear of Play (<see cref="BalloonWander" />),
    ///     with room below for what hangs from it; the line and ribbon are simulated (<see cref="RibbonCloth" />) and
    ///     pushed off Play's balloon if they swing into it. Wire <see cref="Continue" /> to the balloon's
    ///     <c>ScaleDownButton</c> On Scale Complete, as Play is. <see cref="LaunchContinueController" /> decides when it
    ///     shows and what a press does. Sits stretched over the launch canvas, centred on it, so its own space is the
    ///     canvas's; the line, ribbon and writing sit at its centre.
    /// </summary>
    public class LaunchContinueView : MonoBehaviour
    {
        private static readonly int RotationPivotId = Shader.PropertyToID("_RotationPivot");

        [SerializeField] private RectTransform _balloon;

        [Tooltip("Play's balloon, which this one keeps clear of.")]
        [SerializeField] private RectTransform _play;

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

        [Tooltip("Room kept between this balloon and Play's, its tail included, in canvas units.")]
        [SerializeField] [Min(0f)] private float _playGap = 16f;

        [Tooltip("Where the balloon starts, as shares of the room it drifts in: x from the left edge, y from the " +
                 "lowest it can hang. Off the bottom right corner, so the ribbon has room to fall and swing from " +
                 "the first frame.")]
        [SerializeField] private Vector2 _start = new(0.62f, 0.16f);

        [SerializeField] private BalloonWanderSettings _wander = new();
        [SerializeField] private RibbonClothSettings _cloth = new();

        private readonly Subject<Unit> _pressed = new();
        private readonly List<LitMaterial> _lit = new();
        private readonly KeepOut[] _obstacles = new KeepOut[1];

        private BalloonWander _wanderer;
        private RibbonCloth _ribbonCloth;
        private Transform _canvas;

        internal IObservable<Unit> Pressed => _pressed;

        private float Scale => _balloon.localScale.x;

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
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _pressed.Dispose();
            foreach (var lit in _lit)
            {
                Destroy(lit.Material);
            }
        }

        public void Continue()
        {
            _pressed.OnNext(Unit.Default);
        }

        internal void Show()
        {
            gameObject.SetActive(true);
        }

        internal void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Begin()
        {
            _ribbonCloth = new RibbonCloth(_cloth, _writing.Layout(_cloth.Height));
            var area = ((RectTransform)transform).rect;
            var radius = _bodyRadius * Scale;
            var playScale = _play.localScale.x;
            var playCentre = (Vector2)_play.localPosition + _bodyCentre * playScale;
            var playRadius = _bodyRadius * playScale;

            // From the body's centre down to the bottom of the ribbon hanging still below it.
            var hang = (_bodyCentre.y - _knot.y) * Scale + _cloth.LineLength + _cloth.Height * 0.5f;
            var bounds = Rect.MinMaxRect(
                area.xMin + radius + _clearance, area.yMin + hang + _clearance,
                area.xMax - radius - _clearance, area.yMax - radius - _clearance);

            // Below Play its tail hangs past the body down to the knot, so the keep-out reaches down to keep the gap
            // from the tail too; above Play the ribbon hangs toward it, so the keep-out reaches up by the hang.
            var tail = Mathf.Max(0f, (_bodyCentre.y - _knot.y) * playScale - playRadius);
            var keepOut = new KeepOut(
                playCentre + Vector2.down * tail, playCentre + Vector2.up * hang, playRadius + radius + _playGap);
            var start = new Vector2(
                Mathf.Lerp(bounds.xMin, bounds.xMax, _start.x), Mathf.Lerp(bounds.yMin, bounds.yMax, _start.y));
            _wanderer = new BalloonWander(_wander, bounds, keepOut, start, Random.Range(int.MinValue, int.MaxValue));
            _obstacles[0] = new KeepOut(playCentre, playCentre, playRadius);
            _ribbonCloth.SetRoom(area.xMin + _clearance, area.xMax - _clearance);
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

            _wanderer.Step(deltaTime);
            PlaceBalloon();
            _ribbonCloth.Step(Knot(), deltaTime, _obstacles);
            Draw();
        }

        private void PlaceBalloon()
        {
            _balloon.localPosition = _wanderer.Shown - _bodyCentre * Scale;
            foreach (var lit in _lit)
            {
                var pivot = _canvas.InverseTransformPoint(_balloon.TransformPoint(lit.Pivot));
                lit.Material.SetVector(RotationPivotId, new Vector4(pivot.x, pivot.y, 0f, 0f));
            }
        }

        private Vector3 Knot()
        {
            return (Vector2)_balloon.localPosition + _knot * Scale;
        }

        private void Draw()
        {
            _line.Draw(_ribbonCloth);
            _ribbon.Draw(_ribbonCloth);
            _writing.Bend(_ribbonCloth);
        }

        // The balloon art's light-driven materials turn their highlights about a pivot in the canvas's space, authored
        // for Play's balloon at the canvas's centre. This balloon moves, so it gets its own copies whose pivot follows
        // it; the authored pivot is kept as a point in the balloon's own units.
        private void CopyLitMaterials()
        {
            foreach (var graphic in _balloon.GetComponentsInChildren<Graphic>(true))
            {
                var shared = graphic.material;
                if (shared == null || shared == graphic.defaultMaterial || !shared.HasProperty(RotationPivotId))
                {
                    continue;
                }

                var copy = new Material(shared) { name = shared.name + " (Continue)" };
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

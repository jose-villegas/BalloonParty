using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Shield;
using BalloonParty.Shared;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Thrower;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace BalloonParty.Editor.ShieldChains
{
    /// <summary>
    ///     Counts the opening angles that collect every shield on the board, and draws them one at a
    ///     time in the Scene view.
    /// </summary>
    /// <remarks>
    ///     Shields planned as a chain still look like items on a hex lattice, so whether the board
    ///     actually holds a chain is not answerable by looking at it. Drawing every opening at once is
    ///     just as unreadable — a count, plus one path at a time, is what makes the answer legible.
    /// </remarks>
    internal sealed class ShieldChainWindow : EditorWindow
    {
        // Finer than the planner's own fan: this is measuring, not choosing, so resolution is free.
        private const int FanSamples = 61;

        private readonly List<ShieldHostCandidate> _shields = new();
        private readonly List<Vector2> _path = new();
        private readonly List<int> _collected = new();
        private readonly List<DeflectorCircle> _deflectors = new();

        // Every opening that collects at least one shield, with how many it took, plus the subset
        // currently above the threshold.
        private readonly List<float> _openingAngles = new();
        private readonly List<int> _openingCounts = new();
        private readonly List<int> _matching = new();

        private int _current;
        private int _bestCollected;
        private float _fanMinDegrees = 25f;
        private float _fanMaxDegrees = 155f;
        private int _threshold = 1;
        private string _status = "Enter play mode and press Scan.";

        [MenuItem("Tools/BalloonParty/Shield Chains")]
        private static void Open()
        {
            GetWindow<ShieldChainWindow>("Shield Chains");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(_status, MessageType.None);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Scan board"))
                {
                    Scan();
                }
            }

            using (new EditorGUI.DisabledScope(_bestCollected < 1))
            {
                // Defaults to the best any opening manages, so the first thing shown is the strongest
                // chain on the board. Lower it to see the near misses — "no shot takes all four, but
                // nine take three" is the answer that tells you whether placement or budget is at
                // fault, and a bare zero never does.
                var threshold = EditorGUILayout.IntSlider(
                    "Collect at least", _threshold, 1, Mathf.Max(1, _bestCollected));
                if (threshold != _threshold)
                {
                    _threshold = threshold;
                    RebuildMatching();
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUI.DisabledScope(_matching.Count == 0))
            {
                if (GUILayout.Button("Next opening (" + _matching.Count + " found)"))
                {
                    _current = _matching.Count == 0 ? 0 : (_current + 1) % _matching.Count;
                    BuildPath();
                    SceneView.RepaintAll();
                }
            }
        }

        // Every angle that collects ALL of them. A chain is one flight or it is not a chain, which is
        // the whole thing this window exists to check.
        private void Scan()
        {
            _openingAngles.Clear();
            _openingCounts.Clear();
            _matching.Clear();
            _path.Clear();
            _current = 0;
            _bestCollected = 0;

            if (!TryResolve(out var context))
            {
                _status = "No running game scope, or the thrower has not started.";
                Repaint();
                return;
            }

            CollectShields(context);
            if (_shields.Count == 0)
            {
                _status = "No shields on the board.";
                Repaint();
                return;
            }

            var planner = new ShieldChainPlanner(context.Walls, _deflectors, context.ChainSettings);
            for (var i = 0; i < FanSamples; i++)
            {
                planner.PlanChain(
                    context.Origin, DirectionAt(i), context.Shields, _shields.Count, _shields, _collected);
                if (_collected.Count == 0)
                {
                    continue;
                }

                _openingAngles.Add(AngleAt(i));
                _openingCounts.Add(_collected.Count);
                _bestCollected = Mathf.Max(_bestCollected, _collected.Count);
            }

            _threshold = Mathf.Max(1, _bestCollected);
            RebuildMatching();

            _status = _bestCollected == 0
                ? _shields.Count + " shields, and no opening reaches any of them."
                : _shields.Count + " shields on the board; the best opening collects " + _bestCollected
                    + ".";

            BuildPath();
            SceneView.RepaintAll();
            Repaint();
        }

        // Openings meeting the current floor, best-collecting first so stepping starts at the
        // strongest and degrades, rather than wandering the fan in angle order.
        private void RebuildMatching()
        {
            _matching.Clear();
            for (var i = 0; i < _openingCounts.Count; i++)
            {
                if (_openingCounts[i] >= _threshold)
                {
                    _matching.Add(i);
                }
            }

            _matching.Sort((a, b) => _openingCounts[b].CompareTo(_openingCounts[a]));
            _current = 0;
            BuildPath();
        }

        private void BuildPath()
        {
            _path.Clear();
            if (_matching.Count == 0 || !TryResolve(out var context))
            {
                return;
            }

            var radians = _openingAngles[_matching[_current]] * Mathf.Deg2Rad;
            var planner = new ShieldChainPlanner(context.Walls, _deflectors, context.ChainSettings);
            planner.PlanChain(
                context.Origin, new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), context.Shields,
                _shields.Count, _shields, _collected, _path);
        }

        private void CollectShields(in ChainContext context)
        {
            _shields.Clear();
            var radius = context.GridConfig.SlotSeparation.x;
            for (var col = 0; col < context.Grid.Columns; col++)
            {
                for (var row = 0; row < context.Grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (context.Grid.IsEmpty(col, row)
                        || context.Grid.At(slot) is not IHasItemSlot host
                        || host.Item.Value != ItemType.Shield)
                    {
                        continue;
                    }

                    _shields.Add(new ShieldHostCandidate(context.Grid.IndexToWorldPosition(slot), radius));
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_shields.Count == 0)
            {
                return;
            }

            Handles.color = Color.yellow;
            for (var i = 0; i < _shields.Count; i++)
            {
                Handles.DrawWireDisc(_shields[i].Center, Vector3.forward, _shields[i].ContactRadius);
            }

            Handles.color = Color.cyan;
            for (var i = 1; i < _path.Count; i++)
            {
                Handles.DrawAAPolyLine(4f, _path[i - 1], _path[i]);
            }

            if (_matching.Count > 0)
            {
                var index = _matching[_current];
                Handles.Label(
                    _path.Count > 0 ? (Vector3)_path[0] : Vector3.zero,
                    "opening " + (_current + 1) + "/" + _matching.Count + " at "
                    + _openingAngles[index].ToString("0.0") + "° — collects " + _openingCounts[index]);
            }
        }

        // Spans the authored fan, so the window measures the openings the planner considered.
        private float AngleAt(int index)
        {
            return Mathf.Lerp(_fanMinDegrees, _fanMaxDegrees, (float)index / (FanSamples - 1));
        }

        private Vector2 DirectionAt(int index)
        {
            var radians = AngleAt(index) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private bool TryResolve(out ChainContext context)
        {
            context = default;
            if (!Application.isPlaying)
            {
                return false;
            }

            var scope = Object.FindFirstObjectByType<ThrowerLifetimeScope>();
            if (scope == null)
            {
                return false;
            }

            var origin = scope.Container.Resolve<ThrowerOriginProvider>();
            if (!origin.IsAvailable)
            {
                return false;
            }

            var flightConfig = scope.Container.Resolve<IProjectileFlightConfig>();
            _deflectors.Clear();
            scope.Container.Resolve<IDeflectorField>().CollectDeflectors(_deflectors);

            var chainSettings = scope.Container.Resolve<IRunConfig>().ShieldChain;
            _fanMinDegrees = chainSettings.FanMinDegrees;
            _fanMaxDegrees = chainSettings.FanMaxDegrees;

            context = new ChainContext(
                scope.Container.Resolve<SlotGrid>(),
                scope.Container.Resolve<ISlotGridConfig>(),
                new WallLimits(flightConfig.LimitsClockwise),
                origin.Origin,
                flightConfig.ProjectileStartingShields,
                chainSettings);
            return true;
        }

        private readonly struct ChainContext
        {
            public readonly SlotGrid Grid;
            public readonly ISlotGridConfig GridConfig;
            public readonly WallLimits Walls;
            public readonly Vector2 Origin;
            public readonly int Shields;
            public readonly IShieldChainSettings ChainSettings;

            public ChainContext(
                SlotGrid grid, ISlotGridConfig gridConfig, WallLimits walls, Vector2 origin, int shields,
                IShieldChainSettings chainSettings)
            {
                Grid = grid;
                GridConfig = gridConfig;
                Walls = walls;
                Origin = origin;
                Shields = shields;
                ChainSettings = chainSettings;
            }
        }
    }
}

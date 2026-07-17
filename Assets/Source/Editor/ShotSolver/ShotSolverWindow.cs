using System.Collections.Generic;
using System.Diagnostics;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game;
using BalloonParty.Nudge;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Thrower;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace BalloonParty.Editor.ShotSolver
{
    /// <summary>
    ///     Sweeps aim angle against the live board (see @ref plan_shot_geometry) and reports the
    ///     windows that reach a target score — the editor half of the shot-geometry solver, Task 2.
    ///     Critical-angle enumeration (exact tangency breakpoints, plan §2) is v2; this sweeps N
    ///     samples and refines each window edge by bisection instead.
    /// </summary>
    internal sealed class ShotSolverWindow : EditorWindow
    {
        private const int DefaultSampleCount = 2048;
        private const int DefaultTargetScore = 700;
        private const float DefaultMinWindowWidthDegrees = 1.5f;
        private const float DefaultArcMinDegrees = 10f;
        private const float DefaultArcMaxDegrees = 170f;
        private const float BoundaryPrecisionDegrees = 0.01f;
        private const float PathThicknessPixels = 4f;
        private const float GamePathThicknessWorld = 0.05f;
        private const int MaxBisectionIterations = 30;
        private const float StripHeight = 120f;

        private static readonly Color PathColor = Color.red;
        private static readonly Color QualifyingColor = new(0.35f, 0.75f, 0.35f);
        private static readonly Color NonQualifyingColor = new(0.4f, 0.4f, 0.4f);
        private static readonly Color TargetLineColor = new(0.9f, 0.75f, 0.2f);

        private readonly List<ShotSolverWindowEntry> _windows = new();
        private readonly List<Vector2> _bestWindowPath = new();

        private int _sampleCount = DefaultSampleCount;
        private int _targetScore = DefaultTargetScore;
        private float _minWindowWidthDegrees = DefaultMinWindowWidthDegrees;
        private float _arcMinDegrees = DefaultArcMinDegrees;
        private float _arcMaxDegrees = DefaultArcMaxDegrees;
        private bool _drawBest;
        private float[] _sweepScores;
        private int _bestWindowIndex = -1;
        private string _lastRunSummary = "No sweep run yet.";
        private float _bestAngleDegrees = float.NaN;
        private Vector2 _windowListScroll;
        private WorldPolylineGizmo _pathGizmo;

        [MenuItem("Tools/BalloonParty/Shot Solver")]
        private static void Open()
        {
            GetWindow<ShotSolverWindow>("Shot Solver");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyPathGizmo();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode — the solver reads the live board, thrower, and projectile.",
                    MessageType.Info);
                return;
            }

            DrawControls();
            EditorGUILayout.Space();
            DrawStrip(GUILayoutUtility.GetRect(0, StripHeight, GUILayout.ExpandWidth(true)));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(_lastRunSummary, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();
            DrawWindowList();
        }

        private void DrawControls()
        {
            EditorGUILayout.BeginHorizontal();
            _arcMinDegrees = EditorGUILayout.FloatField("Arc Min °", _arcMinDegrees);
            _arcMaxDegrees = EditorGUILayout.FloatField("Arc Max °", _arcMaxDegrees);
            _sampleCount = Mathf.Max(2, EditorGUILayout.IntField("Samples", _sampleCount));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _targetScore = EditorGUILayout.IntField("Target Score", _targetScore);
            _minWindowWidthDegrees = Mathf.Max(0f, EditorGUILayout.FloatField("Min Window °", _minWindowWidthDegrees));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Sweep"))
            {
                RunSweep();
            }

            EditorGUI.BeginChangeCheck();
            _drawBest = EditorGUILayout.ToggleLeft("Draw Best (Scene view)", _drawBest, GUILayout.Width(170));
            if (EditorGUI.EndChangeCheck())
            {
                SyncPathGizmo();
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(float.IsNaN(_bestAngleDegrees)))
            {
                if (GUILayout.Button($"Fire Best ({(float.IsNaN(_bestAngleDegrees) ? 0f : _bestAngleDegrees):F2}°)"))
                {
                    FireBestShot();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStrip(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            if (_sweepScores == null || _sweepScores.Length == 0)
            {
                EditorGUI.HelpBox(rect, "Run a sweep to see score vs. aim angle.", MessageType.None);
                return;
            }

            var maxScore = 1f;
            foreach (var score in _sweepScores)
            {
                maxScore = Mathf.Max(maxScore, score);
            }

            var barWidth = rect.width / _sweepScores.Length;
            for (var i = 0; i < _sweepScores.Length; i++)
            {
                var normalized = Mathf.Clamp01(_sweepScores[i] / maxScore);
                var barHeight = rect.height * normalized;
                var barRect = new Rect(rect.x + (i * barWidth), rect.yMax - barHeight, Mathf.Max(1f, barWidth), barHeight);
                EditorGUI.DrawRect(barRect, _sweepScores[i] >= _targetScore ? QualifyingColor : NonQualifyingColor);
            }

            var targetY = rect.yMax - (rect.height * Mathf.Clamp01(_targetScore / maxScore));
            EditorGUI.DrawRect(new Rect(rect.x, targetY, rect.width, 1f), TargetLineColor);
        }

        private void DrawWindowList()
        {
            EditorGUILayout.LabelField("Qualifying Windows", EditorStyles.boldLabel);

            if (_windows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No window ≥ {_minWindowWidthDegrees:F2}° qualifies at the current target score.",
                    MessageType.Info);
                return;
            }

            _windowListScroll = EditorGUILayout.BeginScrollView(_windowListScroll, GUILayout.Height(140));
            for (var i = 0; i < _windows.Count; i++)
            {
                var window = _windows[i];
                var marker = i == _bestWindowIndex ? "   ★ best" : string.Empty;
                EditorGUILayout.LabelField(
                    $"{window.FromDegrees:F2}°  →  {window.ToDegrees:F2}°   width {window.WidthDegrees:F2}°   " +
                    $"score {window.CenterScore}   pops {window.CenterPops}   toughs {window.CenterToughsCleared}{marker}");
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_drawBest || _bestWindowPath.Count < 2)
            {
                return;
            }

            using var pointsScope = UnityEngine.Pool.ListPool<Vector3>.Get(out var points);
            foreach (var point in _bestWindowPath)
            {
                points.Add(point);
            }

            SceneDrawingHelper.DrawWorldPolyline(points, PathColor, PathThicknessPixels);
        }

        private void RunSweep()
        {
            if (!TryGatherLiveContext(out var context))
            {
                _lastRunSummary = "No live board/thrower/projectile found — is the Game scene running?";
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var workingSet = new ShotBalloonState[context.Board.Count];
            _sweepScores = new float[_sampleCount];
            var qualifies = new bool[_sampleCount];
            var cappedCount = 0;

            var bestSampleIndex = 0;
            for (var i = 0; i < _sampleCount; i++)
            {
                var angleDegrees = SampleAngle(i);
                var result = SimulateAt(angleDegrees, context, workingSet);

                _sweepScores[i] = result.RawScore;
                qualifies[i] = result.RawScore >= _targetScore;
                if (_sweepScores[i] > _sweepScores[bestSampleIndex])
                {
                    bestSampleIndex = i;
                }

                if (result.Capped)
                {
                    cappedCount++;
                }
            }

            BuildWindows(qualifies, context, workingSet);

            // No qualifying window still deserves a drawable answer: fall back to the single
            // best-scoring sample so "Draw Best" always shows the strongest shot on this board.
            var usedFallback = _bestWindowIndex < 0;
            if (usedFallback)
            {
                _bestAngleDegrees = SampleAngle(bestSampleIndex);
                SimulateAt(_bestAngleDegrees, context, workingSet, _bestWindowPath);
            }

            stopwatch.Stop();

            _lastRunSummary =
                $"{_sampleCount} angles in {stopwatch.ElapsedMilliseconds} ms — {_windows.Count} window(s) " +
                $"≥ {_minWindowWidthDegrees:F2}°, {cappedCount} capped run(s)" +
                (usedFallback
                    ? $"; no window qualifies — drawing best single angle {SampleAngle(bestSampleIndex):F2}° " +
                      $"(score {_sweepScores[bestSampleIndex]:F0})"
                    : string.Empty);

            SyncPathGizmo();
            SceneView.RepaintAll();
        }

        private void BuildWindows(IReadOnlyList<bool> qualifies, in LiveContext context, ShotBalloonState[] workingSet)
        {
            _windows.Clear();
            _bestWindowIndex = -1;
            _bestWindowPath.Clear();
            _bestAngleDegrees = float.NaN;

            var bestWidth = -1f;
            var runStart = -1;

            for (var i = 0; i <= qualifies.Count; i++)
            {
                var isQualifying = i < qualifies.Count && qualifies[i];
                if (isQualifying && runStart < 0)
                {
                    runStart = i;
                    continue;
                }

                if (isQualifying || runStart < 0)
                {
                    continue;
                }

                var runEnd = i - 1;
                var fromDegrees = runStart == 0
                    ? SampleAngle(0)
                    : RefineBoundary(SampleAngle(runStart - 1), SampleAngle(runStart), context, workingSet);
                var toDegrees = runEnd == qualifies.Count - 1
                    ? SampleAngle(runEnd)
                    : RefineBoundary(SampleAngle(runEnd + 1), SampleAngle(runEnd), context, workingSet);

                TryAddWindow(fromDegrees, toDegrees, context, workingSet, ref bestWidth);
                runStart = -1;
            }

            if (_bestWindowIndex >= 0)
            {
                var best = _windows[_bestWindowIndex];
                var centerDegrees = (best.FromDegrees + best.ToDegrees) * 0.5f;
                _bestAngleDegrees = centerDegrees;
                SimulateAt(centerDegrees, context, workingSet, _bestWindowPath);
            }
        }

        private void TryAddWindow(
            float fromDegrees, float toDegrees, in LiveContext context, ShotBalloonState[] workingSet, ref float bestWidth)
        {
            var widthDegrees = toDegrees - fromDegrees;
            if (widthDegrees < _minWindowWidthDegrees)
            {
                return;
            }

            var centerDegrees = (fromDegrees + toDegrees) * 0.5f;
            var centerResult = SimulateAt(centerDegrees, context, workingSet);

            _windows.Add(new ShotSolverWindowEntry(
                fromDegrees, toDegrees, widthDegrees, centerResult.RawScore, centerResult.Pops,
                centerResult.ToughsCleared));

            if (widthDegrees > bestWidth)
            {
                bestWidth = widthDegrees;
                _bestWindowIndex = _windows.Count - 1;
            }
        }

        // Binary search for the score-class transition between a sample known NOT to qualify and one
        // known to qualify, to within BoundaryPrecisionDegrees — the plan's "~0.01°" fair-window
        // resolution (§2).
        private float RefineBoundary(
            float outsideDegrees, float insideDegrees, in LiveContext context, ShotBalloonState[] workingSet)
        {
            for (var iteration = 0;
                 iteration < MaxBisectionIterations && Mathf.Abs(insideDegrees - outsideDegrees) > BoundaryPrecisionDegrees;
                 iteration++)
            {
                var midDegrees = (outsideDegrees + insideDegrees) * 0.5f;
                var midQualifies = SimulateAt(midDegrees, context, workingSet).RawScore >= _targetScore;
                if (midQualifies)
                {
                    insideDegrees = midDegrees;
                }
                else
                {
                    outsideDegrees = midDegrees;
                }
            }

            return insideDegrees;
        }

        private static ShotSimulationResult SimulateAt(
            float angleDegrees, in LiveContext context, ShotBalloonState[] workingSet, List<Vector2> pathOut = null)
        {
            return ShotSimulator.Simulate(
                context.Board, context.WallLimitsClockwise, OriginForAngle(angleDegrees, context),
                DirectionFromDegrees(angleDegrees), context.StartingShields, context.ProjectileContactRadius,
                workingSet, pathOut: pathOut, projectileSpeed: context.ProjectileSpeed,
                cruiseConfig: context.CruiseConfig, dynamics: context.Dynamics);
        }

        // The thrower rotates around its pivot to aim (ThrowerView.RotateTo: fire-direction angle − 90°),
        // carrying the child spawn point with it — so the true launch origin orbits the pivot per angle.
        // A fixed snapshot origin makes the drawn path miss the projectile's actual tip.
        private static Vector2 OriginForAngle(float angleDegrees, in LiveContext context)
        {
            var rotation = Quaternion.AngleAxis(angleDegrees - 90f, Vector3.forward);
            return context.ThrowerPivot + (Vector2)(rotation * context.SpawnLocalOffset);
        }

        // The Game view only renders gizmos from scene objects, so the path rides a play-mode
        // GameObject (auto-destroyed on play exit — no DontSave flags) rather than the window's
        // Handles. It must stay VISIBLE in the hierarchy: Unity's annotation system skips gizmos
        // on HideInHierarchy objects, which silently kills the Game-view drawing.
        private void SyncPathGizmo()
        {
            if (!_drawBest || _bestWindowPath.Count < 2 || !Application.isPlaying)
            {
                DestroyPathGizmo();
                return;
            }

            if (_pathGizmo == null)
            {
                var host = new GameObject("Shot Solver Path (editor only)");
                _pathGizmo = host.AddComponent<WorldPolylineGizmo>();
            }

            _pathGizmo.SetPath(_bestWindowPath, PathColor, GamePathThicknessWorld);
        }

        private void DestroyPathGizmo()
        {
            if (_pathGizmo != null)
            {
                DestroyImmediate(_pathGizmo.gameObject);
                _pathGizmo = null;
            }
        }

        // The sweep and the live shot share the exact same origin/direction math, so firing the
        // reported angle reproduces the drawn path (deterministic flight).
        private void FireBestShot()
        {
            // The board keeps moving after a sweep (line spawns, balance drift, nudge wobble), so any
            // stored angle is stale by click time — recompute against the live board and fire fresh.
            RunSweep();
            if (float.IsNaN(_bestAngleDegrees))
            {
                return;
            }

            var scope = UnityEngine.Object.FindFirstObjectByType<ThrowerLifetimeScope>();
            if (scope == null)
            {
                return;
            }

            scope.Container.Resolve<ThrowerController>().FireAt(DirectionFromDegrees(_bestAngleDegrees));
        }

        private float SampleAngle(int index)
        {
            var t = _sampleCount <= 1 ? 0f : (float)index / (_sampleCount - 1);
            return Mathf.Lerp(_arcMinDegrees, _arcMaxDegrees, t);
        }

        private static Vector2 DirectionFromDegrees(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static bool TryGatherLiveContext(out LiveContext context)
        {
            context = default;

            var scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                return false;
            }

            var thrower = UnityEngine.Object.FindFirstObjectByType<ThrowerView>();
            if (thrower == null)
            {
                return false;
            }

            var grid = scope.Container.Resolve<SlotGrid>();
            var config = scope.Container.Resolve<IGameConfiguration>();
            var balloonsConfig = scope.Container.Resolve<IBalloonsConfiguration>();
            var throwerSettings = scope.Container.Resolve<ThrowerSettings>();

            var targets = new List<ShotBalloonSnapshot>();
            var otherDynamicActors = new List<ShotDynamicActorSnapshot>();
            var staticActors = new List<ShotStaticActorSnapshot>();
            CollectBoard(grid, targets, otherDynamicActors, staticActors);

            // ~0.5 frame of interval-crossing quantization + 1 frame of deferred-Balance yield, at
            // whatever rate the editor is actually rendering right now.
            var pulseDelay = Mathf.Clamp(1.5f * Time.smoothDeltaTime, 0f, 0.1f);
            var dynamics = new ShotBoardDynamics(
                config, balloonsConfig, targets, otherDynamicActors, staticActors, pulseDelay);
            var cruiseConfig = new ShotCruiseConfig(
                config.CruiseWallBounceThreshold, config.CruiseSpeedPerShield,
                config.CruiseTapEaseDuration, config.CruiseTapCurve);

            // Un-rotate the spawn point back into the thrower's aim-neutral frame so the sweep can
            // re-rotate it for every candidate angle.
            var spawnLocalOffset =
                Quaternion.Inverse(thrower.Rotation) * (thrower.SpawnPointPosition - thrower.Position);

            context = new LiveContext(
                targets,
                config.LimitsClockwise,
                thrower.Position,
                spawnLocalOffset,
                config.ProjectileStartingShields,
                ResolveProjectileContactRadius(throwerSettings),
                config.ProjectileSpeed,
                cruiseConfig,
                dynamics);
            return true;
        }

        // Every occupied slot feeds the dynamic-board sim's SlotGrid (task 4b): poppable/deflectable
        // shot targets — including never-popping Unbreakables — go to `targets` (geometry +
        // balance/nudge properties); any other Dynamic occupant goes to `otherDynamicActors` (balance
        // properties only, no collision geometry); every Static occupant (obstacles, gatekeepers, ...)
        // goes to `staticActors` (slot only — BalancePlanner never moves it).
        private static void CollectBoard(
            SlotGrid grid, List<ShotBalloonSnapshot> targets, List<ShotDynamicActorSnapshot> otherDynamicActors,
            List<ShotStaticActorSnapshot> staticActors)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var index = new Vector2Int(col, row);
                    var actor = grid.At(index);

                    if (actor.Kind == SlotActorKind.Static)
                    {
                        staticActors.Add(new ShotStaticActorSnapshot(index));
                        continue;
                    }

                    if (TryBuildTargetSnapshot(grid, index, actor, out var target))
                    {
                        targets.Add(target);
                        continue;
                    }

                    var influence = actor as IBalanceInfluence;
                    otherDynamicActors.Add(new ShotDynamicActorSnapshot(
                        index, influence?.BalancePriority ?? 0, influence?.MaxBalanceSteps ?? 0,
                        influence?.DirectBalanceMotion ?? false));
                }
            }
        }

        // Durable + scorable actors are poppable/deflectable targets. Unbreakables have no
        // IHasDurability (EvaluateHit never mutates HitsRemaining) yet still DEFLECT the live shot,
        // so they enter as never-popping deflect geometry — int.MaxValue durability keeps the sim's
        // HitsRemaining > 1 branch permanently deflecting (deflects score nothing, matching the game).
        private static bool TryBuildTargetSnapshot(
            SlotGrid grid, Vector2Int index, IWriteableSlotActor actor, out ShotBalloonSnapshot snapshot)
        {
            snapshot = default;

            int hitsRemaining;
            if (actor is IHasDurability durable)
            {
                hitsRemaining = durable.HitsRemaining.Value;
            }
            else if (actor is UnbreakableBalloonModel)
            {
                hitsRemaining = int.MaxValue;
            }
            else
            {
                return false;
            }

            if (actor is not IHasScore scored)
            {
                return false;
            }

            var colorId = actor is IHasColor colorable ? colorable.Color.Value : null;
            var influence = actor as IBalanceInfluence;
            var nudgeOverrides = actor is IHasNudge nudgeable ? nudgeable.NudgeOverrides : null;

            // The view's live position, not the slot's lattice home: balance tweens and nudge wobble
            // displace views, and the shot collides with the view's collider — the slot is where the
            // balloon belongs, the view is where it IS right now.
            var view = grid.ActorViewAt<BalloonView>(index);
            var radius = view != null ? view.ContactRadius : 0f;
            var position = view != null
                ? (Vector2)view.transform.position
                : (Vector2)grid.IndexToWorldPosition(index);

            snapshot = new ShotBalloonSnapshot(
                position, radius, colorId, scored.ScoreValue, hitsRemaining, index,
                influence?.BalancePriority ?? 0, influence?.MaxBalanceSteps ?? 0,
                influence?.DirectBalanceMotion ?? false, nudgeOverrides);
            return true;
        }

        // Mirrors ProjectileView.Awake's own (private) contact-radius derivation — a capsule's
        // cross-section half-extent, or a circle's radius, scaled by the prefab's world scale.
        private static float ResolveProjectileContactRadius(ThrowerSettings settings)
        {
            var prefabView = settings?.ProjectilePrefab;
            if (prefabView == null)
            {
                return 0f;
            }

            var collider = prefabView.GetComponent<Collider2D>();
            return collider switch
            {
                CircleCollider2D circle => circle.radius * prefabView.transform.lossyScale.x,
                CapsuleCollider2D capsule =>
                    Mathf.Min(capsule.size.x, capsule.size.y) * 0.5f * prefabView.transform.lossyScale.x,
                _ => 0f,
            };
        }

        private readonly struct LiveContext
        {
            public readonly IReadOnlyList<ShotBalloonSnapshot> Board;
            public readonly Vector4 WallLimitsClockwise;
            public readonly Vector2 ThrowerPivot;
            public readonly Vector3 SpawnLocalOffset;
            public readonly int StartingShields;
            public readonly float ProjectileContactRadius;
            public readonly float ProjectileSpeed;
            public readonly ShotCruiseConfig CruiseConfig;
            public readonly ShotBoardDynamics Dynamics;

            public LiveContext(
                IReadOnlyList<ShotBalloonSnapshot> board, Vector4 wallLimitsClockwise, Vector2 throwerPivot,
                Vector3 spawnLocalOffset, int startingShields, float projectileContactRadius,
                float projectileSpeed, ShotCruiseConfig cruiseConfig, ShotBoardDynamics dynamics)
            {
                Board = board;
                WallLimitsClockwise = wallLimitsClockwise;
                ThrowerPivot = throwerPivot;
                SpawnLocalOffset = spawnLocalOffset;
                StartingShields = startingShields;
                ProjectileContactRadius = projectileContactRadius;
                ProjectileSpeed = projectileSpeed;
                CruiseConfig = cruiseConfig;
                Dynamics = dynamics;
            }
        }

        private readonly struct ShotSolverWindowEntry
        {
            public readonly float FromDegrees;
            public readonly float ToDegrees;
            public readonly float WidthDegrees;
            public readonly int CenterScore;
            public readonly int CenterPops;
            public readonly int CenterToughsCleared;

            public ShotSolverWindowEntry(
                float fromDegrees, float toDegrees, float widthDegrees, int centerScore, int centerPops,
                int centerToughsCleared)
            {
                FromDegrees = fromDegrees;
                ToDegrees = toDegrees;
                WidthDegrees = widthDegrees;
                CenterScore = centerScore;
                CenterPops = centerPops;
                CenterToughsCleared = centerToughsCleared;
            }
        }
    }
}

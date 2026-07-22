using System.Collections.Generic;
using System.Diagnostics;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.EditorUI.Charts;
using BalloonParty.Game;
using BalloonParty.Nudge;
using BalloonParty.Projectile;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
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

        private static readonly Color ActualPathColor = new(1f, 0.8f, 0.1f);

        private readonly List<ShotSolverWindowEntry> _windows = new();
        private readonly List<Vector2> _bestWindowPath = new();
        private readonly List<float> _bestWindowTimes = new();
        private readonly List<Vector2> _predictedPath = new();
        private readonly List<float> _predictedTimes = new();
        private readonly List<Vector3> _actualSamples = new();

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
        private string _targetColorId = string.Empty;
        private bool _checkRobustness;
        private string _divergenceSummary = string.Empty;
        private bool _divergenceTracking;
        private float _fireStartTime;
        private float _lastSampleTime;
        private float _maxDivergence;
        private float _maxDivergenceTime;
        private ProjectilePositionProvider _positionProvider;
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
            EditorApplication.update += TickDivergence;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= TickDivergence;
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
            if (!string.IsNullOrEmpty(_divergenceSummary))
            {
                EditorGUILayout.LabelField(_divergenceSummary, EditorStyles.wordWrappedMiniLabel);
            }
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
            _targetColorId = EditorGUILayout.TextField(
                new GUIContent("Target Colour", "Empty = all colours. When set, only pops of this colour id " +
                                                "count toward the target score (milestone-mask style); streaks " +
                                                "and refunds still run unfiltered."),
                _targetColorId);
            _checkRobustness = EditorGUILayout.ToggleLeft(
                new GUIContent("±Nudge robustness", "Re-simulates each window's centre with every contact " +
                                                    "circle fattened AND thinned by the nudge amplitude — a " +
                                                    "window that survives both still qualifies with balloons " +
                                                    "wobbled toward or away from the ray."),
                _checkRobustness, GUILayout.Width(150));
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

            var targetScore = (float)_targetScore;
            var options = new BarChartOptions
            {
                BarPadding = 0f,
                MinBarWidth = 1f,
                BarColorResolver = (_, score) => score >= targetScore ? QualifyingColor : NonQualifyingColor,
                Threshold = new ThresholdLine { Value = targetScore, Color = TargetLineColor },
            };
            BarChart.Draw(rect, _sweepScores, maxScore, options);
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
                var robustness = !_checkRobustness ? string.Empty : window.Robust ? "   ✓robust" : "   ±fragile";
                EditorGUILayout.LabelField(
                    $"{window.FromDegrees:F2}°  →  {window.ToDegrees:F2}°   width {window.WidthDegrees:F2}°   " +
                    $"score {window.CenterScore}   pops {window.CenterPops}   toughs {window.CenterToughsCleared}" +
                    $"{robustness}{marker}");
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

            if (_actualSamples.Count >= 2)
            {
                SceneDrawingHelper.DrawWorldPolyline(_actualSamples, ActualPathColor, PathThicknessPixels);
            }
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
                SimulateAt(_bestAngleDegrees, context, workingSet, _bestWindowPath, _bestWindowTimes);
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

        private void BuildWindows(IReadOnlyList<bool> qualifies, in ShotSolveContext context, ShotBalloonState[] workingSet)
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
                SimulateAt(centerDegrees, context, workingSet, _bestWindowPath, _bestWindowTimes);
            }
        }

        private void TryAddWindow(
            float fromDegrees, float toDegrees, in ShotSolveContext context, ShotBalloonState[] workingSet, ref float bestWidth)
        {
            var widthDegrees = toDegrees - fromDegrees;
            if (widthDegrees < _minWindowWidthDegrees)
            {
                return;
            }

            var centerDegrees = (fromDegrees + toDegrees) * 0.5f;
            var centerResult = SimulateAt(centerDegrees, context, workingSet);

            // Robust = the centre shot still reaches the target with every contact circle fattened
            // AND thinned by the nudge amplitude — a positional-uncertainty band for wobbling balloons.
            var robust = false;
            if (_checkRobustness && context.NudgeAmplitude > 0f)
            {
                robust =
                    SimulateAt(centerDegrees, context, workingSet, radiusBias: context.NudgeAmplitude)
                        .RawScore >= _targetScore
                    && SimulateAt(centerDegrees, context, workingSet, radiusBias: -context.NudgeAmplitude)
                        .RawScore >= _targetScore;
            }

            _windows.Add(new ShotSolverWindowEntry(
                fromDegrees, toDegrees, widthDegrees, centerResult.RawScore, centerResult.Pops,
                centerResult.ToughsCleared, robust));

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
            float outsideDegrees, float insideDegrees, in ShotSolveContext context, ShotBalloonState[] workingSet)
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

        private ShotSimulationResult SimulateAt(
            float angleDegrees, in ShotSolveContext context, ShotBalloonState[] workingSet, List<Vector2> pathOut = null,
            List<float> timesOut = null, float radiusBias = 0f)
        {
            return ShotBoardGather.SimulateAt(
                angleDegrees, context, workingSet, pathOut, timesOut, radiusBias,
                string.IsNullOrEmpty(_targetColorId) ? null : _targetColorId);
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

            scope.Container.Resolve<ThrowerController>().FireAt(ShotBoardGather.DirectionFromDegrees(_bestAngleDegrees));
            BeginDivergenceTracking(scope);
        }

        // Freezes the just-swept prediction and samples the real shot against it every editor update —
        // the plan's §7 4d divergence readout: where (and when) reality leaves the predicted path.
        private void BeginDivergenceTracking(ThrowerLifetimeScope scope)
        {
            if (_bestWindowPath.Count < 2 || _bestWindowTimes.Count != _bestWindowPath.Count)
            {
                return;
            }

            _predictedPath.Clear();
            _predictedPath.AddRange(_bestWindowPath);
            _predictedTimes.Clear();
            _predictedTimes.AddRange(_bestWindowTimes);
            _actualSamples.Clear();
            _positionProvider = scope.Container.Resolve<ProjectilePositionProvider>();
            _fireStartTime = Time.time;
            _lastSampleTime = float.NegativeInfinity;
            _maxDivergence = 0f;
            _maxDivergenceTime = 0f;
            _divergenceSummary = "Fire Best: tracking divergence…";
            _divergenceTracking = true;
        }

        private void TickDivergence()
        {
            if (!_divergenceTracking || !Application.isPlaying)
            {
                return;
            }

            var flightTime = Time.time - _fireStartTime;
            if (_positionProvider == null || !_positionProvider.IsActive)
            {
                // Grace window for the shot's first free physics frame; afterwards, gone = flight over.
                if (flightTime > 0.5f)
                {
                    FinishDivergenceTracking(flightTime);
                }

                return;
            }

            if (flightTime - _lastSampleTime < 0.02f)
            {
                return;
            }

            _lastSampleTime = flightTime;
            var actual = (Vector2)_positionProvider.Position;
            var predicted = PredictedPositionAt(flightTime);
            var divergence = Vector2.Distance(actual, predicted);
            if (divergence > _maxDivergence)
            {
                _maxDivergence = divergence;
                _maxDivergenceTime = flightTime;
            }

            if (_actualSamples.Count < 4096)
            {
                _actualSamples.Add(actual);
            }

            _divergenceSummary =
                $"Fire Best t={flightTime:F2}s — divergence now {divergence:F3} wu, " +
                $"max {_maxDivergence:F3} wu @ {_maxDivergenceTime:F2}s";
            Repaint();

            if (flightTime > _predictedTimes[^1] + 1f)
            {
                FinishDivergenceTracking(flightTime);
            }
        }

        private void FinishDivergenceTracking(float flightTime)
        {
            _divergenceTracking = false;
            _divergenceSummary = _actualSamples.Count == 0
                ? "Fire Best: no shot observed (blocked or paused?)"
                : $"Fire Best done ({flightTime:F2}s tracked) — max divergence {_maxDivergence:F3} wu " +
                  $"@ {_maxDivergenceTime:F2}s over {_actualSamples.Count} samples (actual path drawn in yellow)";
            Repaint();
            SceneView.RepaintAll();
        }

        // Time-interpolated position along the frozen prediction — between events flight is linear in
        // time per segment (the tap-lag fold makes freeze windows read as slower constant motion).
        private Vector2 PredictedPositionAt(float flightTime)
        {
            if (flightTime <= _predictedTimes[0])
            {
                return _predictedPath[0];
            }

            for (var i = 1; i < _predictedTimes.Count; i++)
            {
                if (flightTime <= _predictedTimes[i])
                {
                    var span = _predictedTimes[i] - _predictedTimes[i - 1];
                    var t = span <= 0f ? 1f : (flightTime - _predictedTimes[i - 1]) / span;
                    return Vector2.Lerp(_predictedPath[i - 1], _predictedPath[i], t);
                }
            }

            return _predictedPath[^1];
        }

        private float SampleAngle(int index)
        {
            var t = _sampleCount <= 1 ? 0f : (float)index / (_sampleCount - 1);
            return Mathf.Lerp(_arcMinDegrees, _arcMaxDegrees, t);
        }

        private static bool TryGatherLiveContext(out ShotSolveContext context)
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

            // ~0.5 frame of interval-crossing quantization + 1 frame of deferred-Balance yield, at
            // whatever rate the editor is actually rendering right now.
            var pulseDelay = Mathf.Clamp(1.5f * Time.smoothDeltaTime, 0f, 0.1f);
            context = ShotBoardGather.Gather(
                scope.Container.Resolve<SlotGrid>(),
                scope.Container.Resolve<IGameConfiguration>(),
                scope.Container.Resolve<IBalloonsConfiguration>(),
                thrower,
                scope.Container.Resolve<ThrowerSettings>(),
                pulseDelay);
            return true;
        }

        private readonly struct ShotSolverWindowEntry
        {
            public readonly float FromDegrees;
            public readonly float ToDegrees;
            public readonly float WidthDegrees;
            public readonly int CenterScore;
            public readonly int CenterPops;
            public readonly int CenterToughsCleared;
            public readonly bool Robust;

            public ShotSolverWindowEntry(
                float fromDegrees, float toDegrees, float widthDegrees, int centerScore, int centerPops,
                int centerToughsCleared, bool robust)
            {
                FromDegrees = fromDegrees;
                ToDegrees = toDegrees;
                WidthDegrees = widthDegrees;
                CenterScore = centerScore;
                CenterPops = centerPops;
                CenterToughsCleared = centerToughsCleared;
                Robust = robust;
            }
        }
    }
}

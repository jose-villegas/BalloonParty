using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Immutable parameters for one BigScore group. Owns one registered anchor flight (the cinematic
    ///     principal) and fans out to N formations via <see cref="ShapeFormationTicker.LaunchFormation"/>.
    /// </summary>
    internal readonly struct BigScoreGroupRequest
    {
        internal readonly Vector3 AnchorPosition;
        internal readonly TrailId CarrierId;
        internal readonly Color Color;
        internal readonly ITrailEndpoint Target;
        internal readonly TrailSpawner Spawner;
        internal readonly TrailFlightRegistry<TrailId> Flights;
        internal readonly IScoreTrailReporter Reporter;
        internal readonly BigScoreFormationSettings Settings;
        internal readonly Vector3 SpinAxis;
        internal readonly bool HasSpinAxis;
        internal readonly CancellationToken CancellationToken;

        internal BigScoreGroupRequest(
            Vector3 anchorPosition,
            TrailId carrierId,
            Color color,
            ITrailEndpoint target,
            TrailSpawner spawner,
            TrailFlightRegistry<TrailId> flights,
            IScoreTrailReporter reporter,
            BigScoreFormationSettings settings,
            Vector3 spinAxis,
            bool hasSpinAxis,
            CancellationToken cancellationToken)
        {
            AnchorPosition = anchorPosition;
            CarrierId = carrierId;
            Color = color;
            Target = target;
            Spawner = spawner;
            Flights = flights;
            Reporter = reporter;
            Settings = settings;
            SpinAxis = spinAxis;
            HasSpinAxis = hasSpinAxis;
            CancellationToken = cancellationToken;
        }
    }

    /// <summary>One shape inside a group: its catalog geometry, score value + range, sub-centre, fitted radius.</summary>
    internal readonly struct BigScoreFormationRequest
    {
        internal readonly FormationShape Shape;
        internal readonly int Value;
        internal readonly int RangeLast;
        internal readonly Vector3 Origin;
        internal readonly float FormationRadius;
        internal readonly bool IsPrincipal;
        internal readonly Quaternion InitialRotation;

        // Per-formation spin-axis override (the hit-aligned line precesses about the FLIGHT axis to
        // sweep a bicone); unset falls back to the group's shared roll axis.
        internal readonly Vector3 SpinAxis;
        internal readonly bool HasSpinAxis;

        internal BigScoreFormationRequest(
            FormationShape shape, int value, int rangeLast, Vector3 origin, float formationRadius,
            bool isPrincipal, Quaternion initialRotation, Vector3 spinAxis = default, bool hasSpinAxis = false)
        {
            Shape = shape;
            Value = value;
            RangeLast = rangeLast;
            Origin = origin;
            FormationRadius = formationRadius;
            IsPrincipal = isPrincipal;
            InitialRotation = initialRotation;
            SpinAxis = spinAxis;
            HasSpinAxis = hasSpinAxis;
        }
    }

    /// <summary>
    ///     Pooled, zero-allocation per-frame driver for every in-flight shape formation. Design constraints:
    ///
    ///     • States, groups and anchors are pooled via swap-remove (mirrors <c>BalloonMotionTicker</c>) —
    ///       a running formation must never allocate; adding fields to <c>FormationState</c> that reference
    ///       managed heap objects will defeat this.
    ///
    ///     • <c>liveTarget</c> re-reads the endpoint centre every tick so a drifting UI bar never leaves
    ///       the landing position stale — do not cache the target position at launch.
    ///
    ///     • Transport bridge: every formation in a group shares a single <see cref="TrailFlight"/> anchor.
    ///       The flight stays registered (InFlight) through the group's whole life and is unregistered only
    ///       once the last formation finishes — if a principal that lands first unregistered eagerly, the
    ///       cinematic would see Idle and snap the remaining shapes mid-travel. Paused inflates ribbon time
    ///       so the drawn figure survives the freeze; Idle triggers the SnapFade.
    /// </summary>
    internal sealed class ShapeFormationTicker : ILateTickable
    {
        private const int MaxVertexCount = 100;
        private const float SnapFadeDuration = 0.1f;
        private const float MinDuration = 0.0001f;
        private const float FreezeRibbonTime = 600f;

        // How fast the tumble's angular velocity eases toward its target (per second). Ramping it in keeps a
        // high spin speed from snapping the ribbons with a big rotation delta on the first ticks.
        private const float SpinRampRate = 2f;

        private readonly List<FormationState> _states = new(16);
        private readonly Stack<FormationState> _pool = new(16);
        private readonly Stack<Transform> _anchorPool = new(8);
        private readonly Stack<FormationGroup> _groupPool = new(8);

        public void LateTick()
        {
            var dt = Time.deltaTime;
            var unscaledDt = Time.unscaledDeltaTime;

            for (var i = _states.Count - 1; i >= 0; i--)
            {
                var state = _states[i];
                var group = state.Group;

                // Run reset recreated the group CTS — discard WITHOUT reporting (the reset clears the run's score).
                if (group.CancellationToken.IsCancellationRequested)
                {
                    ReleaseVertices(state);
                    ReleaseStateAt(i);
                    DecrementGroup(group);
                    continue;
                }

                // Post-snap: run the unscaled vertex fade-out to completion, then drop the state.
                if (state.Phase == FormationPhase.SnapFade)
                {
                    if (AdvanceSnapFade(state, unscaledDt))
                    {
                        ReleaseStateAt(i);
                        DecrementGroup(group);
                    }

                    continue;
                }

                var flightPhase = group.Flight.Phase;

                // Cinematic froze the principal — freeze the whole formation and inflate the ribbons so the drawn
                // figure doesn't decay while the cinematic owns the anchor transform.
                if (flightPhase == FlightPhase.Paused)
                {
                    FreezeRibbons(state);
                    continue;
                }

                // Unpaused — restore the computed coverage times before advancing.
                ThawRibbons(state);

                // Cinematic pan-in / CompleteAll drove the principal to Idle — snap this formation now.
                if (flightPhase == FlightPhase.Idle)
                {
                    Snap(state, i, group);
                    continue;
                }

                if (AdvanceFormation(state, dt))
                {
                    ReleaseStateAt(i);
                    DecrementGroup(group);
                }
            }
        }

        // Synchronous so the anchor registers before the caller's Begin returns (the cinematic's registry wait
        // depends on it). Acquires a pooled group + its anchor and registers the principal flight.
        internal FormationGroup BeginGroup(in BigScoreGroupRequest request)
        {
            var group = _groupPool.Count > 0 ? _groupPool.Pop() : new FormationGroup();
            var anchor = AcquireAnchor(request.AnchorPosition);
            group.Initialize(in request, anchor, request.Flights.Register(request.CarrierId, anchor, request.AnchorPosition));
            return group;
        }

        // Acquires the n orbiting pens and seeds a pooled formation state under its group. Pens are distributed
        // across the shape's walks (PensPerWalk), evenly phase-offset along each, and each gets a per-walk ribbon
        // coverage time so k pens tile a period-P walk (computed at scale 1 — conservative as the shape shrinks).
        internal void LaunchFormation(FormationGroup group, in BigScoreFormationRequest request)
        {
            var state = _pool.Count > 0 ? _pool.Pop() : new FormationState();
            state.Initialize(group, in request);

            var shape = request.Shape;
            var penSpeed = Mathf.Max(group.Settings.PenSpeed * shape.PenSpeedScale, MinDuration);
            var coverage = group.Settings.Coverage;
            state.LocalPenSpeed = penSpeed / Mathf.Max(request.FormationRadius, MinDuration);
            var pen = 0;
            for (var w = 0; w < shape.Walks.Length; w++)
            {
                var perimeter = shape.Perimeters[w];
                var k = shape.PensPerWalk[w];
                if (k <= 0)
                {
                    continue;
                }

                // Coverage = a k-pen share of one lap, scaled by the style dial. Lap period is the WORLD perimeter
                // (at scale 1) over the pen speed, so ink density reads the same across shape sizes.
                var lapPeriod = request.FormationRadius * perimeter / penSpeed;
                var ribbonTime = lapPeriod / k * coverage;
                for (var j = 0; j < k; j++)
                {
                    state.PenWalk[pen] = w;
                    state.PenStartDist[pen] = (float)j / k * perimeter;
                    state.PenRibbonTime[pen] = ribbonTime;
                    state.PenSegment[pen] = 0;

                    var vertex = group.Spawner.Acquire(group.Color);
                    vertex.SetRibbonTime(ribbonTime);
                    vertex.transform.position = request.Origin;
                    vertex.transform.localScale = Vector3.one;
                    vertex.ClearRibbon();
                    // Pen down from t = 0: the shape blooms from a point (scale 0), so there are no deploy spokes.
                    vertex.SetRibbonEmitting(true);
                    state.Vertices[pen] = vertex;
                    pen++;
                }
            }

            state.VertexCount = pen;
            state.VerticesLive = true;

            // A guiding central trail rides the formation centre, drawing the real travel path to the
            // bar underneath the shape — a comet line anchoring each constellation member to its bar.
            // Its ribbon is deliberately NOT re-framed (its path is genuine motion, not shape geometry).
            state.Guide = group.Spawner.Acquire(group.Color);
            state.Guide.transform.position = state.Center;
            state.Guide.transform.localScale = Vector3.one;
            state.Guide.ClearRibbon();

            // Sample the random landing offset ONCE; the live target re-reads the endpoint centre every tick.
            if (group.Target != null)
            {
                var offset = group.Target.RandomPosition() - group.Target.Center;
                offset.z = 0f;
                state.TargetOffset = offset;
            }

            _states.Add(state);
            group.Remaining++;
        }

        // Returns true when the formation has finished its travel and the state should be released.
        private bool AdvanceFormation(FormationState state, float dt)
        {
            var group = state.Group;
            var settings = group.Settings;
            var oldCenter = state.Center;
            var oldRotation = state.Rotation;

            state.Elapsed += dt;
            var t = state.Elapsed;

            // Centre travels origin -> live target (re-read each tick, so a moving bar is still hit exactly).
            var liveTarget = group.Target != null ? group.Target.Center + state.TargetOffset : state.Origin;
            var travelT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / state.Duration));
            var newCenter = Vector3.Lerp(state.Origin, liveTarget, travelT);

            // Tumble spins from t = 0 (invisible while the shape is a point). Ease the angular VELOCITY toward
            // its target and integrate the angle, so a high spin speed ramps in instead of snapping the ribbons
            // with a large rotation delta on the first ticks.
            var targetSpin = settings.SpinSpeedDegrees * state.Shape.SpinScale;
            state.SpinSpeed = Mathf.Lerp(state.SpinSpeed, targetSpin, 1f - Mathf.Exp(-SpinRampRate * dt));
            state.SpinAngle += state.SpinSpeed * dt;
            var newRotation = Quaternion.AngleAxis(state.SpinAngle, state.SpinAxis) * state.InitialRotation;
            var delta = newRotation * Quaternion.Inverse(oldRotation);

            var scale = settings.ScaleOverTravel != null ? settings.ScaleOverTravel.Evaluate(t) : 0f;

            // Re-frame the drawn ink by the same translate+tumble+SCALE the live frame moved through, so the
            // drawn figure shrinks with the shape as it approaches the bar. Scale correction matters most on
            // the long-ribbon shapes (the 12's one-pen-per-pentagram walks): un-scaled old ink outlives the
            // taper and holds the big silhouette. The ratio guards the curve's taper through zero — once the
            // old scale is ~0 every recorded point already sits at the centre, so 0 simply keeps it there.
            var scaleRatio = state.LastScale > 1e-4f ? scale / state.LastScale : (scale > 1e-4f ? 1f : 0f);
            var reframeRibbons = state.VerticesLive
                && (newCenter != oldCenter || delta != Quaternion.identity || !Mathf.Approximately(scaleRatio, 1f));

            state.Center = newCenter;
            state.Rotation = newRotation;
            state.LastScale = scale;

            if (state.Guide != null)
            {
                state.Guide.transform.position = newCenter;
            }

            // Reframe each pen's ribbon before writing its new head position — same per-pen order as before,
            // just one pass over the pens instead of two.
            for (var p = 0; p < state.VertexCount; p++)
            {
                if (reframeRibbons)
                {
                    state.Vertices[p].TransformRibbon(oldCenter, newCenter, delta, scaleRatio);
                }

                var local = PenOrbitLocal(state, p);
                if (state.Shape.Displacer != null)
                {
                    local = state.Shape.Displacer(local, state.Elapsed,
                        state.Group.Settings.DisplacementScale * state.Shape.DisplacementScale,
                        state.Group.Settings.DisplacementSpeed * state.Shape.DisplacementSpeed);
                }

                state.Vertices[p].transform.position = LocalToWorld(state, local * scale);
            }

            WriteAnchor(state);

            if (t < state.Duration)
            {
                return false;
            }

            ReportOnce(state, liveTarget);
            ReleaseVertices(state);
            return true;
        }

        // True once the fade completes and the vertices have been released.
        private bool AdvanceSnapFade(FormationState state, float unscaledDt)
        {
            state.FadeElapsed += unscaledDt;
            var fade = Mathf.Clamp01(state.FadeElapsed / SnapFadeDuration);
            ScaleVertices(state, 1f - fade);

            if (fade < 1f)
            {
                return false;
            }

            ReleaseVertices(state);
            return true;
        }

        // The cinematic drove the shared flight Idle — settle this formation's value at the anchor and fade.
        private void Snap(FormationState state, int index, FormationGroup group)
        {
            ReportOnce(state, group.Anchor != null ? group.Anchor.position : state.Center);

            if (state.VerticesLive)
            {
                state.Phase = FormationPhase.SnapFade;
                state.FadeElapsed = 0f;
                return;
            }

            ReleaseStateAt(index);
            DecrementGroup(group);
        }

        private void FreezeRibbons(FormationState state)
        {
            if (state.Frozen || !state.VerticesLive)
            {
                return;
            }

            for (var i = 0; i < state.VertexCount; i++)
            {
                state.Vertices[i].SetRibbonTime(FreezeRibbonTime);
            }

            state.Frozen = true;
        }

        private void ThawRibbons(FormationState state)
        {
            if (!state.Frozen || !state.VerticesLive)
            {
                return;
            }

            for (var i = 0; i < state.VertexCount; i++)
            {
                state.Vertices[i].SetRibbonTime(state.PenRibbonTime[i]);
            }

            state.Frozen = false;
        }

        private static void ReportOnce(FormationState state, Vector3 at)
        {
            if (state.Reported)
            {
                return;
            }

            state.Reported = true;
            state.Group.Reporter.ReportArrival(state.RangeLast, state.Value, at);
        }

        // Once the last formation of a group leaves, unregister the flight and release the anchor + group.
        private void DecrementGroup(FormationGroup group)
        {
            group.Remaining--;
            if (group.Remaining > 0 || group.CleanedUp)
            {
                return;
            }

            group.CleanedUp = true;
            group.Flights.Unregister(group.CarrierId);
            ReleaseAnchor(group);
            ReleaseGroup(group);
        }

        private void ReleaseVertices(FormationState state)
        {
            if (state.Guide != null)
            {
                state.Group.Spawner.Release(state.Guide);
                state.Guide = null;
            }

            if (!state.VerticesLive)
            {
                return;
            }

            for (var i = 0; i < state.VertexCount; i++)
            {
                if (state.Vertices[i] != null)
                {
                    state.Group.Spawner.Release(state.Vertices[i]);
                    state.Vertices[i] = null;
                }
            }

            state.VerticesLive = false;
        }

        private void ReleaseStateAt(int index)
        {
            var state = _states[index];
            _states[index] = _states[^1];
            _states.RemoveAt(_states.Count - 1);

            state.Group = null;
            state.Shape = null;
            _pool.Push(state);
        }

        // Bare, invisible Transform carrying the group's principal centre; the registry/cinematic need a Transform
        // to track/pause. Deactivated on release and pushed back, never destroyed.
        private Transform AcquireAnchor(Vector3 position)
        {
            var anchor = _anchorPool.Count > 0 ? _anchorPool.Pop() : new GameObject("ShapeFormationAnchor").transform;
            anchor.position = position;
            anchor.gameObject.SetActive(true);
            return anchor;
        }

        private void ReleaseAnchor(FormationGroup group)
        {
            if (group.Anchor == null)
            {
                return;
            }

            group.Anchor.gameObject.SetActive(false);
            _anchorPool.Push(group.Anchor);
            group.Anchor = null;
        }

        private void ReleaseGroup(FormationGroup group)
        {
            group.Spawner = null;
            group.Flights = null;
            group.Reporter = null;
            group.Target = null;
            group.Flight = null;
            _groupPool.Push(group);
        }

        private static void WriteAnchor(FormationState state)
        {
            if (state.IsPrincipal && state.Group.Anchor != null)
            {
                state.Group.Anchor.position = state.Center;
            }
        }

        private static Vector3 LocalToWorld(FormationState state, Vector3 local)
        {
            return state.Center + state.Rotation * (state.FormationRadius * local);
        }

        // The pen's local position on its closed walk at the current orbit time, parameterized by LOCAL arc length
        // (so a world-units pen speed gives constant travel speed between vertices). Arc walks slerp their segments
        // (curved ring bands), chords lerp them.
        private static Vector3 PenOrbitLocal(FormationState state, int p)
        {
            var w = state.PenWalk[p];
            var walk = state.Shape.Walks[w];
            var m = walk.Vertices.Length;
            var perimeter = state.Shape.Perimeters[w];
            if (perimeter <= Mathf.Epsilon)
            {
                return state.Shape.Vertices[walk.Vertices[0]];
            }

            var cumulative = state.Shape.Cumulative[w];
            var d = Mathf.Repeat(state.PenStartDist[p] + state.Elapsed * state.LocalPenSpeed, perimeter);

            // Pens advance monotonically within a lap, so the segment index only moves forward; resume the
            // scan from where the pen last was instead of rescanning from 0 every tick. Only a lap wrap
            // (d falls behind the cached segment's start) forces a rescan from the top.
            var seg = state.PenSegment[p];
            if (d < cumulative[seg])
            {
                seg = 0;
            }

            while (seg < m - 1 && cumulative[seg + 1] <= d)
            {
                seg++;
            }

            state.PenSegment[p] = seg;

            var segLength = cumulative[seg + 1] - cumulative[seg];
            var localT = segLength > Mathf.Epsilon ? (d - cumulative[seg]) / segLength : 0f;
            var a = state.Shape.Vertices[walk.Vertices[seg]];
            var b = state.Shape.Vertices[walk.Vertices[(seg + 1) % m]];
            return walk.Arc ? NormalizedLerp(a, b, localT) : Vector3.LerpUnclamped(a, b, localT);
        }

        // Fast great-circle interpolation: Lerp + normalize replaces Slerp's trig (acos + 2 sin)
        // with a single normalize (rsqrt). On unit-radius vertices this traces the same arc with
        // a mild speed nonlinearity at wide angles — imperceptible at gameplay pen speeds.
        private static Vector3 NormalizedLerp(Vector3 a, Vector3 b, float t)
        {
            var v = Vector3.LerpUnclamped(a, b, t);
            var mag = v.magnitude;
            return mag > 1e-6f ? v * (a.magnitude / mag) : a;
        }

        private static void ScaleVertices(FormationState state, float scale)
        {
            var value = Vector3.one * scale;
            for (var i = 0; i < state.VertexCount; i++)
            {
                if (state.Vertices[i] != null)
                {
                    state.Vertices[i].transform.localScale = value;
                }
            }
        }

        private enum FormationPhase
        {
            Travel,
            SnapFade
        }

        // One per BigScore group. Owns the single registered anchor flight the cinematic tracks and the shared
        // per-pop seams; every formation in the group reads its Flight for the transport bridge.
        internal sealed class FormationGroup
        {
            internal Transform Anchor;
            internal TrailFlight Flight;
            internal TrailSpawner Spawner;
            internal TrailFlightRegistry<TrailId> Flights;
            internal IScoreTrailReporter Reporter;
            internal ITrailEndpoint Target;
            internal BigScoreFormationSettings Settings;
            internal TrailId CarrierId;
            internal Color Color;
            internal Vector3 SpinAxis;
            internal bool HasSpinAxis;
            internal CancellationToken CancellationToken;
            internal int Remaining;
            internal bool CleanedUp;

            internal void Initialize(in BigScoreGroupRequest request, Transform anchor, TrailFlight flight)
            {
                Anchor = anchor;
                Flight = flight;
                Spawner = request.Spawner;
                Flights = request.Flights;
                Reporter = request.Reporter;
                Target = request.Target;
                Settings = request.Settings;
                CarrierId = request.CarrierId;
                Color = request.Color;
                SpinAxis = request.SpinAxis;
                HasSpinAxis = request.HasSpinAxis;
                CancellationToken = request.CancellationToken;
                Remaining = 0;
                CleanedUp = false;
            }
        }

        private sealed class FormationState
        {
            internal readonly FlyingTrail[] Vertices = new FlyingTrail[MaxVertexCount];
            internal readonly int[] PenWalk = new int[MaxVertexCount];
            internal readonly float[] PenStartDist = new float[MaxVertexCount];
            internal readonly float[] PenRibbonTime = new float[MaxVertexCount];
            internal readonly int[] PenSegment = new int[MaxVertexCount];

            internal FormationGroup Group;
            internal FormationShape Shape;
            internal Vector3 Origin;
            internal Vector3 Center;
            internal Vector3 TargetOffset;
            internal Quaternion Rotation;
            internal Quaternion InitialRotation;
            internal float LastScale;
            internal Vector3 SpinAxis;
            internal float SpinSpeed;
            internal float SpinAngle;
            internal float FormationRadius;
            internal float LocalPenSpeed;
            internal float Duration;
            internal float Elapsed;
            internal float FadeElapsed;
            internal FormationPhase Phase;
            internal int Value;
            internal int RangeLast;
            internal int VertexCount;
            internal bool IsPrincipal;
            internal bool VerticesLive;
            internal FlyingTrail Guide;
            internal bool Reported;
            internal bool Frozen;

            internal void Initialize(FormationGroup group, in BigScoreFormationRequest request)
            {
                Group = group;
                Shape = request.Shape;
                Value = request.Value;
                RangeLast = request.RangeLast;
                IsPrincipal = request.IsPrincipal;
                FormationRadius = request.FormationRadius;
                Origin = request.Origin;
                Center = request.Origin;
                TargetOffset = Vector3.zero;
                InitialRotation = request.InitialRotation;
                Rotation = InitialRotation;
                LastScale = 0f;
                // All formations of a pop roll about the shared hit-derived axis (a coherent constellation
                // tumble) unless the shape overrides it (the hit-aligned line precesses about the flight
                // axis, sweeping a bicone); a directionless pop falls back to a per-shape random axis.
                SpinAxis = request.HasSpinAxis
                    ? request.SpinAxis
                    : group.HasSpinAxis ? group.SpinAxis : Random.onUnitSphere;
                SpinSpeed = 0f;
                SpinAngle = 0f;
                Duration = Mathf.Max(group.Settings.ScaleOverTravel.Duration(), MinDuration);
                Elapsed = 0f;
                FadeElapsed = 0f;
                Phase = FormationPhase.Travel;
                VertexCount = 0;
                VerticesLive = false;
                Guide = null;
                Reported = false;
                Frozen = false;
            }
        }
    }
}

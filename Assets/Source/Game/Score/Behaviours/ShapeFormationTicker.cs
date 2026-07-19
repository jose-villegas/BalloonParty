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
    ///     Shared, once-per-pop data for one BigScore group: the pooled trail channel, palette colour, endpoint,
    ///     registry/reporter seams, the principal's carrier id and anchor placement, and the global formation
    ///     settings. A group holds one registered anchor flight (the principal the cinematic tracks) and fans out
    ///     to N formations launched via <see cref="ShapeFormationTicker.LaunchFormation"/>.
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
    ///     The analytic driver behind <see cref="BigScoreTrailBehaviour"/>. A group's score decomposes into shapes
    ///     (<see cref="ShapeCatalog"/>); each shape is one formation of n pens whose ribbons are the ink. A
    ///     formation is pure math evaluated once per <see cref="ILateTickable.LateTick"/> — states, groups and
    ///     anchors are pooled, so a running formation never allocates. Mirrors <c>BalloonMotionTicker</c>'s
    ///     pooled-state + swap-remove idioms.
    ///
    ///     One life, one Travel phase (plus a SnapFade for cinematic interrupts): with the shape's scale driven by
    ///     the settings' curve (its last key time is the duration), the world position of a pen is
    ///     <c>C(t) + Q(t) · (radius · scale(t) · localₚ(t))</c> where
    ///     <list type="bullet">
    ///       <item><c>C(t) = Lerp(origin, liveTarget, SmoothStep(t/D))</c> — the shape blooms at its sub-centre and
    ///             travels to the bar; <c>liveTarget</c> re-reads the endpoint centre every tick (plus a
    ///             launch-sampled offset), so a drifting UI bar can never leave the landing stale.</item>
    ///       <item><c>Q(t)</c> — a fixed random tilt spun about a random axis from t = 0 (invisible while the
    ///             shape is still a point).</item>
    ///       <item><c>scale(t)</c> — the settings curve: 0 → bloom → hold → 0, so the shape grows from a point and
    ///             tapers back to one at the bar. Pens are PEN-DOWN from t = 0, so no deploy spokes exist.</item>
    ///       <item><c>localₚ(t)</c> — the pen's position on its closed walk, orbiting continuously; the first lap
    ///             draws the wireframe, later laps re-ink it, k pens tile a period-P walk in P/k.</item>
    ///     </list>
    ///
    ///     Transport bridge — the group's anchor <see cref="TrailFlight"/> handle is the pause/snap/slow-mo
    ///     interface, polled every tick; every formation in the group shares it, so a cinematic pause or completion
    ///     fans out to the whole group. Paused freezes the formation (and inflates the ribbon time so the drawn
    ///     figure survives the cinematic freeze); Idle snaps (report the value now, fade the pens out unscaled);
    ///     Speed scales the formation clock. The flight stays registered (InFlight) through the group's whole life
    ///     and is unregistered only once the last formation finishes, so a principal that lands first never falsely
    ///     signals Idle to the others.
    /// </summary>
    internal sealed class ShapeFormationTicker : ILateTickable
    {
        private const int MaxVertexCount = 50;
        private const float SnapFadeDuration = 0.1f;
        private const float MinDuration = 0.0001f;
        private const float FreezeRibbonTime = 600f;

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

                if (AdvanceFormation(state, dt * group.Flight.Speed))
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
            var penSpeed = Mathf.Max(group.Settings.PenSpeed, MinDuration);
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

            // Tumble spins from t = 0 (invisible while the shape is a point).
            var newRotation = Quaternion.AngleAxis(settings.SpinSpeedDegrees * t, state.SpinAxis) * state.InitialRotation;
            var delta = newRotation * Quaternion.Inverse(oldRotation);

            var scale = settings.ScaleOverTravel != null ? settings.ScaleOverTravel.Evaluate(t) : 0f;

            // Re-frame the drawn ink by the same translate+tumble+SCALE the live frame moved through, so the
            // drawn figure shrinks with the shape as it approaches the bar. Scale correction matters most on
            // the long-ribbon shapes (the 12's one-pen-per-pentagram walks): un-scaled old ink outlives the
            // taper and holds the big silhouette. The ratio guards the curve's taper through zero — once the
            // old scale is ~0 every recorded point already sits at the centre, so 0 simply keeps it there.
            var scaleRatio = state.LastScale > 1e-4f ? scale / state.LastScale : (scale > 1e-4f ? 1f : 0f);
            if (state.VerticesLive
                && (newCenter != oldCenter || delta != Quaternion.identity || !Mathf.Approximately(scaleRatio, 1f)))
            {
                for (var i = 0; i < state.VertexCount; i++)
                {
                    state.Vertices[i].TransformRibbon(oldCenter, newCenter, delta, scaleRatio);
                }
            }

            state.Center = newCenter;
            state.Rotation = newRotation;
            state.LastScale = scale;
            for (var p = 0; p < state.VertexCount; p++)
            {
                state.Vertices[p].transform.position = LocalToWorld(state, PenOrbitLocal(state, p) * scale);
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
            var seg = 0;
            while (seg < m - 1 && cumulative[seg + 1] <= d)
            {
                seg++;
            }

            var segLength = cumulative[seg + 1] - cumulative[seg];
            var localT = segLength > Mathf.Epsilon ? (d - cumulative[seg]) / segLength : 0f;
            var a = state.Shape.Vertices[walk.Vertices[seg]];
            var b = state.Shape.Vertices[walk.Vertices[(seg + 1) % m]];
            return walk.Arc ? Vector3.Slerp(a, b, localT) : Vector3.Lerp(a, b, localT);
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

            internal FormationGroup Group;
            internal FormationShape Shape;
            internal Vector3 Origin;
            internal Vector3 Center;
            internal Vector3 TargetOffset;
            internal Quaternion Rotation;
            internal Quaternion InitialRotation;
            internal float LastScale;
            internal Vector3 SpinAxis;
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
                Duration = Mathf.Max(group.Settings.ScaleOverTravel.Duration(), MinDuration);
                Elapsed = 0f;
                FadeElapsed = 0f;
                Phase = FormationPhase.Travel;
                VertexCount = 0;
                VerticesLive = false;
                Reported = false;
                Frozen = false;
            }
        }
    }
}

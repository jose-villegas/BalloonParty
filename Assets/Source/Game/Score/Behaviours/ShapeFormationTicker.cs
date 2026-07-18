using System;
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
    ///     Everything <see cref="ShapeFormationTicker.Launch"/> needs to drive one BigScore star formation:
    ///     the pooled trail channel, palette colour, endpoint, registry/reporter seams, and the tier geometry.
    /// </summary>
    internal readonly struct BigScoreFormationRequest
    {
        internal readonly Vector3 Center;
        internal readonly Vector3 DeployFrom;
        internal readonly float Radius;
        internal readonly Color Color;
        internal readonly int Points;
        internal readonly int LastScore;
        internal readonly TrailId CarrierId;
        internal readonly ITrailEndpoint Target;
        internal readonly TrailSpawner Spawner;
        internal readonly TrailFlightRegistry<TrailId> Flights;
        internal readonly IScoreTrailReporter Reporter;
        internal readonly BigScoreTierConfig Tier;
        internal readonly float CarrierFlightDuration;
        internal readonly CancellationToken CancellationToken;

        internal BigScoreFormationRequest(
            Vector3 center,
            Vector3 deployFrom,
            float radius,
            Color color,
            int points,
            int lastScore,
            TrailId carrierId,
            ITrailEndpoint target,
            TrailSpawner spawner,
            TrailFlightRegistry<TrailId> flights,
            IScoreTrailReporter reporter,
            BigScoreTierConfig tier,
            float carrierFlightDuration,
            CancellationToken cancellationToken)
        {
            Center = center;
            DeployFrom = deployFrom;
            Radius = radius;
            Color = color;
            Points = points;
            LastScore = lastScore;
            CarrierId = carrierId;
            Target = target;
            Spawner = spawner;
            Flights = flights;
            Reporter = reporter;
            Tier = tier;
            CarrierFlightDuration = carrierFlightDuration;
            CancellationToken = cancellationToken;
        }
    }

    /// <summary>
    ///     The analytic driver behind <see cref="BigScoreTrailBehaviour"/>. Each formation is pure math: n
    ///     vertices in a local frame that we apply a translate/rotate/scale to. The frame has a CENTER — the
    ///     spawn-axis origin, not a trail — carried by a bare pooled anchor Transform. The anchor's transform
    ///     is registered as the principal flight (the registry/cinematic contract needs a Transform to
    ///     track/pause/complete); nothing visible rides it. The n vertex trails read only the world positions
    ///     of the vertices and move between them, drawing the star polygon {n/k} (nested m times) with their
    ///     ribbons. All positions are closed-form, evaluated once per <see cref="ILateTickable.LateTick"/>;
    ///     state objects and anchors are pooled, so a running formation never allocates. Mirrors
    ///     <c>BalloonMotionTicker</c>'s pooled-state + swap-remove idioms.
    ///
    ///     World vertex: <c>C(t) + R(Ω(t)) · (r(t) · dir(φᵢ + repRotation))</c> — the path rotation Ω folds
    ///     into the angle. Ω is 0 until the first Draw completes, then advances at the tier's RotationSpeed
    ///     (formation clock) for the rest of the formation, including the final flight; the collapse is a pure
    ///     radial r→0 inside that rotating frame.
    ///
    ///     Transport bridge — the anchor's <see cref="TrailFlight"/> handle is the formation's pause/snap/
    ///     slow-mo interface, polled every tick:
    ///     <list type="bullet">
    ///       <item>Paused — the level-up cinematic froze the principal; the formation freezes in place and
    ///             the anchor is left for the cinematic to drag.</item>
    ///       <item>Idle — the cinematic pan-in or a <c>CompleteAll</c> (level-up/loss) drove the principal
    ///             home; snap: report the whole value now, fade the live vertices out (unscaled), release.</item>
    ///       <item>Speed — a slow-mo factor scaling the formation clock (matches the trail tweens).</item>
    ///     </list>
    /// </summary>
    internal sealed class ShapeFormationTicker : ILateTickable
    {
        private const int MaxVertexCount = 8;
        private const float SnapFadeDuration = 0.1f;
        private const float MinPhaseDuration = 0.0001f;
        private const float TwoPi = Mathf.PI * 2f;
        private const float InitialTheta = Mathf.PI * 0.5f;

        private readonly List<FormationState> _states = new(8);
        private readonly Stack<FormationState> _pool = new(8);
        private readonly Stack<Transform> _anchorPool = new(8);

        public void LateTick()
        {
            var dt = Time.deltaTime;
            var unscaledDt = Time.unscaledDeltaTime;

            for (var i = _states.Count - 1; i >= 0; i--)
            {
                var state = _states[i];

                // Run reset recreated the group CTS — discard the formation WITHOUT reporting (the reset
                // clears the run's score, so unreported points are moot).
                if (state.CancellationToken.IsCancellationRequested)
                {
                    HardRelease(state);
                    ReleaseStateAt(i);
                    continue;
                }

                // Post-snap: run the unscaled vertex fade-out to completion, then drop the state.
                if (state.Phase == FormationPhase.SnapFade)
                {
                    if (AdvanceSnapFade(state, unscaledDt))
                    {
                        ReleaseStateAt(i);
                    }

                    continue;
                }

                var flightPhase = state.Flight.Phase;

                // Cinematic froze the principal — freeze the whole formation and let the cinematic own the
                // anchor transform (vertex trails stay put where they were last written).
                if (flightPhase == FlightPhase.Paused)
                {
                    continue;
                }

                // Cinematic pan-in / CompleteAll drove the principal to Idle — the value settles now.
                if (flightPhase == FlightPhase.Idle)
                {
                    Settle(state, i);
                    continue;
                }

                if (AdvanceFormation(state, dt * state.Flight.Speed))
                {
                    ReleaseStateAt(i);
                }
            }
        }

        // Synchronous so the anchor registers before the caller's Begin returns (the cinematic's registry wait
        // depends on it). Acquires the anchor + n vertex trails and seeds a pooled formation state.
        internal void Launch(in BigScoreFormationRequest request)
        {
            var state = _pool.Count > 0 ? _pool.Pop() : new FormationState();
            state.Initialize(in request);

            var anchor = AcquireAnchor(request.Center);
            state.Anchor = anchor;
            state.Flight = request.Flights.Register(request.CarrierId, anchor, request.Center);

            // Sampled ONCE at launch: the drawing phases drift toward this point; the final flight re-samples
            // a fresh endpoint (this early sample can go stale — hitting below the moving UI bar).
            state.DriftTarget = request.Target != null ? request.Target.RandomPosition() : request.Center;
            state.TotalDuration = PhasedDuration(request.Tier);
            state.TotalElapsed = 0f;

            var count = request.Tier.VertexCount;
            for (var i = 0; i < count; i++)
            {
                var vertex = request.Spawner.Acquire(request.Color);
                vertex.SetRibbonTime(request.Tier.RibbonTime);
                vertex.transform.position = request.DeployFrom;
                vertex.transform.localScale = Vector3.one;
                vertex.ClearRibbon();
                // Pen up: deploy travels to the vertices without drawing, or the pop->vertex spokes
                // bury the star (the long nested-look ribbon time keeps them alive the whole sequence).
                vertex.SetRibbonEmitting(false);
                state.Vertices[i] = vertex;
            }

            state.VertexCount = count;
            state.VerticesLive = true;
            _states.Add(state);
        }

        // Returns true when the formation has finished its final flight and the state should be released.
        private bool AdvanceFormation(FormationState state, float dt)
        {
            var oldCenter = state.Center;
            var oldRotation = state.Rotation;

            // Centre motion: drift toward the bar while drawing, then the fresh-sampled final leg. Both are
            // smoothstepped so the handoff at the last collapse is continuous.
            Vector3 newCenter;
            if (state.Phase == FormationPhase.FinalFlight)
            {
                state.FinalElapsed += dt;
                var flightT = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(state.FinalElapsed / Mathf.Max(state.CarrierFlightDuration, MinPhaseDuration)));
                newCenter = Vector3.Lerp(state.FinalStartCenter, state.FinalTarget, flightT);
            }
            else if (state.TotalDuration > 0f)
            {
                state.TotalElapsed += dt;
                var driftT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(state.TotalElapsed / state.TotalDuration));
                var driftEnd = Vector3.Lerp(state.InitialCenter, state.DriftTarget, state.Tier.DriftToTarget);
                newCenter = Vector3.Lerp(state.InitialCenter, driftEnd, driftT);
            }
            else
            {
                newCenter = oldCenter;
            }

            // Path rotation Ω advances once the first star is drawn (formation clock, includes the final flight).
            var newRotation = oldRotation + (state.RotationActive ? state.Tier.RotationSpeedRadians * dt : 0f);
            var deltaRadians = newRotation - oldRotation;

            // Ribbons record WORLD positions, so re-frame the drawn history by the same translate+rotate the
            // live frame moved through, keeping the whole figure rigid in formation space.
            if (state.VerticesLive && (newCenter != oldCenter || deltaRadians != 0f))
            {
                for (var i = 0; i < state.VertexCount; i++)
                {
                    state.Vertices[i].TransformRibbon(oldCenter, newCenter, deltaRadians);
                }
            }

            state.Center = newCenter;
            state.Rotation = newRotation;

            switch (state.Phase)
            {
                case FormationPhase.Deploy:
                    AdvanceDeploy(state, dt);
                    break;
                case FormationPhase.Draw:
                    AdvanceDraw(state, dt);
                    break;
                case FormationPhase.Collapse:
                    AdvanceCollapse(state, dt);
                    break;
                case FormationPhase.FinalFlight:
                    return AdvanceFinalFlight(state);
            }

            WriteAnchor(state);
            return false;
        }

        private void AdvanceDeploy(FormationState state, float dt)
        {
            var scale = RepScale(state);
            var duration = Mathf.Max(state.Tier.DeployDuration * scale, MinPhaseDuration);
            state.PhaseElapsed += dt;
            var progress = Mathf.Clamp01(state.PhaseElapsed / duration);

            var radius = state.Radius * scale;
            var theta = RepTheta(state);
            for (var i = 0; i < state.VertexCount; i++)
            {
                var vertex = Vertex(state, i, radius, theta);
                state.Vertices[i].transform.position = Vector3.Lerp(state.DeployFrom, vertex, progress);
            }

            if (progress >= 1f)
            {
                // Pen down exactly at the vertices: the chords drawn from here ARE the star.
                for (var i = 0; i < state.VertexCount; i++)
                {
                    state.Vertices[i].SetRibbonEmitting(true);
                }

                state.Phase = FormationPhase.Draw;
                state.PhaseElapsed = 0f;
            }
        }

        private void AdvanceDraw(FormationState state, float dt)
        {
            var scale = RepScale(state);
            var duration = Mathf.Max(state.Tier.DrawDuration * scale, MinPhaseDuration);
            state.PhaseElapsed += dt;
            var progress = Mathf.Clamp01(state.PhaseElapsed / duration);

            var radius = state.Radius * scale;
            var theta = RepTheta(state);
            var n = state.VertexCount;
            var k = state.Tier.Skip;
            for (var i = 0; i < n; i++)
            {
                var start = Vertex(state, i, radius, theta);
                var end = Vertex(state, (i + k) % n, radius, theta);
                state.Vertices[i].transform.position = Vector3.Lerp(start, end, progress);
            }

            if (progress >= 1f)
            {
                // The shape is now formed — Ω starts here and runs through every later phase and the flight.
                state.RotationActive = true;
                state.Phase = FormationPhase.Collapse;
                state.PhaseElapsed = 0f;
            }
        }

        private void AdvanceCollapse(FormationState state, float dt)
        {
            var scale = RepScale(state);
            var duration = Mathf.Max(state.Tier.CollapseDuration * scale, MinPhaseDuration);
            state.PhaseElapsed += dt;
            var progress = Mathf.Clamp01(state.PhaseElapsed / duration);

            // Pure radial r→0 inside the rotating frame (theta already carries Ω, so the collapse spins with it).
            var radius = state.Radius * scale * (1f - progress);
            var theta = RepTheta(state);
            var n = state.VertexCount;
            var k = state.Tier.Skip;
            for (var i = 0; i < n; i++)
            {
                state.Vertices[i].transform.position = Vertex(state, (i + k) % n, radius, theta);
            }

            if (progress < 1f)
            {
                return;
            }

            if (state.Repetition + 1 < state.Tier.Repeats)
            {
                state.Repetition++;
                state.DeployFrom = state.Center;
                // Pen up for the inward travel — nested stars connect visually via the persisting
                // ribbons of earlier stars, not via travel lines (matches the reference nesting).
                for (var i = 0; i < state.VertexCount; i++)
                {
                    state.Vertices[i].SetRibbonEmitting(false);
                }

                state.Phase = FormationPhase.Deploy;
                state.PhaseElapsed = 0f;
                return;
            }

            BeginFinalFlight(state);
        }

        // The collapsed cluster (all vertices at r≈0, pen ON — one bright comet) flies with the centre to a
        // FRESHLY sampled endpoint. Ω keeps advancing (the point cluster spinning is invisible).
        private bool AdvanceFinalFlight(FormationState state)
        {
            for (var i = 0; i < state.VertexCount; i++)
            {
                state.Vertices[i].transform.position = state.Center;
            }

            WriteAnchor(state);

            if (state.FinalElapsed < state.CarrierFlightDuration)
            {
                return false;
            }

            ReportOnce(state, state.FinalTarget);
            state.Flights.Unregister(state.CarrierId);
            ReleaseVertices(state);
            ReleaseAnchor(state);
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

        private void BeginFinalFlight(FormationState state)
        {
            state.Phase = FormationPhase.FinalFlight;
            state.FinalElapsed = 0f;
            state.FinalStartCenter = state.Center;

            // Re-sample the endpoint NOW: the launch-time sample can go stale (the UI bar drifts), which reads
            // as the comet hitting below the target. Zero z onto the formation plane, like the cinematic does.
            var target = state.Target != null ? state.Target.RandomPosition() : state.Center;
            target.z = 0f;
            state.FinalTarget = target;
        }

        private void Settle(FormationState state, int index)
        {
            ReportOnce(state, state.Anchor != null ? state.Anchor.position : state.Center);
            state.Flights.Unregister(state.CarrierId);
            ReleaseAnchor(state);

            // Formation-phase interrupt: the vertices are still out, so fade them (unscaled — survives the
            // level-up freeze) before releasing.
            if (state.VerticesLive)
            {
                state.Phase = FormationPhase.SnapFade;
                state.FadeElapsed = 0f;
                return;
            }

            ReleaseStateAt(index);
        }

        private void ReportOnce(FormationState state, Vector3 at)
        {
            if (state.Reported)
            {
                return;
            }

            state.Reported = true;
            state.Reporter.ReportArrival(state.LastScore, state.Points, at);
        }

        private void HardRelease(FormationState state)
        {
            state.Flights.Unregister(state.CarrierId);
            ReleaseAnchor(state);
            ReleaseVertices(state);
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
                    state.Spawner.Release(state.Vertices[i]);
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

            state.Spawner = null;
            state.Flights = null;
            state.Reporter = null;
            state.Target = null;
            state.Anchor = null;
            state.Flight = null;
            _pool.Push(state);
        }

        // Bare, invisible Transform carrying the formation centre; the registry/cinematic need a Transform to
        // track/pause. Deactivated on release and pushed back, never destroyed.
        private Transform AcquireAnchor(Vector3 position)
        {
            var anchor = _anchorPool.Count > 0 ? _anchorPool.Pop() : new GameObject("ShapeFormationAnchor").transform;
            anchor.position = position;
            anchor.gameObject.SetActive(true);
            return anchor;
        }

        private void ReleaseAnchor(FormationState state)
        {
            if (state.Anchor == null)
            {
                return;
            }

            state.Anchor.gameObject.SetActive(false);
            _anchorPool.Push(state.Anchor);
            state.Anchor = null;
        }

        // Sum of every repetition's deploy+draw+collapse with the same per-phase clamps the tick uses,
        // so drift progress hits exactly 1 as the final collapse ends.
        private static float PhasedDuration(in BigScoreTierConfig tier)
        {
            var total = 0f;
            var scale = 1f;
            for (var rep = 0; rep < tier.Repeats; rep++)
            {
                total += Mathf.Max(tier.DeployDuration * scale, MinPhaseDuration)
                         + Mathf.Max(tier.DrawDuration * scale, MinPhaseDuration)
                         + Mathf.Max(tier.CollapseDuration * scale, MinPhaseDuration);
                scale *= tier.NestScale;
            }

            return total;
        }

        private static float RepScale(FormationState state)
        {
            return Mathf.Pow(state.Tier.NestScale, state.Repetition);
        }

        // Base orientation + nesting offset + the path rotation Ω, folded into one angle (R(Ω)·dir(φ) = dir(φ+Ω)).
        private static float RepTheta(FormationState state)
        {
            return InitialTheta + state.Repetition * state.Tier.NestRotationRadians + state.Rotation;
        }

        private static Vector3 Vertex(FormationState state, int index, float radius, float theta)
        {
            var phi = theta + TwoPi * index / state.VertexCount;
            Vector3 direction = VectorMathExtensions.DirectionFromAngle(phi);
            return state.Center + direction * radius;
        }

        private static void WriteAnchor(FormationState state)
        {
            if (state.Anchor != null)
            {
                state.Anchor.position = state.Center;
            }
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
            Deploy,
            Draw,
            Collapse,
            FinalFlight,
            SnapFade
        }

        private sealed class FormationState
        {
            internal readonly FlyingTrail[] Vertices = new FlyingTrail[MaxVertexCount];

            internal Transform Anchor;
            internal TrailFlight Flight;
            internal TrailSpawner Spawner;
            internal TrailFlightRegistry<TrailId> Flights;
            internal IScoreTrailReporter Reporter;
            internal ITrailEndpoint Target;
            internal BigScoreTierConfig Tier;
            internal TrailId CarrierId;
            internal Vector3 Center;
            internal Vector3 InitialCenter;
            internal Vector3 DeployFrom;
            internal Vector3 DriftTarget;
            internal Vector3 FinalStartCenter;
            internal Vector3 FinalTarget;
            internal float Radius;
            internal float Rotation;
            internal float TotalDuration;
            internal float TotalElapsed;
            internal float FinalElapsed;
            internal CancellationToken CancellationToken;
            internal FormationPhase Phase;
            internal float CarrierFlightDuration;
            internal float PhaseElapsed;
            internal float FadeElapsed;
            internal int Points;
            internal int LastScore;
            internal int VertexCount;
            internal int Repetition;
            internal bool VerticesLive;
            internal bool RotationActive;
            internal bool Reported;

            internal void Initialize(in BigScoreFormationRequest request)
            {
                Spawner = request.Spawner;
                Flights = request.Flights;
                Reporter = request.Reporter;
                Target = request.Target;
                Tier = request.Tier;
                CarrierId = request.CarrierId;
                Center = request.Center;
                InitialCenter = request.Center;
                DeployFrom = request.DeployFrom;
                Radius = request.Radius;
                Rotation = 0f;
                CancellationToken = request.CancellationToken;
                CarrierFlightDuration = request.CarrierFlightDuration;
                Points = request.Points;
                LastScore = request.LastScore;
                Phase = FormationPhase.Deploy;
                PhaseElapsed = 0f;
                FadeElapsed = 0f;
                FinalElapsed = 0f;
                VertexCount = 0;
                Repetition = 0;
                VerticesLive = false;
                RotationActive = false;
                Reported = false;
                Anchor = null;
                Flight = null;
            }
        }
    }
}

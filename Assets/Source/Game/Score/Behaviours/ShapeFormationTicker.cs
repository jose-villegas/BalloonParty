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
    ///     The analytic driver behind <see cref="BigScoreTrailBehaviour"/>: each live formation flies n vertex
    ///     trails through the deploy → draw → collapse phases of a star polygon {n/k} (nested m times), then
    ///     flashes them out and launches the carrier to the bar. All positions are closed-form and evaluated
    ///     once per <see cref="ILateTickable.LateTick"/>; state objects are pooled, so a running formation
    ///     never allocates. Mirrors <c>BalloonMotionTicker</c>'s pooled-state + swap-remove idioms.
    ///
    ///     Transport bridge — the carrier's <see cref="TrailFlight"/> handle is the formation's pause/snap/
    ///     slow-mo interface, polled every tick:
    ///     <list type="bullet">
    ///       <item>Paused — the level-up cinematic froze the principal; the formation freezes in place.</item>
    ///       <item>Idle — the cinematic pan-in or a <c>CompleteAll</c> (level-up/loss) drove the principal
    ///             home; snap: report the whole value now, fade the live vertices out (unscaled), release.</item>
    ///       <item>Speed — a slow-mo factor scaling the formation clock (matches the trail tweens).</item>
    ///     </list>
    /// </summary>
    internal sealed class ShapeFormationTicker : ILateTickable
    {
        private const int MaxVertexCount = 8;
        private const float FlashDuration = 0.1f;
        private const float MinPhaseDuration = 0.0001f;
        private const float TwoPi = Mathf.PI * 2f;
        private const float InitialTheta = Mathf.PI * 0.5f;

        private readonly List<FormationState> _states = new(8);
        private readonly Stack<FormationState> _pool = new(8);

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

                var carrierPhase = state.Flight.Phase;

                // Cinematic froze the principal — freeze the whole formation and let the cinematic own the
                // carrier transform (vertex trails stay put where they were last written).
                if (carrierPhase == FlightPhase.Paused)
                {
                    continue;
                }

                // Cinematic pan-in / CompleteAll drove the principal to Idle, or the carrier's own launch tween
                // landed. Either way the value settles now.
                if (carrierPhase == FlightPhase.Idle || state.CarrierLanded)
                {
                    Settle(state, i);
                    continue;
                }

                AdvanceFormation(state, dt * state.Flight.Speed, unscaledDt);
            }
        }

        // Synchronous so the carrier registers before the caller's Begin returns (the cinematic's registry wait
        // depends on it). Acquires the carrier + n vertex trails and seeds a pooled formation state.
        internal void Launch(in BigScoreFormationRequest request)
        {
            var state = _pool.Count > 0 ? _pool.Pop() : new FormationState();
            state.Initialize(in request);

            var carrier = request.Spawner.Acquire(request.Color);
            carrier.transform.position = request.Center;
            // Clear AFTER the teleport so the jump from the pooled position doesn't draw a streak.
            carrier.ClearRibbon();
            state.Carrier = carrier;
            state.Flight = request.Flights.Register(request.CarrierId, carrier.transform, request.Center);

            var count = request.Tier.VertexCount;
            for (var i = 0; i < count; i++)
            {
                var vertex = request.Spawner.Acquire(request.Color);
                vertex.SetRibbonTime(request.Tier.RibbonTime);
                vertex.transform.position = request.DeployFrom;
                vertex.transform.localScale = Vector3.one;
                vertex.ClearRibbon();
                state.Vertices[i] = vertex;
            }

            state.VertexCount = count;
            state.VerticesLive = true;
            _states.Add(state);
        }

        private void AdvanceFormation(FormationState state, float dt, float unscaledDt)
        {
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
                case FormationPhase.Merge:
                    AdvanceMerge(state, unscaledDt);
                    break;
                case FormationPhase.CarrierFlight:
                    // The launch tween owns the carrier; the transport-bridge poll handles landing/interrupt.
                    break;
            }
        }

        private void AdvanceDeploy(FormationState state, float dt)
        {
            var scale = RepScale(state);
            var duration = Mathf.Max(state.Tier.DeployDuration * scale, MinPhaseDuration);
            state.PhaseElapsed += dt;
            var progress = Mathf.Clamp01(state.PhaseElapsed / duration);

            var radius = state.Tier.BaseRadius * scale;
            var theta = RepTheta(state);
            for (var i = 0; i < state.VertexCount; i++)
            {
                var vertex = Vertex(state, i, radius, theta);
                state.Vertices[i].transform.position = Vector3.Lerp(state.DeployFrom, vertex, progress);
            }

            WriteCarrier(state);

            if (progress >= 1f)
            {
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

            var radius = state.Tier.BaseRadius * scale;
            var theta = RepTheta(state);
            var n = state.VertexCount;
            var k = state.Tier.Skip;
            for (var i = 0; i < n; i++)
            {
                var start = Vertex(state, i, radius, theta);
                var end = Vertex(state, (i + k) % n, radius, theta);
                state.Vertices[i].transform.position = Vector3.Lerp(start, end, progress);
            }

            WriteCarrier(state);

            if (progress >= 1f)
            {
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

            var radius = state.Tier.BaseRadius * scale;
            var theta = RepTheta(state);
            var spin = state.Tier.RotationSpeedRadians * state.PhaseElapsed;
            var radiusFactor = 1f - progress;
            var n = state.VertexCount;
            var k = state.Tier.Skip;
            for (var i = 0; i < n; i++)
            {
                var offset = Vertex(state, (i + k) % n, radius, theta) - state.Center;
                state.Vertices[i].transform.position = state.Center + Rotate(offset, spin) * radiusFactor;
            }

            WriteCarrier(state);

            if (progress < 1f)
            {
                return;
            }

            if (state.Repetition + 1 < state.Tier.Repeats)
            {
                state.Repetition++;
                state.DeployFrom = state.Center;
                state.Phase = FormationPhase.Deploy;
                state.PhaseElapsed = 0f;
                return;
            }

            state.Phase = FormationPhase.Merge;
            state.FadeElapsed = 0f;
        }

        private void AdvanceMerge(FormationState state, float unscaledDt)
        {
            state.FadeElapsed += unscaledDt;
            var fade = Mathf.Clamp01(state.FadeElapsed / FlashDuration);
            ScaleVertices(state, 1f - fade);
            WriteCarrier(state);

            if (fade < 1f)
            {
                return;
            }

            ReleaseVertices(state);
            LaunchCarrier(state);
            state.Phase = FormationPhase.CarrierFlight;
        }

        // True once the fade completes and the vertices have been released.
        private bool AdvanceSnapFade(FormationState state, float unscaledDt)
        {
            state.FadeElapsed += unscaledDt;
            var fade = Mathf.Clamp01(state.FadeElapsed / FlashDuration);
            ScaleVertices(state, 1f - fade);

            if (fade < 1f)
            {
                return false;
            }

            ReleaseVertices(state);
            return true;
        }

        private void Settle(FormationState state, int index)
        {
            ReportOnce(state, state.Carrier != null ? state.Carrier.transform.position : state.Center);
            state.Flights.Unregister(state.CarrierId);

            if (state.Carrier != null)
            {
                state.Spawner.Release(state.Carrier);
                state.Carrier = null;
            }

            // Formation-phase interrupt: the vertices are still out, so fade them (unscaled — survives the
            // level-up freeze) before releasing. A post-merge landing has none left, so drop the state now.
            if (state.VerticesLive)
            {
                state.Phase = FormationPhase.SnapFade;
                state.FadeElapsed = 0f;
                return;
            }

            ReleaseStateAt(index);
        }

        private void LaunchCarrier(FormationState state)
        {
            var target = state.Target != null ? state.Target.RandomPosition() : Vector3.zero;
            state.Carrier.Setup(target, state.Color, state.CarrierFlightDuration, state.OnCarrierLanded);
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

            if (state.Carrier != null)
            {
                // Pool return kills any in-flight launch tween via OnDespawned; no arrival fires.
                state.Spawner.Release(state.Carrier);
                state.Carrier = null;
            }

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
            state.Carrier = null;
            state.Flight = null;
            _pool.Push(state);
        }

        private static float RepScale(FormationState state)
        {
            return Mathf.Pow(state.Tier.NestScale, state.Repetition);
        }

        private static float RepTheta(FormationState state)
        {
            return InitialTheta + state.Repetition * state.Tier.NestRotationRadians;
        }

        private static Vector3 Vertex(FormationState state, int index, float radius, float theta)
        {
            var phi = theta + TwoPi * index / state.VertexCount;
            Vector3 direction = VectorMathExtensions.DirectionFromAngle(phi);
            return state.Center + direction * radius;
        }

        private static Vector3 Rotate(Vector3 v, float radians)
        {
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector3(v.x * cos - v.y * sin, v.x * sin + v.y * cos, v.z);
        }

        private static void WriteCarrier(FormationState state)
        {
            if (state.Carrier != null)
            {
                state.Carrier.transform.position = state.Center;
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
            Merge,
            CarrierFlight,
            SnapFade
        }

        private sealed class FormationState
        {
            internal readonly FlyingTrail[] Vertices = new FlyingTrail[MaxVertexCount];
            internal readonly Action OnCarrierLanded;

            internal FlyingTrail Carrier;
            internal TrailFlight Flight;
            internal TrailSpawner Spawner;
            internal TrailFlightRegistry<TrailId> Flights;
            internal IScoreTrailReporter Reporter;
            internal ITrailEndpoint Target;
            internal BigScoreTierConfig Tier;
            internal Color Color;
            internal TrailId CarrierId;
            internal Vector3 Center;
            internal Vector3 DeployFrom;
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
            internal bool CarrierLanded;
            internal bool Reported;

            internal FormationState()
            {
                OnCarrierLanded = () => CarrierLanded = true;
            }

            internal void Initialize(in BigScoreFormationRequest request)
            {
                Spawner = request.Spawner;
                Flights = request.Flights;
                Reporter = request.Reporter;
                Target = request.Target;
                Tier = request.Tier;
                Color = request.Color;
                CarrierId = request.CarrierId;
                Center = request.Center;
                DeployFrom = request.DeployFrom;
                CancellationToken = request.CancellationToken;
                CarrierFlightDuration = request.CarrierFlightDuration;
                Points = request.Points;
                LastScore = request.LastScore;
                Phase = FormationPhase.Deploy;
                PhaseElapsed = 0f;
                FadeElapsed = 0f;
                VertexCount = 0;
                Repetition = 0;
                VerticesLive = false;
                CarrierLanded = false;
                Reported = false;
                Carrier = null;
                Flight = null;
            }
        }
    }
}

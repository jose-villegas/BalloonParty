using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer.Unity;
using Light = BalloonParty.Shared.SceneLight.Light;
using Random = UnityEngine.Random;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Launch-screen ambience: a handful of soft point lights in the primary palette colours wander the
    ///     play area in Brownian drift, free to roam in and out past the scenario walls (a light springs
    ///     back only once it strays a margin beyond them). Alive ONLY on the launch screen
    ///     (<see cref="NavigationState.Launch" />) — like <see cref="LaunchDisturbanceStamp" /> it drives a
    ///     Game-scope field service while the game pre-warms. The moment Play is pressed
    ///     (<see cref="LaunchAscend.IsActive" />) they fade out early, so they're gone before the game appears.
    /// </summary>
    internal sealed class LaunchWanderingLights : IStartable, ITickable, IDisposable
    {
        private const int LightCount = 4;
        private const float LightRadius = 2.6f;
        private const float LightIntensity = 2.5f;
        private const float LightFalloffPower = 2f;

        // Brownian wander: a random acceleration each frame, damped by drag and capped, so the lights
        // meander rather than run away. WallMargin is how far past the walls they may drift before the
        // spring pulls them back — that slack is what reads as drifting "in and out" of the walls.
        private const float BrownianAcceleration = 6f;
        private const float Drag = 1.4f;
        private const float MaxSpeed = 2.2f;
        private const float WallMargin = 1.75f;
        private const float BoundarySpring = 5f;

        // Seconds to fade to black once Play is pressed; kept below the launch ascend so they clear early.
        private const float FadeOutSeconds = 0.6f;

        private readonly SceneLightFieldService _lightField;
        private readonly IGamePalette _palette;
        private readonly IProjectileFlightConfig _flightConfig;

        private readonly List<Light> _lights = new();
        private readonly List<IDisposable> _registrations = new();
        private readonly List<Vector2> _velocities = new();

        private WallLimits _walls;
        private float _fade = 1f;
        private bool _done;

        public LaunchWanderingLights(SceneLightFieldService lightField, IGamePalette palette,
            IProjectileFlightConfig flightConfig)
        {
            _lightField = lightField;
            _palette = palette;
            _flightConfig = flightConfig;
        }

        void IStartable.Start()
        {
            // Only decorate the launch screen; a session that boots straight into the game gets nothing.
            if (Navigation.Current.Value != NavigationState.Launch)
            {
                _done = true;
                return;
            }

            _walls = new WallLimits(_flightConfig.LimitsClockwise);

            var count = Mathf.Min(LightCount, _palette.Colors.Count);
            for (var i = 0; i < count; i++)
            {
                var light = new Light(RandomFieldPoint(), LightRadius, LightIntensity, i, LightFalloffPower);
                _lights.Add(light);
                _registrations.Add(_lightField.RegisterLight(light));
                _velocities.Add(Random.insideUnitCircle * MaxSpeed);
            }
        }

        void ITickable.Tick()
        {
            if (_done || _lights.Count == 0)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            // Fade out the instant Play is pressed (or if we somehow leave Launch without it), then tear
            // down once dark — so the lights are gone before the game scene appears.
            if (LaunchAscend.IsActive || Navigation.Current.Value != NavigationState.Launch)
            {
                _fade = Mathf.Max(0f, _fade - dt / FadeOutSeconds);
            }

            for (var i = 0; i < _lights.Count; i++)
            {
                var position = (Vector2)_lights[i].Position.Value;
                var velocity = _velocities[i];

                velocity += Random.insideUnitCircle * (BrownianAcceleration * dt);
                velocity += InwardPull(position) * dt;
                velocity *= Mathf.Max(0f, 1f - Drag * dt);
                velocity = Vector2.ClampMagnitude(velocity, MaxSpeed);
                position += velocity * dt;

                _velocities[i] = velocity;

                // Point light: keep both ends together (a diverging EndPosition would stretch it to a capsule).
                _lights[i].Position.Value = position;
                _lights[i].EndPosition.Value = position;
                _lights[i].Intensity.Value = LightIntensity * _fade;
            }

            if (_fade <= 0f)
            {
                Teardown();
            }
        }

        void IDisposable.Dispose()
        {
            Teardown();
        }

        // A soft inward spring once a light strays past the wall margin — bounded roaming, not escape.
        private Vector2 InwardPull(Vector2 position)
        {
            var pull = Vector2.zero;
            if (position.x < _walls.Left - WallMargin)
            {
                pull.x += BoundarySpring;
            }
            else if (position.x > _walls.Right + WallMargin)
            {
                pull.x -= BoundarySpring;
            }

            if (position.y < _walls.Bottom - WallMargin)
            {
                pull.y += BoundarySpring;
            }
            else if (position.y > _walls.Top + WallMargin)
            {
                pull.y -= BoundarySpring;
            }

            return pull;
        }

        private Vector3 RandomFieldPoint()
        {
            return new Vector3(
                Random.Range(_walls.Left, _walls.Right),
                Random.Range(_walls.Bottom, _walls.Top),
                0f);
        }

        private void Teardown()
        {
            _done = true;
            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }

            _registrations.Clear();
            _lights.Clear();
            _velocities.Clear();
        }
    }
}

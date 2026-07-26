using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;
using Light = BalloonParty.Shared.SceneLight.Light;
using Random = UnityEngine.Random;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Launch-screen ambience: a set of soft point lights in the primary palette colours wander the
    ///     play area in Brownian drift, free to roam in and out past the scenario walls (a light springs
    ///     back only once it strays a margin beyond them). Alive ONLY on the launch screen
    ///     (<see cref="NavigationState.Launch" />) — like <see cref="LaunchDisturbanceStamp" /> it drives a
    ///     Game-scope field service while the game pre-warms. The moment Play is pressed
    ///     (<see cref="LaunchAscend.IsActive" />) they fade out early, so they're gone before the game appears.
    /// </summary>
    internal sealed class LaunchWanderingLights : MonoBehaviour
    {
        [Tooltip("How many lights wander. Each takes a palette colour in turn, cycling the first Color Count.")]
        [SerializeField] private int _lightCount = 8;

        [Tooltip("How many leading palette colours the lights cycle through — the primaries.")]
        [SerializeField] private int _colorCount = 4;

        [SerializeField] private float _lightRadius = 2.6f;
        [SerializeField] private float _lightIntensity = 2.5f;
        [SerializeField] private float _lightFalloffPower = 2f;

        [Tooltip("Brownian wander: random acceleration applied each second, before drag and the speed cap.")]
        [SerializeField] private float _brownianAcceleration = 6f;
        [SerializeField] private float _drag = 1.4f;
        [SerializeField] private float _maxSpeed = 2.2f;

        [Tooltip("How far past a wall a light may drift before the spring reels it back — the slack that " +
                 "reads as drifting in and out of the walls.")]
        [SerializeField] private float _wallMargin = 1.75f;
        [SerializeField] private float _boundarySpring = 5f;

        [Tooltip("Seconds to fade out once Play is pressed; keep it below the launch ascend so they clear early.")]
        [SerializeField] private float _fadeOutSeconds = 0.6f;

        [Inject] private SceneLightFieldService _lightField;
        [Inject] private IGamePalette _palette;
        [Inject] private IProjectileFlightConfig _flightConfig;

        private readonly List<Light> _lights = new();
        private readonly List<IDisposable> _registrations = new();
        private readonly List<Vector2> _velocities = new();

        private WallLimits _walls;
        private float _fade = 1f;
        private bool _done;

        private void Start()
        {
            // Only decorate the launch screen; a session that boots straight into the game gets nothing.
            if (Navigation.Current.Value != NavigationState.Launch)
            {
                _done = true;
                return;
            }

            _walls = new WallLimits(_flightConfig.LimitsClockwise);

            var colors = ColorCycleCount();
            for (var i = 0; i < _lightCount; i++)
            {
                var light = new Light(RandomFieldPoint(), _lightRadius, _lightIntensity, i % colors, _lightFalloffPower);
                _lights.Add(light);
                _registrations.Add(_lightField.RegisterLight(light));
                _velocities.Add(Random.insideUnitCircle * _maxSpeed);
            }
        }

        private void Update()
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
                _fade = Mathf.Max(0f, _fade - dt / Mathf.Max(0.0001f, _fadeOutSeconds));
            }

            // Re-push the per-light appearance each frame so Inspector edits during play take effect live;
            // ReactiveProperty ignores unchanged writes, so a steady value costs nothing. (Light COUNT is
            // structural — it's fixed at Start; only the per-light fields update here.)
            var colors = ColorCycleCount();

            for (var i = 0; i < _lights.Count; i++)
            {
                var position = (Vector2)_lights[i].Position.Value;
                var velocity = _velocities[i];

                velocity += Random.insideUnitCircle * (_brownianAcceleration * dt);
                velocity += InwardPull(position) * dt;
                velocity *= Mathf.Max(0f, 1f - _drag * dt);
                velocity = Vector2.ClampMagnitude(velocity, _maxSpeed);
                position += velocity * dt;

                _velocities[i] = velocity;

                var light = _lights[i];
                // Point light: keep both ends together (a diverging EndPosition would stretch it to a capsule).
                light.Position.Value = position;
                light.EndPosition.Value = position;
                light.Radius.Value = _lightRadius;
                light.EndRadius.Value = _lightRadius;
                light.FalloffPower.Value = _lightFalloffPower;
                light.PaletteIndex.Value = i % colors;
                light.Intensity.Value = _lightIntensity * _fade;
            }

            if (_fade <= 0f)
            {
                Teardown();
            }
        }

        private void OnDestroy()
        {
            Teardown();
        }

        // How many leading palette colours the lights cycle through, clamped to what the palette holds.
        private int ColorCycleCount()
        {
            return Mathf.Clamp(_colorCount, 1, Mathf.Max(1, _palette.Colors.Count));
        }

        // A soft inward spring once a light strays past the wall margin — bounded roaming, not escape.
        private Vector2 InwardPull(Vector2 position)
        {
            var pull = Vector2.zero;
            if (position.x < _walls.Left - _wallMargin)
            {
                pull.x += _boundarySpring;
            }
            else if (position.x > _walls.Right + _wallMargin)
            {
                pull.x -= _boundarySpring;
            }

            if (position.y < _walls.Bottom - _wallMargin)
            {
                pull.y += _boundarySpring;
            }
            else if (position.y > _walls.Top + _wallMargin)
            {
                pull.y -= _boundarySpring;
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

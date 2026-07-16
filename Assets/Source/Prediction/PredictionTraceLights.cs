using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Prediction
{
    /// <summary>
    ///     Capsule (segment) lights mirroring the prediction line while the player aims — one per trace
    ///     leg (the calculator emits corner points only: launch, each wall bounce, the top-wall tip), so
    ///     the lights bend exactly where the line does. Intensity is tunable from launch to tip via
    ///     <see cref="IGameConfiguration.PredictionLightFadeCurve"/>, sampled at each leg's arc-length
    ///     midpoint. Lights register when the trace appears, are mutated in place on every aim update
    ///     (re-registering per frame would churn the field), and are disposed when the trace clears.
    /// </summary>
    internal class PredictionTraceLights : IDisposable
    {
        private readonly IGameConfiguration _config;
        private readonly SceneLightFieldService _lightField;
        private readonly int _paletteIndex;
        private readonly List<Light> _lights = new();
        private readonly List<IDisposable> _registrations = new();
        private readonly List<float> _cumulativeLengths = new();

        internal PredictionTraceLights(SceneLightFieldService lightField, IGameConfiguration config, int paletteIndex)
        {
            _lightField = lightField;
            _config = config;
            _paletteIndex = paletteIndex;
        }

        public void Dispose()
        {
            Clear();
        }

        internal void SetTrace(IReadOnlyList<Vector3> points)
        {
            var totalLength = MeasureTrace(points);

            // A degenerate (empty, single-point or zero-length) trace collapses the capsules to
            // NaN-prone slivers — turn the lights off entirely instead of leaving ghosts.
            if (totalLength <= Mathf.Epsilon)
            {
                Clear();
                return;
            }

            // The leg count changes while aiming (a rotating aim crosses bounce thresholds) —
            // rebuild only then; every other frame just mutates the registered lights.
            var legCount = points.Count - 1;
            if (_lights.Count != legCount)
            {
                Rebuild(legCount);
            }

            var baseIntensity = _config.PredictionLightIntensity;
            var fadeCurve = _config.PredictionLightFadeCurve;
            var halfWidth = _config.PredictionLightHalfWidth;
            var widthCurve = _config.PredictionLightWidthCurve;

            for (var i = 0; i < legCount; i++)
            {
                var light = _lights[i];
                light.Position.Value = points[i];
                light.EndPosition.Value = points[i + 1];

                // Sample the fade at the leg's arc-length midpoint so uneven legs (a long first
                // throw, short post-bounce legs) sit where they actually are along the line.
                var midFraction = (_cumulativeLengths[i] + _cumulativeLengths[i + 1]) * 0.5f / totalLength;
                light.Intensity.Value = baseIntensity * fadeCurve.Evaluate(midFraction);

                // Width samples at the leg's endpoints; the stamp lerps between them along its axis,
                // so adjacent legs share their boundary value and the taper is continuous across
                // bounces — a piecewise-linear read of the curve.
                light.Radius.Value = halfWidth * widthCurve.Evaluate(_cumulativeLengths[i] / totalLength);
                light.EndRadius.Value = halfWidth * widthCurve.Evaluate(_cumulativeLengths[i + 1] / totalLength);
            }
        }

        internal void Clear()
        {
            if (_registrations.Count == 0)
            {
                return;
            }

            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }

            _registrations.Clear();
            _lights.Clear();
        }

        private void Rebuild(int legCount)
        {
            Clear();

            for (var i = 0; i < legCount; i++)
            {
                var light = Light.Segment(Vector3.zero, Vector3.zero,
                    _config.PredictionLightHalfWidth,
                    _config.PredictionLightIntensity,
                    _paletteIndex,
                    _config.PredictionLightFalloffPower);
                _lights.Add(light);
                _registrations.Add(_lightField.RegisterLight(light));
            }
        }

        private float MeasureTrace(IReadOnlyList<Vector3> points)
        {
            _cumulativeLengths.Clear();

            if (points == null || points.Count < 2)
            {
                return 0f;
            }

            _cumulativeLengths.Add(0f);
            var total = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                total += Vector3.Distance(points[i - 1], points[i]);
                _cumulativeLengths.Add(total);
            }

            return total;
        }
    }
}

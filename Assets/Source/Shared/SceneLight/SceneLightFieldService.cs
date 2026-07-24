using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Cadence;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared.SceneLight
{
    /// <summary>
    ///     Owns the scene-light FIELD — a small screen/world-space RT (the disturbance-field
    ///     architecture applied to light, see @ref plan_lighting "Milestone 3"). The field is purely
    ///     LOCAL: it carries only what registered lights stamp (R = local boost, GB = local direction
    ///     weight, A = palette tag). The ambient (direction/colour/intensity) is owned and pushed by
    ///     <see cref="TimeOfDayService"/>; consumers combine that local + ambient in-shader. A render runs a
    ///     three-pass ping-pong pipeline: <b>fill</b> to the empty rest state, <b>accumulate</b> every
    ///     registered light's cone into R (tagging A), then <b>gradient</b> to write the local direction
    ///     into GB.
    ///
    ///     Lights are STATE, not events: a caller <see cref="RegisterLight"/>s a <see cref="Light"/> to
    ///     turn it on and disposes the registration to turn it off. The service watches each light's
    ///     reactive properties and re-renders only when one changed AND a frame-interval cadence cap (see
    ///     <see cref="ISceneLightFieldSettings.FieldFrameInterval"/>) has elapsed — an idle scene costs
    ///     nothing, a moving light can't force the pipeline past its authored cadence regardless of display
    ///     refresh rate, and ambient tweaks never re-render it (consumers read those from the globals live).
    ///     Past the cadence gate, a texel-quantized batch comparison further absorbs any write too small
    ///     for the RT to resolve (sub-texel drift, a converged fade tail) — so "changed" means changed by
    ///     more than the field's own resolution, not merely written.
    /// </summary>
    internal class SceneLightFieldService : IStartable, ITickable, IDisposable, ICadencedEffect
    {
        // The accumulate shader's MAX_STAMPS: the compile-time size of its stamp arrays, so the batch
        // arrays here match it and the configured MaxLights is clamped to it (raising the runtime cap past
        // this needs the shader edited too). Density/cap/ceiling are tuned via ISceneLightFieldSettings.
        private const int ShaderStampCapacity = 32;

        private static readonly int BoundsMinId = Shader.PropertyToID("_SceneLightFieldBoundsMin");
        private static readonly int BoundsSizeId = Shader.PropertyToID("_SceneLightFieldBoundsSize");
        private static readonly int TexelSizeId = Shader.PropertyToID("_SceneLightTexelSize");
        private static readonly int PaletteId = Shader.PropertyToID("_SceneLightPalette");
        private static readonly int FieldOnId = Shader.PropertyToID("_SceneLightFieldOn");
        private static readonly int MaxBoostId = Shader.PropertyToID("_MaxBoost");
        private static readonly int DirectionResponseId = Shader.PropertyToID("_DirectionResponse");
        private static readonly int StampAspectId = Shader.PropertyToID("_StampAspect");
        private static readonly int StampCountId = Shader.PropertyToID("_StampCount");
        private static readonly int StampCentersId = Shader.PropertyToID("_StampCenters");
        private static readonly int StampRadiiId = Shader.PropertyToID("_StampRadii");
        private static readonly int StampEndRadiiId = Shader.PropertyToID("_StampEndRadii");
        private static readonly int StampMagnitudesId = Shader.PropertyToID("_StampMagnitudes");
        private static readonly int StampFalloffsId = Shader.PropertyToID("_StampFalloffs");
        private static readonly int StampColorIndicesId = Shader.PropertyToID("_StampColorIndices");

        private readonly IGameDisplayConfiguration _displayConfig;
        private readonly IGamePalette _palette;
        private readonly ISceneLightFieldSettings _settings;
        private readonly SceneLightFieldResources _resources = new();
        private readonly Vector4[] _paletteBuffer = new Vector4[PaletteChannelEncoding.Slots];
        private readonly List<Registration> _lights = new();
        private readonly Vector4[] _batchCenters = new Vector4[ShaderStampCapacity];
        private readonly float[] _batchRadii = new float[ShaderStampCapacity];
        private readonly float[] _batchEndRadii = new float[ShaderStampCapacity];
        private readonly float[] _batchMagnitudes = new float[ShaderStampCapacity];
        private readonly float[] _batchFalloffs = new float[ShaderStampCapacity];
        private readonly float[] _batchColorIndices = new float[ShaderStampCapacity];

        // The batch actually last sent to the GPU, kept to compare against the freshly built one — see
        // BatchUnchanged. Mirrors the _batch* set shape/order so the two stay easy to read side by side.
        private readonly Vector4[] _renderedCenters = new Vector4[ShaderStampCapacity];
        private readonly float[] _renderedRadii = new float[ShaderStampCapacity];
        private readonly float[] _renderedEndRadii = new float[ShaderStampCapacity];
        private readonly float[] _renderedMagnitudes = new float[ShaderStampCapacity];
        private readonly float[] _renderedFalloffs = new float[ShaderStampCapacity];
        private readonly float[] _renderedColorIndices = new float[ShaderStampCapacity];

        private DisturbanceFieldCoordinates _coords;
        private int _maxLights;
        private float _stampAspect = 1f;
        private float _epsilonX;
        private float _epsilonY;
        private float _epsilonRadius;
        private bool _dirty = true;
        private bool _warnedOverflow;
        private bool _fieldOn;
        private float _renderAccumulator;

        // -1 forces the first comparison in Tick to fail, so nothing can skip before a real render has
        // ever populated the _rendered* arrays.
        private int _renderedCount = -1;

        internal RenderTexture FieldTexture => _resources.FieldTexture;

        internal SceneLightFieldService(
            IGameDisplayConfiguration displayConfig, IGamePalette palette,
            ISceneLightFieldSettings settings)
        {
            _displayConfig = displayConfig;
            _palette = palette;
            _settings = settings;
        }

        void IStartable.Start()
        {
            _maxLights = Mathf.Clamp(_settings.MaxLights, 1, ShaderStampCapacity);
            _coords = new DisturbanceFieldCoordinates(_displayConfig, _settings.TexelsPerUnit);

            // UV space is normalised per-axis over a non-square field, so the accumulate shader corrects
            // the vertical delta by this ratio to keep a radius circular in world space.
            _stampAspect = _coords.Bounds.height / _coords.Bounds.width;

            // Half a texel per axis: a stamp delta below this can't move the rendered RT, so
            // BatchUnchanged treats it as unchanged rather than paying for an invisible re-render.
            _epsilonX = 0.5f / _coords.Width;
            _epsilonY = 0.5f / _coords.Height;

            // Radii are scalar UV (not per-axis), so use the conservative (finer) axis.
            _epsilonRadius = Mathf.Min(_epsilonX, _epsilonY);
            _resources.Initialize(
                _coords.Width, _coords.Height,
                _settings.FillShader, _settings.AccumulateShader, _settings.GradientShader);
            PushGlobalBounds();

            // Texel size lets the include point-sample A (the palette index) at a texel centre — the
            // channel is bilinear like R/GB, so an interpolated index would decode to a wrong colour.
            Shader.SetGlobalVector(TexelSizeId, new Vector4(1f / _coords.Width, 1f / _coords.Height, 0f, 0f));

            // The palette is static config, so push it once as a global the include decodes A against.
            PushGlobalPalette();
        }

        // Re-renders the field's pipeline only when a registered light changed AND the cadence cap (see
        // FieldFrameInterval) allows it — a fast-moving light would otherwise dirty the field every frame,
        // running the pipeline at the display's full refresh rate for identical visuals. An idle scene
        // skips the pipeline entirely; the RT keeps its last (still-correct) contents. Past that gate, the
        // freshly built batch is also compared against the one last sent to the GPU (BatchUnchanged) — a
        // write the RT's texel resolution can't distinguish (sub-texel drift, a converged fade tail) is
        // absorbed rather than paying for a visually identical re-render.
        void ITickable.Tick()
        {
            // FieldFrameInterval is authored as "every N frames at 60 fps"; reinterpreted here as seconds
            // so the field's re-render cost doesn't scale with display refresh — a 120 Hz panel would
            // otherwise double it. Unscaled time on purpose: the dirty gate already makes a frozen scene
            // free, and lights animating during the level-up freeze keep working. Clamped to at most one
            // interval so idle time can't bank multiple instant re-renders.
            var interval = _settings.FieldFrameInterval / 60f;
            _renderAccumulator = Mathf.Min(_renderAccumulator + Time.unscaledDeltaTime, interval);

            if (!_resources.IsReady || (_fieldOn && (!_dirty || _renderAccumulator < interval)))
            {
                return;
            }

            var count = BuildBatch();

            // The batch changed by less than the field can resolve (sub-texel drift, a converged fade
            // tail, an equal-within-a-texel rewrite) — skip the pipeline. Safe to clear _dirty: any further
            // write re-fires the light's ReactiveProperty subscription and dirties it again. Don't touch
            // the accumulator here — an absorbed write isn't a render, so a real change arriving next tick
            // should still be judged against the same cadence boundary, not one this skip already spent.
            if (count == _renderedCount && BatchUnchanged(count))
            {
                _dirty = false;
                return;
            }

            RunAccumulate(count);
            PushGradientParams();
            _resources.Gradient();

            _dirty = false;

            // Subtract, don't reset: a render can fire below the interval (the ungated bootstrap render),
            // and a plain reset would delay the next one.
            _renderAccumulator = Mathf.Max(0f, _renderAccumulator - interval);

            // Remember what was actually sent to the GPU so the next tick's batch can be compared against
            // it. The stale tail beyond count is never read back — count is compared first.
            Array.Copy(_batchCenters, _renderedCenters, count);
            Array.Copy(_batchRadii, _renderedRadii, count);
            Array.Copy(_batchEndRadii, _renderedEndRadii, count);
            Array.Copy(_batchMagnitudes, _renderedMagnitudes, count);
            Array.Copy(_batchFalloffs, _renderedFalloffs, count);
            Array.Copy(_batchColorIndices, _renderedColorIndices, count);
            _renderedCount = count;

            // The on-flag is static once the field is live; set it after the first full pipeline render.
            if (!_fieldOn)
            {
                Shader.SetGlobalFloat(FieldOnId, 1f);
                _fieldOn = true;
            }
        }

        void IDisposable.Dispose()
        {
            Shader.SetGlobalFloat(FieldOnId, 0f);
            _fieldOn = false;
            ClearLights();
            _resources.Dispose();
        }

        int ICadencedEffect.BlitWeight => 2;

        void ICadencedEffect.ApplyPhaseOffset(float offset01)
        {
            var interval = _settings.FieldFrameInterval / 60f;
            _renderAccumulator = offset01 * interval;
        }

        /// <summary>Turns <paramref name="light"/> on: the field composites it every render until the returned
        /// registration is disposed. Mutating the light's reactive properties (position, intensity, …) marks
        /// the field dirty so it re-renders — the caller owns the light's whole on/off/animate lifecycle.</summary>
        internal IDisposable RegisterLight(Light light)
        {
            var subscription = new CompositeDisposable();
            // Any change to a live light dirties the field. ReactiveProperty fires its current value on
            // subscribe, so registering also dirties (the first render picks the light up).
            light.Position.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.EndPosition.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.Radius.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.EndRadius.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.Intensity.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.FalloffPower.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.PaletteIndex.Subscribe(_ => _dirty = true).AddTo(subscription);

            _lights.Add(new Registration(light, subscription));

            return new LightRegistrationHandle(this, light);
        }

        /// <summary>Turns every registered light off at once (each registration's own dispose still works).</summary>
        internal void ClearLights()
        {
            foreach (var registration in _lights)
            {
                registration.Subscription.Dispose();
            }

            _lights.Clear();
            _dirty = true;
        }

        private void Unregister(Light light)
        {
            for (var i = _lights.Count - 1; i >= 0; i--)
            {
                if (_lights[i].Light != light)
                {
                    continue;
                }

                _lights[i].Subscription.Dispose();
                _lights.RemoveAt(i);
                _dirty = true;
                return;
            }
        }

        private int BuildBatch()
        {
            var count = 0;

            foreach (var registration in _lights)
            {
                if (count >= _maxLights)
                {
                    WarnOverflowOnce();
                    break;
                }

                var light = registration.Light;
                // A stamp is a capsule: xy = start UV, zw = end UV. Point light ⇒ end == start.
                var startUV = _coords.WorldToUV(light.Position.Value);
                var endUV = _coords.WorldToUV(light.EndPosition.Value);
                _batchCenters[count] = new Vector4(startUV.x, startUV.y, endUV.x, endUV.y);
                _batchRadii[count] = _coords.WorldRadiusToUV(light.Radius.Value);
                _batchEndRadii[count] = _coords.WorldRadiusToUV(light.EndRadius.Value);
                _batchMagnitudes[count] = light.Intensity.Value;
                _batchFalloffs[count] = light.FalloffPower.Value;
                _batchColorIndices[count] = PaletteChannelEncoding.Encode(light.PaletteIndex.Value);
                count++;
            }

            return count;
        }

        // Compares the freshly built batch against the one last sent to the GPU, tolerant of deltas the
        // field's own texel resolution can't resolve — see the field-level epsilons computed in Start.
        // Plain loop, no LINQ/allocation: this runs every tick the cadence gate passes.
        private bool BatchUnchanged(int count)
        {
            const float valueEpsilon = 1e-3f;

            for (var i = 0; i < count; i++)
            {
                var center = _batchCenters[i];
                var renderedCenter = _renderedCenters[i];
                if (Mathf.Abs(center.x - renderedCenter.x) >= _epsilonX ||
                    Mathf.Abs(center.z - renderedCenter.z) >= _epsilonX ||
                    Mathf.Abs(center.y - renderedCenter.y) >= _epsilonY ||
                    Mathf.Abs(center.w - renderedCenter.w) >= _epsilonY)
                {
                    return false;
                }

                if (Mathf.Abs(_batchRadii[i] - _renderedRadii[i]) >= _epsilonRadius ||
                    Mathf.Abs(_batchEndRadii[i] - _renderedEndRadii[i]) >= _epsilonRadius)
                {
                    return false;
                }

                if (Mathf.Abs(_batchMagnitudes[i] - _renderedMagnitudes[i]) >= valueEpsilon ||
                    Mathf.Abs(_batchFalloffs[i] - _renderedFalloffs[i]) >= valueEpsilon)
                {
                    return false;
                }

                // The encoded palette index is discrete, not a continuum — any change is a different
                // colour, so this compares exactly rather than tolerating drift.
                if (_batchColorIndices[i] != _renderedColorIndices[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void RunAccumulate(int count)
        {
            var material = _resources.AccumulateMaterial;
            material.SetInt(StampCountId, count);

            if (count > 0)
            {
                material.SetVectorArray(StampCentersId, _batchCenters);
                material.SetFloatArray(StampRadiiId, _batchRadii);
                material.SetFloatArray(StampEndRadiiId, _batchEndRadii);
                material.SetFloatArray(StampMagnitudesId, _batchMagnitudes);
                material.SetFloatArray(StampFalloffsId, _batchFalloffs);
                material.SetFloatArray(StampColorIndicesId, _batchColorIndices);
                material.SetFloat(MaxBoostId, _settings.AccumulationCeiling);
                material.SetFloat(StampAspectId, _stampAspect);
            }

            _resources.BlitAndSwap(material);
        }

        // How strongly local brightness bends the direction toward a light (see the gradient shader);
        // pushed each render so it's live-tunable.
        private void PushGradientParams()
        {
            _resources.GradientMaterial.SetFloat(DirectionResponseId, _settings.DirectionResponse);
        }

        private void WarnOverflowOnce()
        {
            if (_warnedOverflow)
            {
                return;
            }

            Log.Warn("SceneLightField", $"more than {_maxLights} lights registered — the extras " +
                             "are dropped this render. Raise MaxLights in the settings (up to the shader's " +
                             $"{ShaderStampCapacity}) if this is real.");
            _warnedOverflow = true;
        }

        private void PushGlobalBounds()
        {
            var bounds = _coords.Bounds;
            Shader.SetGlobalVector(BoundsMinId, new Vector4(bounds.xMin, bounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(BoundsSizeId, new Vector4(bounds.width, bounds.height, 0f, 0f));
        }

        // The same slot order the lights encode into A (IGamePalette.Colors); unused slots stay black.
        private void PushGlobalPalette()
        {
            var count = Mathf.Min(_palette.Colors.Count, _paletteBuffer.Length);
            for (var i = 0; i < count; i++)
            {
                _paletteBuffer[i] = _palette.Colors[i].Color;
            }

            Shader.SetGlobalVectorArray(PaletteId, _paletteBuffer);
        }

        private readonly struct Registration
        {
            public readonly Light Light;
            public readonly CompositeDisposable Subscription;

            public Registration(Light light, CompositeDisposable subscription)
            {
                Light = light;
                Subscription = subscription;
            }
        }

        /// <summary>Lightweight disposable that calls Unregister directly — avoids the closure +
        /// AnonymousDisposable allocation from <c>Disposable.Create</c>.</summary>
        private sealed class LightRegistrationHandle : IDisposable
        {
            private SceneLightFieldService _service;
            private Light _light;

            public LightRegistrationHandle(SceneLightFieldService service, Light light)
            {
                _service = service;
                _light = light;
            }

            public void Dispose()
            {
                _service?.Unregister(_light);
                _service = null;
                _light = null;
            }
        }
    }
}

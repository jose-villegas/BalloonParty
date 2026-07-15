using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
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
    ///     weight, A = palette tag). The ambient (direction/colour/intensity) is the global set that
    ///     <c>SceneLightService</c> publishes and the consumers combine with the field — so this service
    ///     doesn't touch the ambient owner at all. A render runs a three-pass ping-pong pipeline:
    ///     <b>fill</b> to the empty rest state, <b>accumulate</b> every registered light's cone into R
    ///     (tagging A), then <b>gradient</b> to write the local direction into GB.
    ///
    ///     Lights are STATE, not events: a caller <see cref="RegisterLight"/>s a <see cref="Light"/> to
    ///     turn it on and disposes the registration to turn it off. The service watches each light's
    ///     reactive properties and only re-renders when one changed — an idle scene costs nothing, and
    ///     ambient tweaks never re-render it (consumers read those from the globals live).
    /// </summary>
    internal class SceneLightFieldService : IStartable, ITickable, IDisposable
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
        private readonly float[] _batchMagnitudes = new float[ShaderStampCapacity];
        private readonly float[] _batchFalloffs = new float[ShaderStampCapacity];
        private readonly float[] _batchColorIndices = new float[ShaderStampCapacity];

        private DisturbanceFieldCoordinates _coords;
        private int _maxLights;
        private float _stampAspect = 1f;
        private bool _dirty = true;
        private bool _warnedOverflow;
        private bool _fieldOn;

        internal RenderTexture FieldTexture => _resources.FieldTexture;

        internal SceneLightFieldService(
            IGameDisplayConfiguration displayConfig, IGamePalette palette, ISceneLightFieldSettings settings)
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

        // Re-renders only when a registered light changed (the _dirty flag its reactive properties set).
        // The field is purely local, so ambient tweaks (direction/colour/intensity) never touch it —
        // consumers read those from the globals live. An idle scene skips the pipeline entirely; the RT
        // keeps its last (still-correct) contents.
        void ITickable.Tick()
        {
            if (!_resources.IsReady || (_fieldOn && !_dirty))
            {
                return;
            }

            var count = BuildBatch();
            _resources.Fill();
            RunAccumulate(count);
            PushGradientParams();
            _resources.Gradient();

            _dirty = false;

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
            light.Intensity.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.FalloffPower.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.PaletteIndex.Subscribe(_ => _dirty = true).AddTo(subscription);

            _lights.Add(new Registration(light, subscription));

            return Disposable.Create(() => Unregister(light));
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
                _batchMagnitudes[count] = light.Intensity.Value;
                _batchFalloffs[count] = light.FalloffPower.Value;
                _batchColorIndices[count] = PaletteChannelEncoding.Encode(light.PaletteIndex.Value);
                count++;
            }

            return count;
        }

        private void RunAccumulate(int count)
        {
            if (count == 0)
            {
                return;
            }

            var material = _resources.AccumulateMaterial;
            material.SetInt(StampCountId, count);
            material.SetVectorArray(StampCentersId, _batchCenters);
            material.SetFloatArray(StampRadiiId, _batchRadii);
            material.SetFloatArray(StampMagnitudesId, _batchMagnitudes);
            material.SetFloatArray(StampFalloffsId, _batchFalloffs);
            material.SetFloatArray(StampColorIndicesId, _batchColorIndices);
            material.SetFloat(MaxBoostId, _settings.AccumulationCeiling);
            material.SetFloat(StampAspectId, _stampAspect);

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

            Debug.LogWarning($"SceneLightFieldService: more than {_maxLights} lights registered — the extras " +
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
    }
}

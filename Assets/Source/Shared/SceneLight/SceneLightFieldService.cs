using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Display;
using BalloonParty.Shared.Disturbance;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared.SceneLight
{
    /// <summary>
    ///     Owns the scene-light FIELD — a small screen/world-space RT (the disturbance-field
    ///     architecture applied to light, see @ref plan_lighting "Milestone 3"). A render runs a
    ///     three-pass ping-pong pipeline: <b>fill</b> to the directional system's rest state,
    ///     <b>accumulate</b> every registered light's magnitude into R (tagging A with the dominant
    ///     light's palette index), then <b>gradient</b> to recompute the GB direction from grad(R) — so
    ///     the direction always points toward the brightest nearby light.
    ///
    ///     Lights are STATE, not events: a caller <see cref="RegisterLight"/>s a <see cref="Light"/> to
    ///     turn it on and disposes the registration to turn it off. The service watches each light's
    ///     reactive properties and the directional owner, and only re-renders when something changed —
    ///     an idle scene costs nothing. With no lights the pipeline reproduces the rest field
    ///     bit-for-bit, so it stays identical to the directional system; the shared include's helpers
    ///     fall back to the flat globals whenever <c>_SceneLightFieldOn</c> is 0.
    /// </summary>
    internal class SceneLightFieldService : IStartable, ITickable, IDisposable
    {
        // Matches the disturbance field's density so the two fields share a world scale; the light
        // field is smooth so it could be coarser, but reusing the value keeps DisturbanceFieldCoordinates
        // producing an identically fitted play-area rectangle.
        private const int TexelsPerUnit = 8;
        private const int MaxLights = 32;
        private const float PaletteIndexSlots = 16f;

        // Ceiling on the summed additive boost the accumulate pass can add above the rest magnitude;
        // overlapping lights approach it asymptotically instead of blowing R out. See the shader.
        private const float AccumulationCeiling = 3f;

        private static readonly int BoundsMinId = Shader.PropertyToID("_SceneLightFieldBoundsMin");
        private static readonly int BoundsSizeId = Shader.PropertyToID("_SceneLightFieldBoundsSize");
        private static readonly int FieldOnId = Shader.PropertyToID("_SceneLightFieldOn");
        private static readonly int MaxBoostId = Shader.PropertyToID("_MaxBoost");
        private static readonly int StampAspectId = Shader.PropertyToID("_StampAspect");
        private static readonly int StampCountId = Shader.PropertyToID("_StampCount");
        private static readonly int StampCentersId = Shader.PropertyToID("_StampCenters");
        private static readonly int StampRadiiId = Shader.PropertyToID("_StampRadii");
        private static readonly int StampMagnitudesId = Shader.PropertyToID("_StampMagnitudes");
        private static readonly int StampEdgesId = Shader.PropertyToID("_StampEdges");
        private static readonly int StampColorIndicesId = Shader.PropertyToID("_StampColorIndices");

        private readonly IGameDisplayConfiguration _displayConfig;
        private readonly SceneLightFieldResources _resources = new();
        private readonly List<Registration> _lights = new();
        private readonly Vector4[] _batchCenters = new Vector4[MaxLights];
        private readonly float[] _batchRadii = new float[MaxLights];
        private readonly float[] _batchMagnitudes = new float[MaxLights];
        private readonly float[] _batchEdges = new float[MaxLights];
        private readonly float[] _batchColorIndices = new float[MaxLights];

        private DisturbanceFieldCoordinates _coords;
        private SceneLightService _sceneLight;
        private float _stampAspect = 1f;
        private Vector2 _lastDirection;
        private float _lastIntensity = float.NaN;
        private bool _dirty = true;
        private bool _warnedOverflow;
        private bool _fieldOn;

        internal RenderTexture FieldTexture => _resources.FieldTexture;

        internal SceneLightFieldService(IGameDisplayConfiguration displayConfig)
        {
            _displayConfig = displayConfig;
        }

        void IStartable.Start()
        {
            _coords = new DisturbanceFieldCoordinates(_displayConfig, TexelsPerUnit);

            // UV space is normalised per-axis over a non-square field, so the accumulate shader corrects
            // the vertical delta by this ratio to keep a radius circular in world space.
            _stampAspect = _coords.Bounds.height / _coords.Bounds.width;
            _resources.Initialize(_coords.Width, _coords.Height);
            PushGlobalBounds();

            // The directional owner lives on a scene object (Game.unity's "Lighting"), unreachable from
            // a shared prefab, so resolve it at runtime — the ScreenSpaceLightService precedent.
            _sceneLight = UnityEngine.Object.FindFirstObjectByType<SceneLightService>();
            if (_sceneLight == null)
            {
                Debug.LogError("SceneLightFieldService: no SceneLightService in the scene — the light " +
                               "field stays off (_SceneLightFieldOn = 0); consumers use the flat globals.");
            }
        }

        // Re-renders only when a registered light changed (the _dirty flag its reactive properties set) or
        // the directional owner's live-tunable direction/intensity moved. An idle scene skips the pipeline
        // entirely — the RT keeps its last contents, which are still correct.
        void ITickable.Tick()
        {
            if (_sceneLight == null || !_resources.IsReady)
            {
                return;
            }

            var direction = _sceneLight.Direction;
            var intensity = _sceneLight.Intensity;
            var ownerChanged = direction != _lastDirection || !Mathf.Approximately(intensity, _lastIntensity);

            if (_fieldOn && !_dirty && !ownerChanged)
            {
                return;
            }

            var count = BuildBatch();
            _resources.Fill(intensity, direction);
            RunAccumulate(count);
            _resources.Gradient();

            _lastDirection = direction;
            _lastIntensity = intensity;
            _dirty = false;

            // The on-flag is static once the field is live; set it after the first full pipeline render so a
            // missing owner leaves it at 0 and consumers fall back to the flat globals.
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
            light.Radius.Subscribe(_ => _dirty = true).AddTo(subscription);
            light.Intensity.Subscribe(_ => _dirty = true).AddTo(subscription);
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

        // Packs the registered lights into the batch arrays, capped at MaxLights. Reads each light's
        // reactive values into the parallel arrays the accumulate shader uploads.
        private int BuildBatch()
        {
            var count = 0;

            foreach (var registration in _lights)
            {
                if (count >= MaxLights)
                {
                    WarnOverflowOnce();
                    break;
                }

                var light = registration.Light;
                _batchCenters[count] = ToVector4(_coords.WorldToUV(light.Position.Value));
                _batchRadii[count] = _coords.WorldRadiusToUV(light.Radius.Value);
                _batchMagnitudes[count] = light.Intensity.Value;
                _batchEdges[count] = light.EdgeSoftness;
                _batchColorIndices[count] = Encode(light.PaletteIndex.Value);
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
            material.SetFloatArray(StampEdgesId, _batchEdges);
            material.SetFloatArray(StampColorIndicesId, _batchColorIndices);
            material.SetFloat(MaxBoostId, AccumulationCeiling);
            material.SetFloat(StampAspectId, _stampAspect);

            _resources.BlitAndSwap(material);
        }

        // Encoded so 0 always reads "no colour" in the shader; indices quantize into 16 slots. -1 = none.
        private static float Encode(int paletteIndex)
        {
            return paletteIndex >= 0 ? (paletteIndex + 1f) / PaletteIndexSlots : 0f;
        }

        private static Vector4 ToVector4(Vector2 uv)
        {
            return new Vector4(uv.x, uv.y, 0f, 0f);
        }

        private void WarnOverflowOnce()
        {
            if (_warnedOverflow)
            {
                return;
            }

            Debug.LogWarning($"SceneLightFieldService: more than {MaxLights} lights registered — the extras " +
                             "are dropped this render (one accumulate batch). Raise MaxLights if this is real.");
            _warnedOverflow = true;
        }

        private void PushGlobalBounds()
        {
            var bounds = _coords.Bounds;
            Shader.SetGlobalVector(BoundsMinId, new Vector4(bounds.xMin, bounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(BoundsSizeId, new Vector4(bounds.width, bounds.height, 0f, 0f));
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

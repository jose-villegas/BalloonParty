using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using UnityEngine;
using VContainer.Unity;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Shared.Disturbance
{
    /// <summary>Screen-space disturbance field any system can stamp into; GPU resources live in <see cref="DisturbanceFieldResources"/>.</summary>
    internal class DisturbanceFieldService : IStartable, ITickable, IDisposable
    {
        private const int MaxStampsPerBatch = 32;
        private const float PaletteIndexSlots = 16f;

        private static readonly int DiffusionRateId = Shader.PropertyToID("_DiffusionRate");
        private static readonly int ReformSpeedId = Shader.PropertyToID("_ReformSpeed");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int WindDirId = Shader.PropertyToID("_WindDir");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int PressureStrId = Shader.PropertyToID("_PressureStr");
        private static readonly int DisplaceDecayId = Shader.PropertyToID("_DisplaceDecay");
        private static readonly int DisplaceAmountId = Shader.PropertyToID("_DisplaceAmount");
        private static readonly int StampAspectId = Shader.PropertyToID("_StampAspect");
        private static readonly int StampCountId = Shader.PropertyToID("_StampCount");
        private static readonly int StampCentersId = Shader.PropertyToID("_StampCenters");
        private static readonly int StampRadiiId = Shader.PropertyToID("_StampRadii");
        private static readonly int StampStrengthsId = Shader.PropertyToID("_StampStrengths");
        private static readonly int StampDirectionsId = Shader.PropertyToID("_StampDirections");
        private static readonly int StampColorIndicesId = Shader.PropertyToID("_StampColorIndices");
        private static readonly int ColorDecayId = Shader.PropertyToID("_ColorDecay");

        private static readonly int GlobalFieldBoundsMinId = Shader.PropertyToID("_FieldBoundsMin");
        private static readonly int GlobalFieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");

        private readonly IDisturbanceFieldSettings _settings;
        private readonly IGameDisplayConfiguration _displayConfig;
        private readonly ImpactEventBus _impactBus;
        private readonly List<PendingStamp> _pendingStamps = new();
        private readonly Vector4[] _batchCenters = new Vector4[MaxStampsPerBatch];
        private readonly float[] _batchRadii = new float[MaxStampsPerBatch];
        private readonly float[] _batchStrengths = new float[MaxStampsPerBatch];
        private readonly Vector4[] _batchDirections = new Vector4[MaxStampsPerBatch];
        private readonly float[] _batchColorIndices = new float[MaxStampsPerBatch];

        private DisturbanceFieldResources _resources;
        private DisturbanceFieldCoordinates _coords;
        private LerpStampScheduler _lerpScheduler;
        private Action<Vector3, float, float, Vector2, int> _emitInstantStamp;
        private float _diffusionTimer;
        private Vector2 _windTarget;
        private Vector2 _windCurrent;
        private float _stampAspect = 1f;

        internal DisturbanceFieldService(
            IDisturbanceFieldSettings settings,
            IGameDisplayConfiguration displayConfig,
            ImpactEventBus impactBus)
        {
            _settings = settings;
            _displayConfig = displayConfig;
            _impactBus = impactBus;

            // Built here (not Start()) so Stamp() is safe before Start(); _resources reports not-ready until then.
            _lerpScheduler = new LerpStampScheduler(_settings.MaxLerpStamps);
            _resources = new DisturbanceFieldResources(_settings);
        }

        internal RenderTexture FieldTexture => _resources.FieldTexture;

        internal Vector2 FieldBoundsMin => _coords.Bounds.min;
        internal Vector2 FieldBoundsSize => _coords.Bounds.size;

        void IStartable.Start()
        {
            _coords = new DisturbanceFieldCoordinates(_displayConfig, _settings.TexelsPerUnit);

            // UV space is normalised per-axis over a non-square field, so the stamp shaders correct
            // the vertical delta by this ratio to keep a radius circular in world space.
            _stampAspect = _coords.Bounds.height / _coords.Bounds.width;
            _emitInstantStamp = (pos, radius, strength, dir, palette) =>
                Stamp(pos, radius, strength, dir, paletteIndex: palette);

            _resources.Initialize(_coords.Width, _coords.Height);
            PushGlobalBounds();
        }

        void ITickable.Tick()
        {
            var dt = Time.deltaTime;
            _diffusionTimer += dt;

            var diffusionDue = _diffusionTimer >= _settings.DiffusionTickInterval;

            if (diffusionDue)
            {
                _lerpScheduler.Tick(_diffusionTimer, _emitInstantStamp);
            }

            var hasStamps = _pendingStamps.Count > 0;

            if (diffusionDue && hasStamps && _pendingStamps.Count <= MaxStampsPerBatch)
            {
                TickCombinedPass();
            }
            else
            {
                FlushPendingStamps();

                if (diffusionDue)
                {
                    TickDiffusion();
                }
            }
        }

        void IDisposable.Dispose()
        {
            _resources.Dispose();
        }

        /// <summary>Profile for a source, so callers needn't inject <see cref="IDisturbanceFieldSettings"/>.</summary>
        internal StampProfile GetProfile(StampSource source)
        {
            return _settings.GetProfile(source);
        }

        /// <summary>Stamps using the source's configured profile (radius/strength/duration). <paramref name="paletteIndex"/> tags the stamped region with a palette color (field A channel); -1 = none.</summary>
        internal void Stamp(StampSource source, Vector3 worldPosition, Vector2 direction, int paletteIndex = -1)
        {
            var profile = _settings.GetProfile(source);
            Stamp(worldPosition, profile.Radius, profile.Strength, direction, profile.Duration, paletteIndex);
        }

        /// <summary>Profile stamp with its radius scaled — e.g. a heavy balloon's deflect stamping wider than a light one's.</summary>
        internal void Stamp(
            StampSource source, Vector3 worldPosition, Vector2 direction, float radiusScale, int paletteIndex = -1)
        {
            var profile = _settings.GetProfile(source);
            Stamp(worldPosition, profile.Radius * radiusScale, profile.Strength, direction, profile.Duration, paletteIndex);
        }

        /// <summary>
        ///     A positive <paramref name="duration"/> ramps the stamp over time for a smooth shockwave
        ///     instead of a single-frame pop. <paramref name="reportImpact"/> false suppresses the bush
        ///     rustle — for constant emitters (rainbow/tough pulses) rather than one-off hits.
        /// </summary>
        internal void Stamp(
            Vector3 worldPosition, float radius, float strength, Vector2 direction, float duration = 0f,
            int paletteIndex = -1, bool reportImpact = true)
        {
            // Signed strength: > 0 repels (bumps R up), < 0 attracts (digs R down), 0 is a pure colour
            // tag with no force. Only the outward push of repulsion rustles bushes and shoves the wind.
            var colorOnly = strength == 0f;
            var repels = strength > 0f;

            if (repels && reportImpact)
            {
                _impactBus.Report(worldPosition, radius);
            }

            if (duration > 0f)
            {
                _lerpScheduler.Add(worldPosition, radius, strength, direction, duration, paletteIndex);
                return;
            }

            if (!_resources.IsReady)
            {
                return;
            }

            if (colorOnly)
            {
                // Nothing to write without a color to tag.
                if (paletteIndex < 0)
                {
                    return;
                }
            }
            else if (Mathf.Abs(strength) < _settings.MinStampStrength)
            {
                return;
            }

            var uv = _coords.WorldToUV(worldPosition);
            var radiusUV = _coords.WorldRadiusToUV(radius);

            if (repels && direction.sqrMagnitude > 0.001f)
            {
                _windTarget = -direction;
            }

            _pendingStamps.Add(new PendingStamp
            {
                CenterUV = uv,
                RadiusUV = radiusUV,
                Strength = strength,
                Direction = direction,
                // Encoded so 0 always reads "no color" in the shader; indices quantize into 16 slots.
                EncodedPaletteIndex = paletteIndex >= 0 ? (paletteIndex + 1f) / PaletteIndexSlots : 0f
            });
        }

        private void FlushPendingStamps()
        {
            if (_pendingStamps.Count == 0)
            {
                return;
            }

            var offset = 0;
            while (offset < _pendingStamps.Count)
            {
                var count = Mathf.Min(MaxStampsPerBatch, _pendingStamps.Count - offset);
                FillBatchArrays(offset, count);

                UploadStampArrays(_resources.StampMaterial, count);
                _resources.BlitAndSwap(_resources.StampMaterial);

                offset += count;
            }

            _pendingStamps.Clear();
        }

        private void TickDiffusion()
        {
            if (_resources.DiffusionMaterial == null)
            {
                return;
            }

            SetDiffusionUniforms();
            _resources.SetStampsEnabled(_resources.DiffusionMaterial, false);

            _resources.BlitAndSwap(_resources.DiffusionMaterial);
            _diffusionTimer = 0f;
        }

        private void TickCombinedPass()
        {
            if (_resources.DiffusionMaterial == null)
            {
                return;
            }

            SetDiffusionUniforms();

            var count = _pendingStamps.Count;
            FillBatchArrays(0, count);

            UploadStampArrays(_resources.DiffusionMaterial, count);
            _resources.SetStampsEnabled(_resources.DiffusionMaterial, true);

            _resources.BlitAndSwap(_resources.DiffusionMaterial);
            _diffusionTimer = 0f;
            _pendingStamps.Clear();
        }

        private void UploadStampArrays(Material material, int count)
        {
            material.SetInt(StampCountId, count);
            material.SetVectorArray(StampCentersId, _batchCenters);
            material.SetFloatArray(StampRadiiId, _batchRadii);
            material.SetFloatArray(StampStrengthsId, _batchStrengths);
            material.SetVectorArray(StampDirectionsId, _batchDirections);
            material.SetFloatArray(StampColorIndicesId, _batchColorIndices);
            material.SetFloat(DisplaceAmountId, _settings.DisplaceAmount);
            material.SetFloat(StampAspectId, _stampAspect);
        }

        private void SetDiffusionUniforms()
        {
            var material = _resources.DiffusionMaterial;

            material.SetFloat(DiffusionRateId, _settings.DiffusionRate);
            material.SetFloat(ReformSpeedId, _settings.ReformSpeed);
            material.SetFloat(DeltaTimeId, _diffusionTimer);

            _windCurrent = Vector2.Lerp(_windCurrent, _windTarget, _settings.WindSmoothing * _diffusionTimer);
            _windTarget = Vector2.Lerp(_windTarget, Vector2.zero, _settings.WindDecay * _diffusionTimer);

            material.SetVector(WindDirId, new Vector4(_windCurrent.x, _windCurrent.y, 0f, 0f));
            material.SetFloat(WindSpeedId, _settings.WindSpeed);
            material.SetFloat(PressureStrId, _settings.PressureStrength);
            material.SetFloat(DisplaceDecayId, _settings.DisplaceDecay);
            material.SetFloat(ColorDecayId, _settings.ColorTagDecay);
        }

        private void FillBatchArrays(int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var s = _pendingStamps[offset + i];
                _batchCenters[i] = new Vector4(s.CenterUV.x, s.CenterUV.y, 0f, 0f);
                _batchRadii[i] = s.RadiusUV;
                _batchStrengths[i] = s.Strength;
                _batchDirections[i] = new Vector4(s.Direction.x, s.Direction.y, 0f, 0f);
                _batchColorIndices[i] = s.EncodedPaletteIndex;
            }
        }

        private void PushGlobalBounds()
        {
            var bounds = _coords.Bounds;
            Shader.SetGlobalVector(GlobalFieldBoundsMinId, new Vector4(bounds.xMin, bounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(GlobalFieldBoundsSizeId, new Vector4(bounds.width, bounds.height, 0f, 0f));
        }

        private struct PendingStamp
        {
            public Vector2 CenterUV;
            public float RadiusUV;
            public float Strength;
            public Vector2 Direction;
            public float EncodedPaletteIndex;
        }
    }
}

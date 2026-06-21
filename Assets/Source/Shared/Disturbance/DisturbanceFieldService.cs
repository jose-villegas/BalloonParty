using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;

namespace BalloonParty.Shared.Disturbance
{
    /// <summary>
    /// Owns a single screen-space RT pair (density + displacement) that any
    /// game system can stamp into. Runs one diffusion blit per tick to reform
    /// the field toward equilibrium. The cloud shader (and any future effect)
    /// samples from <see cref="FieldTexture"/>.
    /// </summary>
    internal class DisturbanceFieldService : IStartable, ITickable, IDisposable
    {
        private const int MaxStampsPerBatch = 32;

        private static readonly int DiffusionRateId = Shader.PropertyToID("_DiffusionRate");
        private static readonly int ReformSpeedId = Shader.PropertyToID("_ReformSpeed");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int WindDirId = Shader.PropertyToID("_WindDir");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int PressureStrId = Shader.PropertyToID("_PressureStr");
        private static readonly int DisplaceDecayId = Shader.PropertyToID("_DisplaceDecay");
        private static readonly int DisplaceAmountId = Shader.PropertyToID("_DisplaceAmount");
        private static readonly int StampCountId = Shader.PropertyToID("_StampCount");
        private static readonly int StampCentersId = Shader.PropertyToID("_StampCenters");
        private static readonly int StampRadiiId = Shader.PropertyToID("_StampRadii");
        private static readonly int StampStrengthsId = Shader.PropertyToID("_StampStrengths");
        private static readonly int StampDirectionsId = Shader.PropertyToID("_StampDirections");

        private static readonly int GlobalDisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");
        private static readonly int GlobalFieldBoundsMinId = Shader.PropertyToID("_FieldBoundsMin");
        private static readonly int GlobalFieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");

        private static LocalKeyword _stampsOnKeyword;
        private static bool _stampsKeywordResolved;

        private readonly IDisturbanceFieldSettings _settings;
        private readonly IGameDisplayConfiguration _displayConfig;
        private readonly ImpactEventBus _impactBus;
        private readonly List<PendingStamp> _pendingStamps = new();
        private readonly Vector4[] _batchCenters = new Vector4[MaxStampsPerBatch];
        private readonly float[] _batchRadii = new float[MaxStampsPerBatch];
        private readonly float[] _batchStrengths = new float[MaxStampsPerBatch];
        private readonly Vector4[] _batchDirections = new Vector4[MaxStampsPerBatch];

        private DisturbanceFieldCoordinates _coords;
        private LerpStampScheduler _lerpScheduler;
        private Action<Vector3, float, float, Vector2> _emitInstantStamp;
        private RenderTexture _fieldA;
        private RenderTexture _fieldB;
        private bool _readFromA = true;
        private Material _diffusionMaterial;
        private Material _batchedStampMaterial;
        private float _diffusionTimer;
        private Vector2 _windTarget;
        private Vector2 _windCurrent;

        internal DisturbanceFieldService(
            IDisturbanceFieldSettings settings,
            IGameDisplayConfiguration displayConfig,
            ImpactEventBus impactBus)
        {
            _settings = settings;
            _displayConfig = displayConfig;
            _impactBus = impactBus;
        }

        internal RenderTexture FieldTexture => _readFromA ? _fieldA : _fieldB;
        private RenderTexture FieldWrite => _readFromA ? _fieldB : _fieldA;

        internal Vector2 FieldBoundsMin => _coords.Bounds.min;
        internal Vector2 FieldBoundsSize => _coords.Bounds.size;

        void IStartable.Start()
        {
            _coords = new DisturbanceFieldCoordinates(_displayConfig, _settings.TexelsPerUnit);
            _lerpScheduler = new LerpStampScheduler(_settings.MaxLerpStamps);
            _emitInstantStamp = (pos, radius, strength, dir) => Stamp(pos, radius, strength, dir);

            CreateFieldRTs();
            EnsureDiffusionMaterial();
            EnsureBatchedStampMaterial();
            PushGlobalBounds();
            PushGlobalTexture();
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
            ReleaseRT(ref _fieldA);
            ReleaseRT(ref _fieldB);
            DestroyMaterial(ref _diffusionMaterial);
            DestroyMaterial(ref _batchedStampMaterial);
        }

        /// <summary>
        /// Exposes the stamp profile for a source so callers can read
        /// profile values (e.g. Radius for step calculations) without
        /// injecting <see cref="IDisturbanceFieldSettings"/> themselves.
        /// </summary>
        internal StampProfile GetProfile(StampSource source)
        {
            return _settings.GetProfile(source);
        }

        /// <summary>
        /// Stamps using a pre-configured profile. Reads radius, strength, and
        /// duration from <see cref="IDisturbanceFieldSettings"/> so callers don't
        /// need to inject the settings themselves.
        /// </summary>
        internal void Stamp(StampSource source, Vector3 worldPosition, Vector2 direction)
        {
            var profile = _settings.GetProfile(source);
            Stamp(worldPosition, profile.Radius, profile.Strength, direction, profile.Duration);
        }

        /// <summary>
        /// Stamps a disturbance at the given world position. The field will
        /// show a density hole that reforms over time via diffusion.
        /// When <paramref name="duration"/> is greater than zero the stamp
        /// ramps up over that many seconds, spreading the effect across
        /// multiple frames for a smooth shockwave instead of a single-frame pop.
        /// </summary>
        internal void Stamp(Vector3 worldPosition, float radius, float strength, Vector2 direction, float duration = 0f)
        {
            _impactBus.Report(worldPosition, radius);

            if (duration > 0f)
            {
                _lerpScheduler.Add(worldPosition, radius, strength, direction, duration);
                return;
            }

            if (_fieldA == null || _batchedStampMaterial == null)
            {
                return;
            }

            if (strength < _settings.MinStampStrength)
            {
                return;
            }

            var uv = _coords.WorldToUV(worldPosition);
            var radiusUV = _coords.WorldRadiusToUV(radius);

            if (direction.sqrMagnitude > 0.001f)
            {
                _windTarget = -direction;
            }

            _pendingStamps.Add(new PendingStamp
            {
                CenterUV = uv,
                RadiusUV = radiusUV,
                Strength = strength,
                Direction = direction
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

                UploadStampArrays(_batchedStampMaterial, count);
                BlitAndSwap(_batchedStampMaterial);

                offset += count;
            }

            _pendingStamps.Clear();
        }

        private void CreateFieldRTs()
        {
            _fieldA = CreateRT(_coords.Width, _coords.Height);
            _fieldB = CreateRT(_coords.Width, _coords.Height);
            ClearToEquilibrium(_fieldA);
            ClearToEquilibrium(_fieldB);
            _readFromA = true;
        }

        private void TickDiffusion()
        {
            if (_diffusionMaterial == null)
            {
                return;
            }

            SetDiffusionUniforms();
            SetStampsKeyword(_diffusionMaterial, false);

            BlitAndSwap(_diffusionMaterial);
            _diffusionTimer = 0f;
        }

        private void TickCombinedPass()
        {
            if (_diffusionMaterial == null)
            {
                return;
            }

            SetDiffusionUniforms();

            var count = _pendingStamps.Count;
            FillBatchArrays(0, count);

            UploadStampArrays(_diffusionMaterial, count);
            SetStampsKeyword(_diffusionMaterial, true);

            BlitAndSwap(_diffusionMaterial);
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
            material.SetFloat(DisplaceAmountId, _settings.DisplaceAmount);
        }

        private void BlitAndSwap(Material material)
        {
            Graphics.Blit(FieldTexture, FieldWrite, material);
            _readFromA = !_readFromA;
            PushGlobalTexture();
        }

        private void SetDiffusionUniforms()
        {
            _diffusionMaterial.SetFloat(DiffusionRateId, _settings.DiffusionRate);
            _diffusionMaterial.SetFloat(ReformSpeedId, _settings.ReformSpeed);
            _diffusionMaterial.SetFloat(DeltaTimeId, _diffusionTimer);

            _windCurrent = Vector2.Lerp(_windCurrent, _windTarget, _settings.WindSmoothing * _diffusionTimer);
            _windTarget = Vector2.Lerp(_windTarget, Vector2.zero, _settings.WindDecay * _diffusionTimer);

            _diffusionMaterial.SetVector(WindDirId, new Vector4(_windCurrent.x, _windCurrent.y, 0f, 0f));
            _diffusionMaterial.SetFloat(WindSpeedId, _settings.WindSpeed);
            _diffusionMaterial.SetFloat(PressureStrId, _settings.PressureStrength);
            _diffusionMaterial.SetFloat(DisplaceDecayId, _settings.DisplaceDecay);
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
            }
        }

        private static void SetStampsKeyword(Material mat, bool enabled)
        {
            if (!_stampsKeywordResolved)
            {
                _stampsOnKeyword = new LocalKeyword(mat.shader, "_STAMPS_ON");
                _stampsKeywordResolved = true;
            }

            if (enabled)
            {
                mat.EnableKeyword(in _stampsOnKeyword);
            }
            else
            {
                mat.DisableKeyword(in _stampsOnKeyword);
            }
        }

        private void PushGlobalBounds()
        {
            var bounds = _coords.Bounds;
            Shader.SetGlobalVector(GlobalFieldBoundsMinId, new Vector4(bounds.xMin, bounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(GlobalFieldBoundsSizeId, new Vector4(bounds.width, bounds.height, 0f, 0f));
        }

        private void PushGlobalTexture()
        {
            var tex = FieldTexture;
            if (tex != null)
            {
                Shader.SetGlobalTexture(GlobalDisturbanceTexId, tex);
            }
        }

        private void EnsureDiffusionMaterial()
        {
            if (_diffusionMaterial != null)
            {
                return;
            }

            var shader = _settings.DiffusionShader;
            if (shader == null)
            {
                Debug.LogError("DisturbanceFieldService: DiffusionShader not assigned on IDisturbanceFieldSettings.");
                return;
            }

            _diffusionMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void EnsureBatchedStampMaterial()
        {
            if (_batchedStampMaterial != null)
            {
                return;
            }

            var shader = _settings.StampBatchedShader;
            if (shader == null)
            {
                Debug.LogError("DisturbanceFieldService: StampBatchedShader not assigned on IDisturbanceFieldSettings.");
                return;
            }

            _batchedStampMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private static RenderTexture CreateRT(int width, int height)
        {
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf
                : RenderTextureFormat.ARGB32;

            var rt = new RenderTexture(width, height, 0, format)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        private static void ClearToEquilibrium(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, new Color(1f, 0.5f, 0.5f, 1f));
            RenderTexture.active = prev;
        }

        private static void ReleaseRT(ref RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                rt = null;
            }
        }

        private static void DestroyMaterial(ref Material mat)
        {
            if (mat != null)
            {
                UnityEngine.Object.Destroy(mat);
                mat = null;
            }
        }

        private struct PendingStamp
        {
            public Vector2 CenterUV;
            public float RadiusUV;
            public float Strength;
            public Vector2 Direction;
        }
    }
}

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
        private readonly List<LerpStamp> _activeStamps = new();
        private readonly List<PendingStamp> _pendingStamps = new();
        private readonly Vector4[] _batchCenters = new Vector4[MaxStampsPerBatch];
        private readonly float[] _batchRadii = new float[MaxStampsPerBatch];
        private readonly float[] _batchStrengths = new float[MaxStampsPerBatch];
        private readonly Vector4[] _batchDirections = new Vector4[MaxStampsPerBatch];

        private RenderTexture _fieldA;
        private RenderTexture _fieldB;
        private bool _readFromA = true;
        private Material _diffusionMaterial;
        private Material _batchedStampMaterial;
        private float _diffusionTimer;
        private Vector2 _windTarget;
        private Vector2 _windCurrent;
        private Rect _fieldBounds;
        private int _fieldWidth;
        private int _fieldHeight;

        internal DisturbanceFieldService(
            IDisturbanceFieldSettings settings,
            IGameDisplayConfiguration displayConfig)
        {
            _settings = settings;
            _displayConfig = displayConfig;
        }

        internal RenderTexture FieldTexture => _readFromA ? _fieldA : _fieldB;
        private RenderTexture FieldWrite => _readFromA ? _fieldB : _fieldA;

        internal Vector2 FieldBoundsMin => _fieldBounds.min;
        internal Vector2 FieldBoundsSize => _fieldBounds.size;

        void IStartable.Start()
        {
            ComputeFieldBounds();
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
                TickLerpStamps(_diffusionTimer);
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
        /// Stamps a disturbance at the given world position. The field will
        /// show a density hole that reforms over time via diffusion.
        /// When <paramref name="duration"/> is greater than zero the stamp
        /// ramps up over that many seconds, spreading the effect across
        /// multiple frames for a smooth shockwave instead of a single-frame pop.
        /// </summary>
        internal void Stamp(Vector3 worldPosition, float radius, float strength, Vector2 direction, float duration = 0f)
        {
            if (duration > 0f)
            {
                if (_activeStamps.Count >= _settings.MaxLerpStamps)
                {
                    _activeStamps.RemoveAt(0);
                }

                _activeStamps.Add(new LerpStamp
                {
                    Position = worldPosition,
                    Radius = radius,
                    Strength = strength,
                    Direction = direction,
                    Duration = duration,
                    Elapsed = 0f,
                    LastT = 0f
                });
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

            var uv = WorldToFieldUV(worldPosition);
            var radiusUV = WorldRadiusToFieldUV(radius);

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

        internal Vector2 WorldToFieldUV(Vector3 worldPos)
        {
            return new Vector2(
                (worldPos.x - _fieldBounds.xMin) / _fieldBounds.width,
                (worldPos.y - _fieldBounds.yMin) / _fieldBounds.height);
        }

        private float WorldRadiusToFieldUV(float worldRadius)
        {
            var avgSize = (_fieldBounds.width + _fieldBounds.height) * 0.5f;
            return avgSize > 0.001f ? worldRadius / avgSize : 0.1f;
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

                _batchedStampMaterial.SetInt(StampCountId, count);
                _batchedStampMaterial.SetVectorArray(StampCentersId, _batchCenters);
                _batchedStampMaterial.SetFloatArray(StampRadiiId, _batchRadii);
                _batchedStampMaterial.SetFloatArray(StampStrengthsId, _batchStrengths);
                _batchedStampMaterial.SetVectorArray(StampDirectionsId, _batchDirections);
                _batchedStampMaterial.SetFloat(DisplaceAmountId, _settings.DisplaceAmount);

                Graphics.Blit(FieldTexture, FieldWrite, _batchedStampMaterial);
                _readFromA = !_readFromA;
                PushGlobalTexture();

                offset += count;
            }

            _pendingStamps.Clear();
        }

        private void ComputeFieldBounds()
        {
            var orthoSize = _displayConfig.GetOrthogonalSize();
            var aspect = (float)Screen.width / Screen.height;
            var worldHeight = orthoSize * 2f;
            var worldWidth = worldHeight * aspect;

            _fieldBounds = new Rect(-worldWidth * 0.5f, -worldHeight * 0.5f, worldWidth, worldHeight);

            _fieldWidth = Mathf.Max(4, Mathf.RoundToInt(worldWidth * _settings.TexelsPerUnit));
            _fieldHeight = Mathf.Max(4, Mathf.RoundToInt(worldHeight * _settings.TexelsPerUnit));
        }

        private void CreateFieldRTs()
        {
            _fieldA = CreateRT(_fieldWidth, _fieldHeight);
            _fieldB = CreateRT(_fieldWidth, _fieldHeight);
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

            Graphics.Blit(FieldTexture, FieldWrite, _diffusionMaterial);
            _readFromA = !_readFromA;
            _diffusionTimer = 0f;
            PushGlobalTexture();
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

            _diffusionMaterial.SetInt(StampCountId, count);
            _diffusionMaterial.SetVectorArray(StampCentersId, _batchCenters);
            _diffusionMaterial.SetFloatArray(StampRadiiId, _batchRadii);
            _diffusionMaterial.SetFloatArray(StampStrengthsId, _batchStrengths);
            _diffusionMaterial.SetVectorArray(StampDirectionsId, _batchDirections);
            _diffusionMaterial.SetFloat(DisplaceAmountId, _settings.DisplaceAmount);
            SetStampsKeyword(_diffusionMaterial, true);

            Graphics.Blit(FieldTexture, FieldWrite, _diffusionMaterial);
            _readFromA = !_readFromA;
            _diffusionTimer = 0f;
            _pendingStamps.Clear();
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
            Shader.SetGlobalVector(GlobalFieldBoundsMinId, new Vector4(_fieldBounds.xMin, _fieldBounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(GlobalFieldBoundsSizeId, new Vector4(_fieldBounds.width, _fieldBounds.height, 0f, 0f));
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

        private void TickLerpStamps(float dt)
        {
            for (var i = _activeStamps.Count - 1; i >= 0; i--)
            {
                var s = _activeStamps[i];
                s.Elapsed += dt;
                var t = Mathf.Clamp01(s.Elapsed / s.Duration);

                var delta = t - s.LastT;
                s.LastT = t;
                _activeStamps[i] = s;

                if (delta > 0.0001f)
                {
                    var radiusNow = Mathf.Lerp(s.Radius * 0.3f, s.Radius, t);
                    Stamp(s.Position, radiusNow, s.Strength * delta, s.Direction);
                }

                if (t >= 1f)
                {
                    _activeStamps.RemoveAt(i);
                }
            }
        }

        private struct PendingStamp
        {
            public Vector2 CenterUV;
            public float RadiusUV;
            public float Strength;
            public Vector2 Direction;
        }

        private struct LerpStamp
        {
            public Vector3 Position;
            public float Radius;
            public float Strength;
            public Vector2 Direction;
            public float Duration;
            public float Elapsed;
            public float LastT;
        }
    }
}

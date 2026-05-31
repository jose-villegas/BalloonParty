using System;
using BalloonParty.Configuration;
using UnityEngine;
using VContainer;
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
        private static readonly int DiffusionRateId = Shader.PropertyToID("_DiffusionRate");
        private static readonly int ReformSpeedId = Shader.PropertyToID("_ReformSpeed");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int WindDirId = Shader.PropertyToID("_WindDir");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int PressureStrId = Shader.PropertyToID("_PressureStr");
        private static readonly int DisplaceDecayId = Shader.PropertyToID("_DisplaceDecay");
        private static readonly int StampCenterId = Shader.PropertyToID("_StampCenter");
        private static readonly int StampRadiusId = Shader.PropertyToID("_StampRadius");
        private static readonly int StampStrengthId = Shader.PropertyToID("_StampStrength");
        private static readonly int StampDirectionId = Shader.PropertyToID("_StampDirection");
        private static readonly int DisplaceAmountId = Shader.PropertyToID("_DisplaceAmount");

        [Inject] private readonly DisturbanceFieldSettings _settings;
        [Inject] private readonly GameDisplayConfiguration _displayConfig;

        private RenderTexture _fieldA;
        private RenderTexture _fieldB;
        private bool _readFromA = true;
        private Material _diffusionMaterial;
        private Material _stampMaterial;
        private float _diffusionTimer;
        private Vector2 _windTarget;
        private Vector2 _windCurrent;

        private Rect _fieldBounds;
        private int _fieldWidth;
        private int _fieldHeight;

        internal RenderTexture FieldTexture => _readFromA ? _fieldA : _fieldB;
        private RenderTexture FieldWrite => _readFromA ? _fieldB : _fieldA;

        internal Vector2 FieldBoundsMin => _fieldBounds.min;
        internal Vector2 FieldBoundsSize => _fieldBounds.size;

        void IStartable.Start()
        {
            ComputeFieldBounds();
            CreateFieldRTs();
            EnsureDiffusionMaterial();
            EnsureStampMaterial();
        }

        void ITickable.Tick()
        {
            TickDiffusion(Time.deltaTime);
        }

        void IDisposable.Dispose()
        {
            ReleaseRT(ref _fieldA);
            ReleaseRT(ref _fieldB);
            DestroyMaterial(ref _diffusionMaterial);
            DestroyMaterial(ref _stampMaterial);
        }

        /// <summary>
        /// Stamps a disturbance at the given world position. The field will
        /// show a density hole that reforms over time via diffusion.
        /// </summary>
        internal void Stamp(Vector3 worldPosition, float radius, float strength, Vector2 direction)
        {
            if (_fieldA == null || _stampMaterial == null)
            {
                return;
            }

            var uv = WorldToFieldUV(worldPosition);
            var radiusUV = WorldRadiusToFieldUV(radius);

            _stampMaterial.SetVector(StampCenterId, new Vector4(uv.x, uv.y, 0f, 0f));
            _stampMaterial.SetFloat(StampRadiusId, radiusUV);
            _stampMaterial.SetFloat(StampStrengthId, strength);
            _stampMaterial.SetVector(StampDirectionId, new Vector4(direction.x, direction.y, 0f, 0f));
            _stampMaterial.SetFloat(DisplaceAmountId, _settings.DisplaceAmount);

            if (direction.sqrMagnitude > 0.001f)
            {
                _windTarget = -direction;
            }

            Graphics.Blit(FieldTexture, FieldWrite, _stampMaterial);
            _readFromA = !_readFromA;
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

        private void ComputeFieldBounds()
        {
            var halfWidth = _displayConfig.ReferenceWorldWidth * 0.5f;
            var halfHeight = _displayConfig.ReferenceWorldHeight * 0.5f;
            _fieldBounds = new Rect(-halfWidth, -halfHeight, halfWidth * 2f, halfHeight * 2f);

            _fieldWidth = Mathf.Max(4, Mathf.RoundToInt(_displayConfig.ReferenceWorldWidth * _settings.TexelsPerUnit));
            _fieldHeight = Mathf.Max(4, Mathf.RoundToInt(_displayConfig.ReferenceWorldHeight * _settings.TexelsPerUnit));
        }

        private void CreateFieldRTs()
        {
            _fieldA = CreateRT(_fieldWidth, _fieldHeight);
            _fieldB = CreateRT(_fieldWidth, _fieldHeight);
            ClearToEquilibrium(_fieldA);
            ClearToEquilibrium(_fieldB);
            _readFromA = true;
        }

        private void TickDiffusion(float dt)
        {
            _diffusionTimer += dt;
            if (_diffusionTimer < _settings.DiffusionTickInterval)
            {
                return;
            }

            if (_diffusionMaterial == null)
            {
                return;
            }

            _diffusionMaterial.SetFloat(DiffusionRateId, _settings.DiffusionRate);
            _diffusionMaterial.SetFloat(ReformSpeedId, _settings.ReformSpeed);
            _diffusionMaterial.SetFloat(DeltaTimeId, _diffusionTimer);

            _windCurrent = Vector2.Lerp(_windCurrent, _windTarget, _settings.WindSmoothing * _diffusionTimer);
            _windTarget = Vector2.Lerp(_windTarget, Vector2.zero, _settings.WindDecay * _diffusionTimer);

            _diffusionMaterial.SetVector(WindDirId, new Vector4(_windCurrent.x, _windCurrent.y, 0f, 0f));
            _diffusionMaterial.SetFloat(WindSpeedId, _settings.WindSpeed);
            _diffusionMaterial.SetFloat(PressureStrId, _settings.PressureStrength);
            _diffusionMaterial.SetFloat(DisplaceDecayId, _settings.DisplaceDecay);

            Graphics.Blit(FieldTexture, FieldWrite, _diffusionMaterial);
            _readFromA = !_readFromA;
            _diffusionTimer = 0f;
        }

        private void EnsureDiffusionMaterial()
        {
            if (_diffusionMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Hidden/BalloonParty/Grid/PuffCloudDiffusion");
            if (shader == null)
            {
                Debug.LogError("DisturbanceFieldService: PuffCloudDiffusion shader not found.");
                return;
            }

            _diffusionMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void EnsureStampMaterial()
        {
            if (_stampMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Hidden/BalloonParty/Grid/PuffCloudStamp");
            if (shader == null)
            {
                Debug.LogError("DisturbanceFieldService: PuffCloudStamp shader not found.");
                return;
            }

            _stampMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private static RenderTexture CreateRT(int width, int height)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf)
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
    }
}


using BalloonParty.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonParty.Shared.Disturbance
{
    /// <summary>
    ///     Owns the disturbance field's GPU resources: the ping-pong RenderTexture pair (read/write
    ///     swapped on each blit), the diffusion + batched-stamp materials, and the <c>_STAMPS_ON</c>
    ///     keyword. After every blit it publishes the current read texture as the global
    ///     <c>_DisturbanceTex</c> so sampling shaders pick it up. The service drives the simulation and
    ///     sets per-pass uniforms; this just holds, blits, and flips the buffers.
    /// </summary>
    internal class DisturbanceFieldResources
    {
        private static readonly int GlobalDisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");

        private readonly IDisturbanceFieldSettings _settings;

        private RenderTexture _fieldA;
        private RenderTexture _fieldB;
        private bool _readFromA = true;
        private Material _diffusionMaterial;
        private Material _batchedStampMaterial;
        private LocalKeyword _stampsOnKeyword;
        private bool _stampsKeywordResolved;

        public DisturbanceFieldResources(IDisturbanceFieldSettings settings)
        {
            _settings = settings;
        }

        public RenderTexture FieldTexture => _readFromA ? _fieldA : _fieldB;
        public Material DiffusionMaterial => _diffusionMaterial;
        public Material StampMaterial => _batchedStampMaterial;
        public bool IsReady => _fieldA != null && _batchedStampMaterial != null;

        private RenderTexture FieldWrite => _readFromA ? _fieldB : _fieldA;

        public void Initialize(int width, int height)
        {
            _fieldA = CreateRT(width, height);
            _fieldB = CreateRT(width, height);
            ClearToEquilibrium(_fieldA);
            ClearToEquilibrium(_fieldB);
            _readFromA = true;

            _diffusionMaterial = CreateMaterial(_settings.DiffusionShader, "DiffusionShader");
            _batchedStampMaterial = CreateMaterial(_settings.StampBatchedShader, "StampBatchedShader");

            PushGlobalTexture();
        }

        /// <summary>Blits read→write through <paramref name="material"/>, swaps, and republishes the texture.</summary>
        public void BlitAndSwap(Material material)
        {
            Graphics.Blit(FieldTexture, FieldWrite, material);
            _readFromA = !_readFromA;
            PushGlobalTexture();
        }

        public void SetStampsEnabled(Material material, bool enabled)
        {
            if (!_stampsKeywordResolved)
            {
                _stampsOnKeyword = new LocalKeyword(material.shader, "_STAMPS_ON");
                _stampsKeywordResolved = true;
            }

            if (enabled)
            {
                material.EnableKeyword(in _stampsOnKeyword);
            }
            else
            {
                material.DisableKeyword(in _stampsOnKeyword);
            }
        }

        public void Dispose()
        {
            ReleaseRT(ref _fieldA);
            ReleaseRT(ref _fieldB);
            DestroyMaterial(ref _diffusionMaterial);
            DestroyMaterial(ref _batchedStampMaterial);
        }

        private void PushGlobalTexture()
        {
            var tex = FieldTexture;
            if (tex != null)
            {
                Shader.SetGlobalTexture(GlobalDisturbanceTexId, tex);
            }
        }

        private static Material CreateMaterial(Shader shader, string settingName)
        {
            if (shader == null)
            {
                Debug.LogError($"DisturbanceFieldResources: {settingName} not assigned on IDisturbanceFieldSettings.");
                return null;
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
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
                Object.Destroy(rt);
                rt = null;
            }
        }

        private static void DestroyMaterial(ref Material mat)
        {
            if (mat != null)
            {
                Object.Destroy(mat);
                mat = null;
            }
        }
    }
}

using BalloonParty.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Shared.Disturbance
{
    /// <summary>Holds the disturbance field's GPU resources and blit-and-swap, separate from <see cref="DisturbanceFieldService"/>'s simulation logic.</summary>
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

        public RenderTexture FieldTexture => _readFromA ? _fieldA : _fieldB;
        public Material DiffusionMaterial => _diffusionMaterial;
        public Material StampMaterial => _batchedStampMaterial;
        public bool IsReady => _fieldA != null && _batchedStampMaterial != null;

        private RenderTexture FieldWrite => _readFromA ? _fieldB : _fieldA;

        public DisturbanceFieldResources(IDisturbanceFieldSettings settings)
        {
            _settings = settings;
        }

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
            // A = 0: no palette-color tag anywhere yet (see the diffusion shader's channel map).
            GL.Clear(false, true, new Color(1f, 0.5f, 0.5f, 0f));
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

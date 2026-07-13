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
        private static readonly int GlobalDisturbanceColorTexId = Shader.PropertyToID("_DisturbanceColorTex");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int ColorLerpSpeedId = Shader.PropertyToID("_ColorLerpSpeed");

        private readonly IDisturbanceFieldSettings _settings;

        private RenderTexture _fieldA;
        private RenderTexture _fieldB;
        private bool _readFromA = true;
        private RenderTexture _colorA;
        private RenderTexture _colorB;
        private bool _colorReadFromA = true;
        private Material _diffusionMaterial;
        private Material _batchedStampMaterial;
        private Material _colorLerpMaterial;
        private LocalKeyword _stampsOnKeyword;
        private bool _stampsKeywordResolved;

        public RenderTexture FieldTexture => _readFromA ? _fieldA : _fieldB;
        public Material DiffusionMaterial => _diffusionMaterial;
        public Material StampMaterial => _batchedStampMaterial;
        public bool IsReady => _fieldA != null && _batchedStampMaterial != null;

        private RenderTexture FieldWrite => _readFromA ? _fieldB : _fieldA;
        private RenderTexture ColorRead => _colorReadFromA ? _colorA : _colorB;
        private RenderTexture ColorWrite => _colorReadFromA ? _colorB : _colorA;

        public DisturbanceFieldResources(IDisturbanceFieldSettings settings)
        {
            _settings = settings;
        }

        public void Initialize(int width, int height)
        {
            _fieldA = CreateRT(width, height, FieldFormat);
            _fieldB = CreateRT(width, height, FieldFormat);
            ClearToEquilibrium(_fieldA);
            ClearToEquilibrium(_fieldB);
            _readFromA = true;

            _diffusionMaterial = CreateMaterial(_settings.DiffusionShader, "DiffusionShader");
            _batchedStampMaterial = CreateMaterial(_settings.StampBatchedShader, "StampBatchedShader");
            _colorLerpMaterial = CreateMaterial(_settings.ColorLerpShader, "ColorLerpShader");

            _colorA = CreateRT(width, height, RenderTextureFormat.ARGB32);
            _colorB = CreateRT(width, height, RenderTextureFormat.ARGB32);
            // RGB = colour (irrelevant until tagged), A = 0 strength (untagged = no tint).
            ClearColor(_colorA, new Color(1f, 1f, 1f, 0f));
            ClearColor(_colorB, new Color(1f, 1f, 1f, 0f));
            _colorReadFromA = true;

            PushGlobalTexture();
            PushGlobalColorTexture();
        }

        // Eases the smoothed colour layer toward the field's current tag colour, so an overwrite crossfades
        // instead of snapping. Runs on the diffusion cadence; dt is that tick's elapsed time.
        public void TickColorLerp(float dt)
        {
            if (_colorLerpMaterial == null)
            {
                return;
            }

            _colorLerpMaterial.SetFloat(DeltaTimeId, dt);
            _colorLerpMaterial.SetFloat(ColorLerpSpeedId, _settings.ColorLerpSpeed);
            Graphics.Blit(ColorRead, ColorWrite, _colorLerpMaterial);
            _colorReadFromA = !_colorReadFromA;
            PushGlobalColorTexture();
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
            ReleaseRT(ref _colorA);
            ReleaseRT(ref _colorB);
            DestroyMaterial(ref _diffusionMaterial);
            DestroyMaterial(ref _batchedStampMaterial);
            DestroyMaterial(ref _colorLerpMaterial);
        }

        private void PushGlobalTexture()
        {
            var tex = FieldTexture;
            if (tex != null)
            {
                Shader.SetGlobalTexture(GlobalDisturbanceTexId, tex);
            }
        }

        private void PushGlobalColorTexture()
        {
            var tex = ColorRead;
            if (tex != null)
            {
                Shader.SetGlobalTexture(GlobalDisturbanceColorTexId, tex);
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

        // The field packs signed density + displacement, so it wants half-float; the colour layer is LDR
        // (palette colour + 0..1 strength), so plain 8-bit ARGB32 is enough and half the memory.
        private static RenderTextureFormat FieldFormat =>
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf
                : RenderTextureFormat.ARGB32;

        private static RenderTexture CreateRT(int width, int height, RenderTextureFormat format)
        {
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
            // R = 0.5: signed density rest — above 0.5 repels, below attracts. GB = 0.5: zero
            // displacement. A = 0: no palette-color tag anywhere yet (see the diffusion shader's map).
            GL.Clear(false, true, new Color(0.5f, 0.5f, 0.5f, 0f));
            RenderTexture.active = prev;
        }

        private static void ClearColor(RenderTexture rt, Color color)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, color);
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

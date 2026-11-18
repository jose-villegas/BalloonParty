using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Shared.SceneLight
{
    /// <summary>Holds the scene-light field's GPU resources (two ping-pong RTs, the smaller colour map, and the
    /// fill/accumulate/gradient materials) and the blit-and-swap, separate from
    /// <see cref="SceneLightFieldService"/>'s per-tick logic — mirrors DisturbanceFieldResources.</summary>
    internal class SceneLightFieldResources
    {
        private const int FieldPass = 0;
        private const int ColorPass = 1;

        private static readonly int GlobalSceneLightTexId = Shader.PropertyToID("_SceneLightTex");
        private static readonly int GlobalSceneLightColorTexId = Shader.PropertyToID("_SceneLightColorTex");
        private static readonly int TexelSizeId = Shader.PropertyToID("_FieldTexelSize");

        private RenderTexture _fieldA;
        private RenderTexture _fieldB;
        private RenderTexture _color;
        private bool _readFromA = true;
        private Material _fillMaterial;
        private Material _accumulateMaterial;
        private Material _gradientMaterial;

        public RenderTexture FieldTexture => _readFromA ? _fieldA : _fieldB;

        /// <summary>What colour the local light is at each point: the tagged lights' blended palette colour times
        /// alpha, alpha the share of the local light that is tagged. Bound globally as <c>_SceneLightColorTex</c>.</summary>
        public RenderTexture ColorTexture => _color;
        public Material AccumulateMaterial => _accumulateMaterial;
        public Material GradientMaterial => _gradientMaterial;
        public bool IsReady =>
            _fieldA != null && _fillMaterial != null && _accumulateMaterial != null && _gradientMaterial != null;

        private RenderTexture FieldWrite => _readFromA ? _fieldB : _fieldA;

        public void Initialize(
            int width, int height, int colorDownscale, Shader fill, Shader accumulate, Shader gradient)
        {
            _fieldA = CreateRT(width, height, FieldFormat);
            _fieldB = CreateRT(width, height, FieldFormat);
            ClearToRest(_fieldA);
            ClearToRest(_fieldB);
            _readFromA = true;

            // Colours and shares, all within 0..1, read through a bilinear filter: 8 bits are enough, and a
            // colour changes slowly enough across a light that it needs fewer texels than its brightness.
            var downscale = Mathf.Max(1, colorDownscale);
            _color = CreateRT(
                Mathf.Max(1, width / downscale), Mathf.Max(1, height / downscale), RenderTextureFormat.ARGB32);
            Clear(_color, Color.clear);

            // Prefer the serialized references (from the settings SO) so a device build keeps these Hidden
            // shaders; fall back to Shader.Find in-editor if a slot's unassigned (Find-only would be
            // stripped on device — see the settings' Shaders tooltip).
            _fillMaterial = LoadMaterial(fill, "Hidden/BalloonParty/SceneLightFieldFill");
            _accumulateMaterial = LoadMaterial(accumulate, "Hidden/BalloonParty/SceneLightAccumulate");
            _gradientMaterial = LoadMaterial(gradient, "Hidden/BalloonParty/SceneLightGradient");

            // Texel size is fixed by the RT dimensions, so push it once — the gradient pass samples
            // neighbours a texel away to build grad(R).
            if (_gradientMaterial != null)
            {
                _gradientMaterial.SetVector(TexelSizeId, new Vector4(1f / width, 1f / height, 0f, 0f));
            }

            PushGlobalTexture();
        }

        /// <summary>Pass 1 — clears the read buffer to the (constant, purely local) rest state
        /// (R = 0, GB = 0.5 neutral, A = 0) and swaps it in. The field carries no ambient; consumers read
        /// the ambient direction/magnitude from the globals. Source is ignored.</summary>
        public void Fill()
        {
            BlitAndSwap(_fillMaterial);
        }

        /// <summary>Pass 3 — recomputes GB from grad(R). No per-tick uniforms (texel size is fixed at
        /// init, blend thresholds default in the shader).</summary>
        public void Gradient()
        {
            BlitAndSwap(_gradientMaterial);
        }

        // The pass is named: Blit with none draws every pass of the material into the target, and the
        // accumulate material's second pass is the colour map's.
        public void BlitAndSwap(Material material)
        {
            Graphics.Blit(FieldTexture, FieldWrite, material, FieldPass);
            _readFromA = !_readFromA;
            PushGlobalTexture();
        }

        /// <summary>Renders the colour map from the lights the accumulate material was just given.</summary>
        public void BlitColor()
        {
            Graphics.Blit(null, _color, _accumulateMaterial, ColorPass);
        }

        public void Dispose()
        {
            // A shader that still samples the map after this (the Editor's scene view) reads no colour.
            Shader.SetGlobalTexture(GlobalSceneLightColorTexId, Texture2D.blackTexture);
            ReleaseRT(ref _fieldA);
            ReleaseRT(ref _fieldB);
            ReleaseRT(ref _color);
            DestroyMaterial(ref _fillMaterial);
            DestroyMaterial(ref _accumulateMaterial);
            DestroyMaterial(ref _gradientMaterial);
        }

        private void PushGlobalTexture()
        {
            var tex = FieldTexture;
            if (tex != null)
            {
                Shader.SetGlobalTexture(GlobalSceneLightTexId, tex);
            }

            if (_color != null)
            {
                Shader.SetGlobalTexture(GlobalSceneLightColorTexId, _color);
            }
        }

        private static Material LoadMaterial(Shader serialized, string shaderName)
        {
            var shader = serialized != null ? serialized : Shader.Find(shaderName);
            if (shader == null)
            {
                Log.Error("SceneLightField", $"shader '{shaderName}' not found (assign it on the " +
                               "Scene Light Field Settings asset for device builds) — the light field will not run.");
                return null;
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        // Directions live in G/B as signed 2D vectors (0.5-biased), so the field wants half-float
        // precision where supported — the same reasoning as the disturbance field's displacement channels.
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

        // R = 1: neutral magnitude. GB = 0.5: a zero (un-biased) direction until the first fill
        // reads the owner. A = 0: no palette colour anywhere (consumers fall back to _SceneLightColor).
        private static void ClearToRest(RenderTexture rt)
        {
            Clear(rt, new Color(1f, 0.5f, 0.5f, 0f));
        }

        private static void Clear(RenderTexture rt, Color color)
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

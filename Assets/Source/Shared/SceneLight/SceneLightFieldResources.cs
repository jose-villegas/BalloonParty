using UnityEngine;

namespace BalloonParty.Shared.SceneLight
{
    /// <summary>Holds the scene-light field's GPU resources (two ping-pong RTs + the fill/accumulate/gradient
    /// materials) and the blit-and-swap, separate from <see cref="SceneLightFieldService"/>'s per-tick
    /// logic — mirrors DisturbanceFieldResources.</summary>
    internal class SceneLightFieldResources
    {
        private static readonly int GlobalSceneLightTexId = Shader.PropertyToID("_SceneLightTex");
        private static readonly int TexelSizeId = Shader.PropertyToID("_FieldTexelSize");

        private RenderTexture _fieldA;
        private RenderTexture _fieldB;
        private bool _readFromA = true;
        private Material _fillMaterial;
        private Material _accumulateMaterial;
        private Material _gradientMaterial;

        public RenderTexture FieldTexture => _readFromA ? _fieldA : _fieldB;
        public Material AccumulateMaterial => _accumulateMaterial;
        public Material GradientMaterial => _gradientMaterial;
        public bool IsReady =>
            _fieldA != null && _fillMaterial != null && _accumulateMaterial != null && _gradientMaterial != null;

        private RenderTexture FieldWrite => _readFromA ? _fieldB : _fieldA;

        public void Initialize(int width, int height)
        {
            _fieldA = CreateRT(width, height, FieldFormat);
            _fieldB = CreateRT(width, height, FieldFormat);
            ClearToRest(_fieldA);
            ClearToRest(_fieldB);
            _readFromA = true;

            // Editor fallback wiring — the plain-C# service can't carry a serialized Shader reference the
            // way the disturbance config SO does, so each pass shader is resolved by name. Hidden shaders
            // reached only via Shader.Find are stripped from device builds unless they're Always-Included;
            // that registration is deferred for all three (see the field service's README) — Phase C is
            // editor-verified only.
            _fillMaterial = LoadMaterial("Hidden/BalloonParty/SceneLightFieldFill");
            _accumulateMaterial = LoadMaterial("Hidden/BalloonParty/SceneLightAccumulate");
            _gradientMaterial = LoadMaterial("Hidden/BalloonParty/SceneLightGradient");

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

        public void BlitAndSwap(Material material)
        {
            Graphics.Blit(FieldTexture, FieldWrite, material);
            _readFromA = !_readFromA;
            PushGlobalTexture();
        }

        public void Dispose()
        {
            ReleaseRT(ref _fieldA);
            ReleaseRT(ref _fieldB);
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
        }

        private static Material LoadMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"SceneLightFieldResources: shader '{shaderName}' not found — the light field will not run.");
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

        private static void ClearToRest(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            // R = 1: neutral magnitude. GB = 0.5: a zero (un-biased) direction until the first fill
            // reads the owner. A = 0: no palette colour anywhere (consumers fall back to _SceneLightColor).
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

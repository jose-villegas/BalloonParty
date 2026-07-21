using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Scenario
{
    /// <summary>Holds the painting field's GPU resources: ping-pong RTs and blit-and-swap,
    /// separate from <see cref="PaintingFieldService"/>'s stamp/decay logic.</summary>
    internal sealed class PaintingFieldResources
    {
        private static readonly int GlobalPaintingTexId = Shader.PropertyToID("_PaintingTex");

        private RenderTexture _fieldA;
        private RenderTexture _fieldB;
        private bool _readFromA = true;
        private Material _stampMaterial;
        private Material _decayMaterial;

        public RenderTexture ReadTexture => _readFromA ? _fieldA : _fieldB;
        public Material StampMaterial => _stampMaterial;
        public Material DecayMaterial => _decayMaterial;
        public bool IsReady => _fieldA != null && _stampMaterial != null;

        private RenderTexture WriteTexture => _readFromA ? _fieldB : _fieldA;

        public void Initialize(IPaintingFieldSettings settings, int width, int height)
        {
            _fieldA = CreateRT(width, height);
            _fieldB = CreateRT(width, height);
            ClearToEmpty(_fieldA);
            ClearToEmpty(_fieldB);
            _readFromA = true;

            _stampMaterial = CreateMaterial(settings.StampShader, "StampShader");
            _decayMaterial = CreateMaterial(settings.DecayShader, "DecayShader");

            PushGlobalTexture();
        }

        public void BlitAndSwap(Material material)
        {
            Graphics.Blit(ReadTexture, WriteTexture, material);
            _readFromA = !_readFromA;
            PushGlobalTexture();
        }

        public void Dispose()
        {
            ReleaseRT(ref _fieldA);
            ReleaseRT(ref _fieldB);
            DestroyMaterial(ref _stampMaterial);
            DestroyMaterial(ref _decayMaterial);
        }

        private void PushGlobalTexture()
        {
            var tex = ReadTexture;
            if (tex != null)
            {
                Shader.SetGlobalTexture(GlobalPaintingTexId, tex);
            }
        }

        private static Material CreateMaterial(Shader shader, string settingName)
        {
            if (shader == null)
            {
                Log.Error("PaintingField", $"{settingName} not assigned on IPaintingFieldSettings.");
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
                name = "PaintingField",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        // Empty = (0, 0, 0, 0): R=0 means untagged, G=0 means invisible.
        private static void ClearToEmpty(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
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

using System;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The light the water's surface focuses on the bottom, published as the global <c>_WaterCausticTex</c> that
    ///     <c>BalloonParty/Scenario/Water</c> lays under its surface. Each frame one blit of
    ///     <c>Hidden/BalloonParty/Scenario/WaterCaustics</c> draws one tile of the moving caustic net into a small
    ///     square target that wraps, with mips, so the water can tile it at any scale and read it softer where the
    ///     water is deep. Only while water shows.
    /// </summary>
    internal sealed class WaterCausticBuffer : IDisposable
    {
        private static readonly int WaterCausticTexId = Shader.PropertyToID("_WaterCausticTex");

        private readonly Material _material;
        private readonly RenderTexture _target;

        internal WaterCausticBuffer(Material causticMaterial, int resolution)
        {
            // A runtime copy, so a tweak in play mode never writes into the material asset.
            _material = causticMaterial != null
                ? new Material(causticMaterial) { hideFlags = HideFlags.HideAndDontSave }
                : null;

            if (_material == null)
            {
                Log.Warn("Water", "no caustics (no caustic material): the water's bottom shows no light.");
                Rest();
                return;
            }

            var size = Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 32, 1024));
            _target = new RenderTexture(size, size, 0, RenderTextureFormat.R8)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Repeat,
                useMipMap = true,
                autoGenerateMips = true
            };
            _target.Create();
            Rest();
        }

        public void Dispose()
        {
            Rest();
            if (_target != null)
            {
                _target.Release();
                Object.Destroy(_target);
            }

            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        internal void Tick()
        {
            if (_material == null)
            {
                return;
            }

            Graphics.Blit(null, _target, _material);
            Shader.SetGlobalTexture(WaterCausticTexId, _target);
        }

        // Black is no light: the bottom shows its own colour only.
        internal void Rest()
        {
            Shader.SetGlobalTexture(WaterCausticTexId, Texture2D.blackTexture);
        }
    }
}

using System;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The light the water surface focuses on the bottom, published as the global <c>_WaterCausticTex</c> that
    ///     <c>BalloonParty/Scenario/Water</c> reads under its surface. Each frame one blit of
    ///     <c>Hidden/BalloonParty/Scenario/WaterCaustics</c> draws it from the same surface the water shows, into a
    ///     small target over the camera's rect (the disturbance field's), so reading it back enlarged is already
    ///     soft. Only while water shows.
    /// </summary>
    internal sealed class WaterCausticBuffer : IDisposable
    {
        private static readonly int WaterCausticTexId = Shader.PropertyToID("_WaterCausticTex");
        private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");

        // The surface's tuning, authored once on the water material and mirrored onto the caustic pass, so the
        // light on the bottom always matches the waves drawn over it.
        private static readonly int[] SurfaceFloatIds =
        {
            Shader.PropertyToID("_WaveSize"),
            Shader.PropertyToID("_WaveSpeed"),
            Shader.PropertyToID("_WaveHeight"),
            Shader.PropertyToID("_RippleRings"),
            Shader.PropertyToID("_RippleSpeed"),
            Shader.PropertyToID("_RippleHeight"),
        };

        private readonly Material _material;
        private readonly RenderTexture _target;

        internal WaterCausticBuffer(Material causticMaterial, Material waterMaterial, Vector2 viewSize,
            float texelsPerUnit)
        {
            // A runtime copy, so play mode never writes the mirrored surface values into the material asset.
            _material = causticMaterial != null && waterMaterial != null
                ? new Material(causticMaterial) { hideFlags = HideFlags.HideAndDontSave }
                : null;

            if (_material == null)
            {
                Log.Warn("Water", "no caustics (no caustic material): the water's bottom shows no light.");
                Rest();
                return;
            }

            MirrorSurface(waterMaterial);
            var density = Mathf.Max(0.5f, texelsPerUnit);
            _target = new RenderTexture(
                Mathf.Max(4, Mathf.RoundToInt(viewSize.x * density)),
                Mathf.Max(4, Mathf.RoundToInt(viewSize.y * density)),
                0, RenderTextureFormat.R8)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
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

        private void MirrorSurface(Material water)
        {
            if (water.HasProperty(NoiseTexId))
            {
                _material.SetTexture(NoiseTexId, water.GetTexture(NoiseTexId));
            }

            foreach (var id in SurfaceFloatIds)
            {
                if (water.HasProperty(id))
                {
                    _material.SetFloat(id, water.GetFloat(id));
                }
            }
        }
    }
}

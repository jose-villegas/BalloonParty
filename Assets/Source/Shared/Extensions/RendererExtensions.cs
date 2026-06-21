using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class RendererExtensions
    {
        /// <summary>
        ///     Sets a single MaterialPropertyBlock float and applies it — the
        ///     GetPropertyBlock → Set → SetPropertyBlock dance in one call. For multiple
        ///     properties, do the dance inline (a multi-set helper would need a per-call
        ///     closure, which allocates in per-frame paths).
        /// </summary>
        internal static void SetFloatAndApply(
            this Renderer renderer, MaterialPropertyBlock block, int id, float value)
        {
            renderer.GetPropertyBlock(block);
            block.SetFloat(id, value);
            renderer.SetPropertyBlock(block);
        }

        internal static void SetVectorAndApply(
            this Renderer renderer, MaterialPropertyBlock block, int id, Vector4 value)
        {
            renderer.GetPropertyBlock(block);
            block.SetVector(id, value);
            renderer.SetPropertyBlock(block);
        }
    }
}

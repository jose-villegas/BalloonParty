using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class RendererExtensions
    {
        /// <summary>Get→Set→SetPropertyBlock in one call; for multiple properties do it inline to avoid a per-call closure alloc.</summary>
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

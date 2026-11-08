using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>Builds a runtime quad GameObject wired to a material, Unity layer, and sorting
    /// layer/order — the backdrop quads (the GI overlay, the sky-scatter backdrop, the ground floor).
    /// <see cref="MeshRendererBuilder" /> does the wiring; see it for why the layer is required.</summary>
    internal static class QuadRendererBuilder
    {
        internal static GameObject Build(
            string name, Material material, int layer, string sortingLayerName, int sortingOrder,
            QuadPivot pivot = QuadPivot.Center)
        {
            return MeshRendererBuilder.Build(
                name, MeshHelper.CreateQuad(pivot), material, layer, sortingLayerName, sortingOrder).gameObject;
        }
    }
}

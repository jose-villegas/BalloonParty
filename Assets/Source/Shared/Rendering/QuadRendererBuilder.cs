using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>Builds a runtime quad GameObject wired to a material, Unity layer, and sorting
    /// layer/order — the shared recipe behind every runtime-built backdrop quad (the GI overlay,
    /// the sky-scatter backdrop). Requires the Unity layer as an explicit argument rather than
    /// leaving it at the <c>new GameObject</c> default of 0 (Default) — that default sits outside
    /// <c>NavigationCameraReveal</c>'s Launch culling mask, so an omitted layer makes the quad
    /// silently disappear on the Launch begin-screen instead of failing loudly.</summary>
    internal static class QuadRendererBuilder
    {
        internal static GameObject Build(
            string name, Material material, int layer, string sortingLayerName, int sortingOrder,
            QuadPivot pivot = QuadPivot.Center)
        {
            var quad = new GameObject(name)
            {
                layer = layer
            };

            var filter = quad.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshHelper.CreateQuad(pivot);

            var renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            return quad;
        }
    }
}

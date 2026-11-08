using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>Builds a runtime GameObject that draws one mesh with one material on an explicit Unity layer and
    /// sorting layer/order, with no shadows — the shared recipe behind every runtime-built backdrop renderer
    /// (<see cref="QuadRendererBuilder" />'s quads, the ground's grass blades). The layer is a required argument:
    /// the <c>new GameObject</c> default of 0 (Default) sits outside <c>NavigationCameraReveal</c>'s Launch culling
    /// mask, so an omitted layer would make the renderer silently vanish on the Launch begin-screen.</summary>
    internal static class MeshRendererBuilder
    {
        internal static MeshRenderer Build(
            string name, Mesh mesh, Material material, int layer, string sortingLayerName, int sortingOrder)
        {
            var host = new GameObject(name)
            {
                layer = layer
            };

            host.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}

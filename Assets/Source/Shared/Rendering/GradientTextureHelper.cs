using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    internal static class GradientTextureHelper
    {
        private const int DefaultResolution = 64;

        internal static Texture2D Bake(Gradient gradient, int resolution = DefaultResolution)
        {
            var tex = new Texture2D(resolution, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[resolution];
            for (var i = 0; i < resolution; i++)
            {
                pixels[i] = gradient.Evaluate(i / (float)(resolution - 1));
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}


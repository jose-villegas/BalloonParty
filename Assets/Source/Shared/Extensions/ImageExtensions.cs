using DG.Tweening;
using UnityEngine.UI;

namespace BalloonParty.Shared.Extensions
{
    internal static class ImageExtensions
    {
        /// <summary>True when the Image has a material override (not the built-in default UI material).
        /// Accessing <c>Image.material</c> auto-instances on first write; reading is safe.</summary>
        internal static bool HasCustomMaterial(this Image image)
        {
            return image.material != null && image.material != image.defaultMaterial;
        }

        /// <summary>Fades the effective alpha — material <c>_Color.a</c> when a custom material is
        /// assigned, otherwise the graphic's vertex color. Callers must ensure the material is already
        /// instanced per-object (e.g. via a prefab material override) to avoid cross-bleed.</summary>
        internal static Tween DOFadeAuto(this Image image, float target, float duration)
        {
            if (image.HasCustomMaterial())
            {
                return image.material.DOFade(target, duration);
            }

            return image.DOFade(target, duration);
        }

        /// <summary>Snaps the effective alpha without tweening (same routing as <see cref="DOFadeAuto"/>).</summary>
        internal static void SetAlphaAuto(this Image image, float alpha)
        {
            if (image.HasCustomMaterial())
            {
                var c = image.material.color;
                c.a = alpha;
                image.material.color = c;
            }
            else
            {
                var c = image.color;
                c.a = alpha;
                image.color = c;
            }
        }

        /// <summary>Reads the effective alpha (material or graphic, matching the write path).</summary>
        internal static float GetAlphaAuto(this Image image)
        {
            return image.HasCustomMaterial() ? image.material.color.a : image.color.a;
        }
    }
}

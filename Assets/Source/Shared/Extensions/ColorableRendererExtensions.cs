using System;
using UniRx;
using BalloonParty.Shared.Rendering;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    public static class ColorableRendererExtensions
    {
        /// <summary>Tints every non-null renderer in the set; a null array is a no-op.</summary>
        public static void SetColor(this ColorableRenderer[] renderers, Color color)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.SetColor(color);
                }
            }
        }

        public static IDisposable BindColor(
            this ColorableRenderer[] renderers,
            IReadOnlyReactiveProperty<string> colorName,
            Func<string, Color> resolve)
        {
            return colorName.Subscribe(Apply);

            void Apply(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return;
                }

                renderers.SetColor(resolve(name));
            }
        }
    }
}

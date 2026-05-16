using System;
using UniRx;
using BalloonParty.Shared.Rendering;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    public static class ColorableRendererExtensions
    {
        public static IDisposable BindColor(
            this ColorableRenderer[] renderers,
            IReadOnlyReactiveProperty<string> colorName,
            Func<string, Color> resolve)
        {
            return colorName.Subscribe(name =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return;
                }

                var color = resolve(name);

                foreach (var renderer in renderers)
                {
                    renderer.SetColor(color);
                }
            });
        }
    }
}

using System;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using UnityEngine;

namespace BalloonParty.Item.Laser
{
    /// <summary>
    ///     Animator-driven laser effect that tints its wired renderers to the colour it receives — the
    ///     host balloon's colour, which is white for a rainbow host (the palette resolves the wildcard id
    ///     to white). Renderers the beam always keeps at a fixed colour simply stay out of the array.
    /// </summary>
    public class LaserView : AnimatorEffectView
    {
        [SerializeField] private ColorableRenderer[] _colorableRenderers;

        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            ApplyColor(tint);
            base.Play(position, tint, onComplete);
        }

        // Separate from Play so the editor preview can recolour without driving the animation lifecycle.
        public void ApplyColor(Color color)
        {
            if (_colorableRenderers == null)
            {
                return;
            }

            foreach (var colorable in _colorableRenderers)
            {
                if (colorable != null)
                {
                    colorable.SetColor(color);
                }
            }
        }
    }
}

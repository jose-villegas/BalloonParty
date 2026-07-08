using System;
using System.Collections.Generic;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using UnityEngine;

namespace BalloonParty.Item.Laser
{
    /// <summary>
    ///     Animator-driven laser effect that tints its wired renderers to the colour it receives — the
    ///     host balloon's colour, which is white for a rainbow host (the palette resolves the wildcard id
    ///     to white). Renderers the beam always keeps at a fixed colour simply stay out of the array. A
    ///     rainbow holder instead lerps the wired renderers through a colour set over the anim (see
    ///     <see cref="SetCycleColors" />).
    /// </summary>
    public class LaserView : AnimatorEffectView
    {
        [SerializeField] private ColorableRenderer[] _colorableRenderers;

        private IReadOnlyList<Color> _cycleColors;
        private float _cycles;

        protected override void Update()
        {
            base.Update();

            if (_cycleColors != null && _cycleColors.Count > 0)
            {
                ApplyColor(ColorCycle.Sample(_cycleColors, Mathf.Repeat(AnimationProgress * _cycles, 1f)));
            }
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            _cycleColors = null;
        }

        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            ApplyColor(tint);
            base.Play(position, tint, onComplete);
        }

        // Rainbow holder: lerp the wired renderers through these colours, cycles loops over the anim.
        public void SetCycleColors(IReadOnlyList<Color> colors, float cycles)
        {
            _cycleColors = colors;
            _cycles = Mathf.Max(0f, cycles);
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

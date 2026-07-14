using System;
using System.Collections.Generic;
using BalloonParty.Shared.Extensions;
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

        private ColorCycleState _cycleState;

        protected override void Update()
        {
            base.Update();

            if (_cycleState.HasColors)
            {
                ApplyColor(_cycleState.Sample(AnimationProgress));
            }
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            _cycleState.Clear();
        }

        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            ApplyColor(tint);
            base.Play(position, tint, onComplete);
        }

        // Rainbow holder: lerp the wired renderers through these colours, cycles loops over the anim.
        public void SetCycleColors(IReadOnlyList<Color> colors, float cycles)
        {
            _cycleState.Set(colors, cycles);
        }

        // Separate from Play so the editor preview can recolour without driving the animation lifecycle.
        public void ApplyColor(Color color)
        {
            _colorableRenderers.SetColor(color);
        }
    }
}

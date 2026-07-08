using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleColorableRenderer : ColorableRenderer<ParticleSystem>
    {
        public override void SetColor(Color color)
        {
            var main = Renderer.main;
            main.startColor = WithIntensity(color);

            // Force-restart so all particles immediately reflect the new color.
            Renderer.Clear();

            if (Renderer.isPlaying)
            {
                Renderer.Play();
            }
        }

        protected override float ReadAlpha()
        {
            return Renderer.main.startColor.color.a;
        }
    }
}

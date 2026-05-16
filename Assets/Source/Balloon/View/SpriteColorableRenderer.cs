using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteColorableRenderer : ColorableRenderer<SpriteRenderer>
    {
        public override void SetColor(Color color)
        {
            Renderer.color = color;
        }
    }
}

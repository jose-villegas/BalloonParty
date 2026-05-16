using BalloonParty.Shared.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.Score
{
    [RequireComponent(typeof(Graphic))]
    public class GraphicColorableRenderer : ColorableRenderer<Graphic>
    {
        public override void SetColor(Color color)
        {
            Renderer.color = color;
        }
    }
}


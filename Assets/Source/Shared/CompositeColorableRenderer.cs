using UnityEngine;

namespace BalloonParty.Shared
{
    public class CompositeColorableRenderer : ColorableRenderer
    {
        [SerializeField] private ColorableRenderer[] _renderers;

        public override void SetColor(Color color)
        {
            foreach (var colorable in _renderers)
            {
                colorable.SetColor(color);
            }
        }
    }
}

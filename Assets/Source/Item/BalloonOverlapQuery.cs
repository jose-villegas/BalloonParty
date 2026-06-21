using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Shared physics setup for area-of-effect items (bomb, laser): a balloon-layer
    ///     <see cref="ContactFilter2D" /> for the cast/overlap call, and resolution of a hit collider
    ///     to a live balloon model — skipping recycled views and the popped balloon itself.
    /// </summary>
    internal class BalloonOverlapQuery
    {
        private static readonly int BalloonsLayer = LayerMask.GetMask("Balloons");

        public ContactFilter2D Filter { get; }

        public BalloonOverlapQuery()
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(BalloonsLayer);
            filter.useTriggers = true;
            Filter = filter;
        }

        public bool TryResolveBalloon(
            Collider2D collider,
            IBalloonModel exclude,
            out BalloonView view,
            out IBalloonModel model)
        {
            view = collider.GetComponentInParent<BalloonView>();
            model = view != null ? view.Model : null;

            if (model == null || ReferenceEquals(model, exclude))
            {
                view = null;
                model = null;
                return false;
            }

            return true;
        }
    }
}

#region

using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.Balloon.Items
{
    public class ItemViewScope : LifetimeScope
    {
        protected override LifetimeScope FindParent()
        {
            if (transform.parent != null)
            {
                var parentScope = transform.parent.GetComponentInParent<LifetimeScope>();
                if (parentScope != null)
                {
                    return parentScope;
                }
            }

            return FindFirstObjectByType<GameLifetimeScope>();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ItemDisplayService>();

            builder.RegisterBuildCallback(resolver =>
            {
                foreach (var view in GetComponentsInChildren<ItemVisualView>(true))
                {
                    resolver.Inject(view);
                }
            });
        }
    }
}

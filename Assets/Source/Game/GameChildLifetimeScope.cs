#region

using VContainer.Unity;

#endregion

namespace BalloonParty.Game
{
    public abstract class GameChildLifetimeScope : LifetimeScope
    {
        protected override LifetimeScope FindParent()
        {
            return FindFirstObjectByType<GameLifetimeScope>();
        }
    }
}

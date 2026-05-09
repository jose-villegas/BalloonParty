using VContainer.Unity;

namespace BalloonParty.Game
{
    public abstract class GameChildLifetimeScope : LifetimeScope
    {
        protected override LifetimeScope FindParent() => FindFirstObjectByType<GameLifetimeScope>();
    }
}

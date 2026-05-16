using BalloonParty.Balloon.View;
using BalloonParty.Shared.Pool;
using VContainer;

namespace BalloonParty.Balloon.Spawner
{
    internal class BalloonPoolChannel : InjectingPoolChannel<BalloonView>
    {
        public BalloonPoolChannel(IObjectResolver resolver, BalloonView prefab)
            : base(resolver, prefab)
        {
        }
    }
}

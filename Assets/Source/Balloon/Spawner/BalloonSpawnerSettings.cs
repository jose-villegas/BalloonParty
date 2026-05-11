using VContainer.Unity;

namespace BalloonParty.Balloon.Spawner
{
    public class BalloonSpawnerSettings
    {
        public readonly LifetimeScope BalloonScopePrefab;

        public BalloonSpawnerSettings(LifetimeScope balloonScopePrefab)
        {
            BalloonScopePrefab = balloonScopePrefab;
        }
    }
}

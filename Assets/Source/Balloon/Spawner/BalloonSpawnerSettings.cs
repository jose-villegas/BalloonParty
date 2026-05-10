#region

using VContainer.Unity;

#endregion

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



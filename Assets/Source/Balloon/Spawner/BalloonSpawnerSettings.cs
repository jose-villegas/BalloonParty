using UnityEngine;

namespace BalloonParty.Balloon.Spawner
{
    public class BalloonSpawnerSettings
    {
        public readonly GameObject BalloonPrefab;

        public BalloonSpawnerSettings(GameObject balloonPrefab)
        {
            BalloonPrefab = balloonPrefab;
        }
    }
}

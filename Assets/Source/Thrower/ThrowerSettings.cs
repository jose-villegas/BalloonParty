using UnityEngine;

namespace BalloonParty.Thrower
{
    public class ThrowerSettings
    {
        public readonly GameObject ProjectilePrefab;

        public ThrowerSettings(GameObject projectilePrefab)
        {
            ProjectilePrefab = projectilePrefab;
        }
    }
}
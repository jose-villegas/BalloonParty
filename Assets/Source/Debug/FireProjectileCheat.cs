#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using BalloonParty.Thrower;

namespace BalloonParty.Debug
{
    public class FireProjectileCheat : ICheat
    {
        private readonly ThrowerController _thrower;

        public string Name => "Fire Projectile";
        public string Section => "Thrower";
        public IReadOnlyList<string> Tags => new[] { "projectile", "thrower" };

        public FireProjectileCheat(ThrowerController thrower)
        {
            _thrower = thrower;
        }

        public void Execute() => _thrower.FireImmediate();
    }
}
#endif


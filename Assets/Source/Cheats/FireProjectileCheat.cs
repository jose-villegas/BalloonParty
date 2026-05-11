#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Cheats
{
    public class FireProjectileCheat : ICheat
    {
        private IWriteableProjectileModel _activeProjectile;

        public string Name => "Fire Projectile";
        public string Section => "Thrower";
        public IReadOnlyList<string> Tags => new[] { "projectile", "thrower" };

        public FireProjectileCheat(ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            loadedSubscriber.Subscribe(msg => _activeProjectile = (IWriteableProjectileModel)msg.Model);
        }

        public void Execute()
        {
            if (_activeProjectile == null || _activeProjectile.IsFree)
            {
                return;
            }

            _activeProjectile.IsFree = true;
        }
    }
}
#endif

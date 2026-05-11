using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ProjectileLoadedMessage
    {
        public IProjectileModel Model { get; }

        public ProjectileLoadedMessage(IProjectileModel model)
        {
            Model = model;
        }
    }
}

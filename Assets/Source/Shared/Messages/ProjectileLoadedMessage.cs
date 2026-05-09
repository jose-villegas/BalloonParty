using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ProjectileLoadedMessage
    {
        public ProjectileModel Model { get; }

        public ProjectileLoadedMessage(ProjectileModel model)
        {
            Model = model;
        }
    }
}
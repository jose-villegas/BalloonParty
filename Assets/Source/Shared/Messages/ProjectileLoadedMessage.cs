#region

using BalloonParty.Projectile.Model;

#endregion

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

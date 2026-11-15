using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ProjectileLoadedMessage
    {
        public IProjectileModel Model { get; }

        /// <summary>
        ///     The ball took over a Snipe lance carried from the last level: it was already given, so nothing
        ///     announces it again.
        /// </summary>
        public bool LanceCarried { get; }

        public ProjectileLoadedMessage(IProjectileModel model, bool lanceCarried = false)
        {
            Model = model;
            LanceCarried = lanceCarried;
        }
    }
}

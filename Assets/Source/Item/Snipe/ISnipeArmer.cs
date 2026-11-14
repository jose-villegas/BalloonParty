using BalloonParty.Projectile.Model;

namespace BalloonParty.Item.Snipe
{
    /// <summary>Arms a shot as a Snipe lance without a pickup: what <see cref="SnipeItemHandler" /> does for one.</summary>
    internal interface ISnipeArmer
    {
        /// <summary>
        ///     Arms <paramref name="shot" /> as a plain (not rainbow) lance with the Snipe's speed buff, and opens its
        ///     pencil line: at the muzzle when it fires, if it's still in the thrower.
        /// </summary>
        void Arm(IWriteableProjectileModel shot);
    }
}

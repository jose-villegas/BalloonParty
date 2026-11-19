using BalloonParty.Projectile.Model;
using UnityEngine;

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

        /// <summary>
        ///     Arms <paramref name="shot" />, a ball in flight that is not piercing, as a plain lance earned there
        ///     with no pickup behind it (the meadow's mushroom chain): as <see cref="Arm" /> arms one, its pencil
        ///     line starting at <paramref name="at" />.
        /// </summary>
        /// <remarks>
        ///     Never asked for a ball already piercing: two pierces cannot overlap, and a lance bounces off no
        ///     mushroom, so the chain's rule (<c>MushroomChainRule.OnBounce</c>) only ever arms a ball that is
        ///     not one.
        /// </remarks>
        /// <param name="at">Where the lance was earned, which is where its straight run begins.</param>
        void ArmAt(IWriteableProjectileModel shot, Vector3 at);
    }
}

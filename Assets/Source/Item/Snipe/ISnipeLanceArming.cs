using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Item.Snipe
{
    /// <summary>What <see cref="SnipeItemHandler" /> tells the pencil line when it arms a lance.</summary>
    internal interface ISnipeLanceArming
    {
        /// <summary>A fresh lance armed on <paramref name="shot" />, wherever it is right now.</summary>
        void OnLanceArmed(IProjectileModel shot);

        /// <summary>
        ///     A fresh lance armed on <paramref name="shot" /> in flight at a point the arming knows better than
        ///     the ball's last drawn position does: a mushroom bounce arms from inside the ball's fixed step,
        ///     before the ball has been moved to where that step leaves it. Its run starts at
        ///     <paramref name="point" />.
        /// </summary>
        void OnLanceArmedAt(IProjectileModel shot, Vector3 point);

        /// <summary>A banked charge re-armed <paramref name="shot" /> at the discharge wall it just left.</summary>
        void OnLanceRearmed(IProjectileModel shot, Vector3 wall);
    }
}

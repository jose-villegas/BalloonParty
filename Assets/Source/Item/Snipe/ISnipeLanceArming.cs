using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Item.Snipe
{
    /// <summary>What <see cref="SnipeItemHandler" /> tells the pencil line when it arms a lance.</summary>
    internal interface ISnipeLanceArming
    {
        /// <summary>A fresh lance armed on <paramref name="shot" />, wherever it is right now.</summary>
        void OnLanceArmed(IProjectileModel shot);

        /// <summary>A banked charge re-armed <paramref name="shot" /> at the discharge wall it just left.</summary>
        void OnLanceRearmed(IProjectileModel shot, Vector3 wall);
    }
}

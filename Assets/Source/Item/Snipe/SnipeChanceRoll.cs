using System;
using BalloonParty.Game.Upgrades;
using BalloonParty.Projectile.Model;
using Random = UnityEngine.Random;

namespace BalloonParty.Item.Snipe
{
    /// <summary>What the thrower asks of each ball it loads: roll the Sharpshooter card's chance at a Snipe lance.</summary>
    internal interface ISnipeChanceRoll
    {
        /// <summary>
        ///     Rolls <see cref="IWeaponStats.SnipeChance" /> for <paramref name="loaded" />, the ball just loaded, and
        ///     on a hit arms it as a Snipe lance. True when it was armed.
        /// </summary>
        bool TryArm(IWriteableProjectileModel loaded);
    }

    /// <summary>
    ///     The Sharpshooter card's roll, once per ball the thrower loads. A hit arms the ball through the Snipe's own
    ///     path (<see cref="ISnipeArmer" />), so it is a Snipe lance in every way, pencil line included.
    /// </summary>
    internal sealed class SnipeChanceRoll : ISnipeChanceRoll
    {
        private readonly IWeaponStats _weapon;
        private readonly ISnipeArmer _armer;

        // Tests replace the draw to pin the roll.
        internal Func<float> Random01 { get; set; } = () => Random.value;

        internal SnipeChanceRoll(IWeaponStats weapon, ISnipeArmer armer)
        {
            _weapon = weapon;
            _armer = armer;
        }

        // Random.value can return 1, so a chance of 1 has to hit without the draw.
        public bool TryArm(IWriteableProjectileModel loaded)
        {
            var chance = _weapon.SnipeChance;
            if (loaded == null || chance <= 0f || loaded.IsPiercing.Value)
            {
                return false;
            }

            if (chance < 1f && Random01() >= chance)
            {
                return false;
            }

            _armer.Arm(loaded);
            return true;
        }
    }
}

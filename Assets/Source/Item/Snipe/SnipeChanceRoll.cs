using System;
using BalloonParty.Game.Upgrades;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using UnityEngine;
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
    ///     path (<see cref="ISnipeArmer" />), so it is a Snipe lance in every way, pencil line included. After a hit,
    ///     no load rolls for <see cref="IUpgradeSettings.SnipeCooldownSeconds" />, so rapid fire can't chain lances.
    /// </summary>
    internal sealed class SnipeChanceRoll : ISnipeChanceRoll
    {
        private readonly IWeaponStats _weapon;
        private readonly ISnipeArmer _armer;
        private readonly IUpgradeSettings _settings;

        private float _lastArmedAt = float.NegativeInfinity;

        // Tests replace the draw to pin the roll, and the clock to step through the cooldown.
        internal Func<float> Random01 { get; set; } = () => Random.value;
        internal Func<float> Now { get; set; } = () => Time.time;

        internal SnipeChanceRoll(IWeaponStats weapon, ISnipeArmer armer, IUpgradeSettings settings)
        {
            _weapon = weapon;
            _armer = armer;
            _settings = settings;
        }

        // Random.value can return 1, so a chance of 1 has to hit without the draw.
        public bool TryArm(IWriteableProjectileModel loaded)
        {
            var chance = _weapon.SnipeChance;
            if (loaded == null || chance <= 0f || loaded.IsPiercing.Value)
            {
                return false;
            }

            var now = Now();
            if (now - _lastArmedAt < _settings.SnipeCooldownSeconds)
            {
                return false;
            }

            if (chance < 1f && Random01() >= chance)
            {
                return false;
            }

            _lastArmedAt = now;
            _armer.Arm(loaded);
            return true;
        }
    }
}

using System;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
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
    ///     path (<see cref="ISnipeArmer" />), so it is a Snipe lance in every way, pencil line included. The card's
    ///     chance is low; pity (<see cref="IAssistSettings.SnipePity" />) raises it for each ball loaded without a
    ///     Snipe while the player is in trouble (<see cref="IPlayerNeed" />), armour has stood on the board too long
    ///     (<see cref="IArmourNeed" />) or a bomb has sat under a formation's cover (<see cref="ICoveredBombNeed" />),
    ///     whichever is the most. After a hit, no load rolls for
    ///     <see cref="IUpgradeSettings.SnipeCooldownSeconds" />, so rapid fire can't chain lances. A purple level
    ///     raises the card's chance (<see cref="IUpgradeSettings.PurpleLevelSnipeChanceScale" />). A guaranteed
    ///     load (<see cref="ISnipeGuarantee" />, the first golden balloon's lesson) arms with no roll and no cooldown.
    /// </summary>
    internal sealed class SnipeChanceRoll : ISnipeChanceRoll, ISnipeGuarantee, IRunResettable
    {
        private readonly IWeaponStats _weapon;
        private readonly ISnipeArmer _armer;
        private readonly IUpgradeSettings _settings;
        private readonly IPlayerNeed _need;
        private readonly IArmourNeed _armour;
        private readonly ICoveredBombNeed _coveredBomb;
        private readonly IActiveLevelParameters _level;
        private readonly PityRoll _pity;

        private float _lastArmedAt = float.NegativeInfinity;
        private bool _guaranteed;

        // Tests replace the draw to pin the roll, and the clock to step through the cooldown.
        internal Func<float> Random01 { get; set; } = () => Random.value;
        internal Func<float> Now { get; set; } = () => Time.time;
        public int ResetOrder => RunResetOrder.Counters;

        internal SnipeChanceRoll(IWeaponStats weapon, ISnipeArmer armer, IUpgradeSettings settings, IPlayerNeed need,
            IArmourNeed armour, ICoveredBombNeed coveredBomb, IAssistSettings assist, IActiveLevelParameters level)
        {
            _weapon = weapon;
            _armer = armer;
            _settings = settings;
            _need = need;
            _armour = armour;
            _coveredBomb = coveredBomb;
            _level = level;
            _pity = new PityRoll(assist.SnipePity);
        }

        public bool TryArm(IWriteableProjectileModel loaded)
        {
            if (loaded == null || loaded.IsPiercing.Value)
            {
                return false;
            }

            var now = Now();
            if (_guaranteed)
            {
                _guaranteed = false;
                _lastArmedAt = now;
                _armer.Arm(loaded);
                return true;
            }

            var chance = _weapon.SnipeChance;
            if (chance <= 0f)
            {
                return false;
            }

            if (now - _lastArmedAt < _settings.SnipeCooldownSeconds)
            {
                return false;
            }

            if (_level.Current is { IsExtraColorLevel: true })
            {
                chance *= _settings.PurpleLevelSnipeChanceScale;
            }

            var need = Mathf.Max(_need.Need, _armour.Need, _coveredBomb.Need);
            if (!_pity.Roll(chance, need, Random01()))
            {
                return false;
            }

            _lastArmedAt = now;
            _armer.Arm(loaded);
            return true;
        }

        public void GuaranteeNext()
        {
            _guaranteed = true;
        }

        // A new run starts with no pity, no cooldown and no guarantee left over from the last.
        public void ResetRun(int generation)
        {
            _lastArmedAt = float.NegativeInfinity;
            _guaranteed = false;
            _pity.Clear();
        }
    }
}

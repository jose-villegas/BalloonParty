using BalloonParty.Game.Level;
using BalloonParty.Game.Upgrades;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Thrower
{
    /// <summary>
    ///     The thrower's reload rules: how far the held ball has slid from the pivot to the muzzle, and
    ///     whether a release fires it. The slide advances only while a slot under the ball cap is free,
    ///     so a ball held short of the muzzle is the player's "not yet" cue, and one at the muzzle fires.
    ///     Its speed depends on what's in the air: near-instant with no ball flying, slow while one is,
    ///     so firing again before a shot lands costs time. A ball that dies mid-slide speeds up the rest
    ///     of it. The cap is read live; the two reload times are the run's, cards included, taken at each
    ///     <see cref="Restart" /> so a pick applies from the next shot.
    /// </summary>
    internal sealed class ThrowerReload
    {
        // Must not overshoot: the ball reaching the muzzle tells the player a release fires, so it may
        // not get there before the reload has ended.
        private const Ease SlideEase = Ease.OutCubic;

        private readonly IWeaponStats _weapon;

        private float _progress;
        private float _busyDuration;
        private float _idleDuration;

        internal float Progress => _progress;

        /// <summary>The eased 0 (pivot) to 1 (muzzle) position of the held ball; 1 only once loaded.</summary>
        internal float SlidePosition => IsLoaded ? 1f : DOVirtual.EasedValue(0f, 1f, Progress, SlideEase);

        internal bool IsLoaded => _progress >= 1f;

        internal ThrowerReload(IWeaponStats weapon)
        {
            _weapon = weapon;
        }

        internal void Restart()
        {
            _progress = 0f;
            _busyDuration = _weapon.ReloadSeconds;
            _idleDuration = _weapon.IdleReloadSeconds;
        }

        internal void Advance(float deltaTime, int inFlight)
        {
            if (IsLoaded || !HasFreeSlot(inFlight))
            {
                return;
            }

            var duration = inFlight == 0 ? _idleDuration : _busyDuration;
            _progress = duration > 0f ? Mathf.Min(1f, _progress + deltaTime / duration) : 1f;
        }

        internal bool HasFreeSlot(int inFlight)
        {
            return inFlight < _weapon.MaxBallsInFlight;
        }

        /// <summary>Whether a ball may leave the thrower at all, reload aside: tools that skip the slide
        /// still respect the cap and the level phase.</summary>
        internal bool CanLaunch(int inFlight, LevelUpPhase phase)
        {
            return HasFreeSlot(inFlight) && phase == LevelUpPhase.Playing;
        }

        internal bool IsReady(int inFlight, LevelUpPhase phase)
        {
            return IsLoaded && CanLaunch(inFlight, phase);
        }
    }
}

using BalloonParty.Game.Level;
using BalloonParty.Shared;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Thrower
{
    /// <summary>
    ///     The thrower's reload rules: how far the held ball has slid from the pivot to the muzzle, and
    ///     whether a release fires it. The slide advances only while a slot under the ball cap is free,
    ///     so a ball held short of the muzzle is the player's "not yet" cue, and one at the muzzle fires.
    /// </summary>
    internal sealed class ThrowerReload
    {
        // Must not overshoot: the ball reaching the muzzle tells the player a release fires, so it may
        // not get there before the reload has ended.
        private const Ease SlideEase = Ease.OutCubic;

        private readonly IProjectileFlightConfig _config;

        private float _elapsed;
        private float _duration;

        internal float Progress => _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

        /// <summary>The eased 0 (pivot) to 1 (muzzle) position of the held ball; 1 only once loaded.</summary>
        internal float SlidePosition => IsLoaded ? 1f : DOVirtual.EasedValue(0f, 1f, Progress, SlideEase);

        internal bool IsLoaded => _elapsed >= _duration;

        internal ThrowerReload(IProjectileFlightConfig config)
        {
            _config = config;
        }

        internal void Restart()
        {
            _elapsed = 0f;

            // The cooldown is a floor on the slide: a slide shorter than it would read ready too early.
            _duration = Mathf.Max(_config.ProjectileLoadDuration, _config.ReloadCooldownSeconds);
        }

        internal void Advance(float deltaTime, int inFlight)
        {
            if (!IsLoaded && HasFreeSlot(inFlight))
            {
                _elapsed += deltaTime;
            }
        }

        internal bool HasFreeSlot(int inFlight)
        {
            return inFlight < _config.MaxBallsInFlight;
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

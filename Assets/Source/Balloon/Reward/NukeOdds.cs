using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Health;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     The nuke balloon's chance on a clock line (PLAN-NukeBalloon): <see cref="INukeOddsSettings.BaseChance" />
    ///     out of trouble, rising to <see cref="INukeOddsSettings.MaxChance" /> with the urgency. The urgency is the
    ///     weighted sum of four signals, each 0 to 1, capped at 1: the board's fill from
    ///     <see cref="INukeOddsSettings.FillFrom" /> to <see cref="INukeOddsSettings.FillFull" />, the spawn clock's
    ///     pressure, the hearts' need (0 at full, 1 on the last), and the hearts lost recently over
    ///     <see cref="INukeOddsSettings.LossesForFull" />. Each <see cref="WaveDamageMessage" /> adds its hearts to
    ///     the recent losses, and each clock line keeps <see cref="INukeOddsSettings.LossKeptPerLine" /> of them, so
    ///     hearts lost one line after another add up and a single loss fades.
    /// </summary>
    internal sealed class NukeOdds : INukeOdds, IStartable, IRunResettable, IDisposable
    {
        private readonly INukeOddsSettings _settings;
        private readonly IBoardFill _boardFill;
        private readonly ISpawnClock _clock;
        private readonly IPlayerHealth _health;
        private readonly ISubscriber<WaveDamageMessage> _damageSubscriber;

        private IDisposable _subscription;
        private float _recentLosses;

        public int ResetOrder => RunResetOrder.Counters;
        public float Chance => ChanceFor(_settings, Urgency);

        internal float Urgency => UrgencyOf(
            _settings, _boardFill.Occupancy, _clock.Pressure,
            PlayerNeed.HeartsNeed(_health.Current.Value, _health.Max), _recentLosses);

        internal float RecentLosses => _recentLosses;

        public NukeOdds(
            IBalloonsConfiguration balloons,
            IBoardFill boardFill,
            ISpawnClock clock,
            IPlayerHealth health,
            ISubscriber<WaveDamageMessage> damageSubscriber)
        {
            _settings = balloons.NukeOdds;
            _boardFill = boardFill;
            _clock = clock;
            _health = health;
            _damageSubscriber = damageSubscriber;
        }

        public void Start()
        {
            _subscription = _damageSubscriber.Subscribe(OnWaveDamage);
        }

        public void LinePassed()
        {
            _recentLosses *= Mathf.Clamp01(_settings.LossKeptPerLine);
        }

        public void ResetRun(int generation)
        {
            _recentLosses = 0f;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        internal static float UrgencyOf(
            INukeOddsSettings settings, float occupancy, float pressure, float heartsNeed, float recentLosses)
        {
            var losses = Mathf.Clamp01(recentLosses / Mathf.Max(0.1f, settings.LossesForFull));
            var urgency = settings.FillWeight * FillSignal(settings, occupancy)
                          + settings.PressureWeight * Mathf.Clamp01(pressure)
                          + settings.HeartsWeight * Mathf.Clamp01(heartsNeed)
                          + settings.LossWeight * losses;
            return Mathf.Clamp01(urgency);
        }

        internal static float ChanceFor(INukeOddsSettings settings, float urgency)
        {
            return Mathf.Clamp01(Mathf.Lerp(settings.BaseChance, settings.MaxChance, Mathf.Clamp01(urgency)));
        }

        // A full mark at or below FillFrom leaves no range to rise over: there the signal is a step.
        internal static float FillSignal(INukeOddsSettings settings, float occupancy)
        {
            if (settings.FillFull <= settings.FillFrom)
            {
                return occupancy >= settings.FillFrom ? 1f : 0f;
            }

            return Mathf.InverseLerp(settings.FillFrom, settings.FillFull, occupancy);
        }

        private void OnWaveDamage(WaveDamageMessage msg)
        {
            if (msg.HeartsLost > 0)
            {
                _recentLosses += msg.HeartsLost;
            }
        }
    }
}

using System;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    /// <summary>
    ///     The nuke's fuse and blast in sound (PLAN-NukeBalloon): a click and an alarm as it is armed, a beep on each
    ///     count of its fuse, a step higher each time, the boom as it goes off, and a gust of wind as the breather after
    ///     it starts. Its dents' clank is <c>CombatSoundRouter</c>'s, as for the other balloons with durability.
    /// </summary>
    internal sealed class NukeSoundRouter : IStartable, IDisposable
    {
        // Semitones between two beeps: a whole tone, so three counts climb a major third.
        internal const int BeepStepSemitones = 2;

        private readonly ISoundPlayer _player;
        private readonly ISubscriber<NukeArmedMessage> _armedSubscriber;
        private readonly ISubscriber<NukeCountdownMessage> _countdownSubscriber;
        private readonly ISubscriber<NukeDetonatedMessage> _detonatedSubscriber;
        private readonly ISubscriber<NukeBreatherMessage> _breatherSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        // The fuse's first count, which beeps at the root; each later one beeps a step higher.
        private int _firstCount = -1;

        [Inject]
        public NukeSoundRouter(ISoundPlayer player,
            ISubscriber<NukeArmedMessage> armedSubscriber,
            ISubscriber<NukeCountdownMessage> countdownSubscriber,
            ISubscriber<NukeDetonatedMessage> detonatedSubscriber,
            ISubscriber<NukeBreatherMessage> breatherSubscriber)
        {
            _player = player;
            _armedSubscriber = armedSubscriber;
            _countdownSubscriber = countdownSubscriber;
            _detonatedSubscriber = detonatedSubscriber;
            _breatherSubscriber = breatherSubscriber;
        }

        public void Start()
        {
            _armedSubscriber.Subscribe(OnArmed).AddTo(_subscriptions);
            _countdownSubscriber.Subscribe(OnCountdown).AddTo(_subscriptions);
            _detonatedSubscriber.Subscribe(m => _player.Play(GameSoundId.BalloonPopNuke, m.Position))
                .AddTo(_subscriptions);
            // The wind blows over the whole board, so it isn't panned to the nuke.
            _breatherSubscriber.Subscribe(_ => _player.Play(GameSoundId.NukeBreatherWind, null)).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnArmed(NukeArmedMessage message)
        {
            _firstCount = -1;
            _player.Play(GameSoundId.NukeArmed, message.Position);
        }

        private void OnCountdown(NukeCountdownMessage message)
        {
            if (_firstCount < message.SecondsLeft)
            {
                _firstCount = message.SecondsLeft;
            }

            var step = (_firstCount - message.SecondsLeft) * BeepStepSemitones;
            _player.Play(GameSoundId.NukeCountdownBeep, message.Position, semitoneOffset: step);
        }
    }
}

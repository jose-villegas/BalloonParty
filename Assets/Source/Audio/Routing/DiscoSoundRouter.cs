using System;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    /// <summary>
    ///     The disco ball's wave in sound (PLAN-DiscoBalloon). A glissando climbs two octaves of the board's key over the
    ///     wave's time, and each balloon the wave paints ticks at the degree its distance from the pop reaches. Both play
    ///     their entries as <c>ScaleWalkUp</c>, with the degree passed as the melodic step, so every note lands in the key.
    ///     The tick's cooldown in the sound bank is what keeps a full board from stacking a clip per balloon.
    /// </summary>
    internal sealed class DiscoSoundRouter : IStartable, IDisposable
    {
        // Two octaves of the pentatonic key, root to root: the glissando's notes, and the ticks' range of pitch.
        internal const int ScaleDegrees = 10;

        // The glissando starts an octave under the root and ends an octave over it, so the ticks sparkle above it.
        internal const int GlissandoSemitoneOffset = -12;

        private readonly ISoundPlayer _player;
        private readonly ISubscriber<DiscoWaveAdvancedMessage> _advancedSubscriber;
        private readonly ISubscriber<DiscoWaveReachedMessage> _reachedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private int _glissandoDegree = -1;
        private float _lastProgress;

        [Inject]
        public DiscoSoundRouter(ISoundPlayer player,
            ISubscriber<DiscoWaveAdvancedMessage> advancedSubscriber,
            ISubscriber<DiscoWaveReachedMessage> reachedSubscriber)
        {
            _player = player;
            _advancedSubscriber = advancedSubscriber;
            _reachedSubscriber = reachedSubscriber;
        }

        public void Start()
        {
            _advancedSubscriber.Subscribe(OnAdvanced).AddTo(_subscriptions);
            _reachedSubscriber.Subscribe(OnReached).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // A wave's time only grows, so a step back is a new wave, whether the last one ended or was cut short; a frame
        // with no time (a pause right after the pop) repeats a progress and replays nothing. A slow frame that crosses
        // several notes plays only the last: two notes in one frame would fall in the sound service's burst window,
        // which detunes the second off the key.
        private void OnAdvanced(DiscoWaveAdvancedMessage message)
        {
            if (message.Progress < _lastProgress)
            {
                _glissandoDegree = -1;
            }

            _lastProgress = message.Progress;

            var degree = Mathf.FloorToInt(Mathf.Clamp01(message.Progress) * ScaleDegrees);
            if (degree <= _glissandoDegree)
            {
                return;
            }

            _glissandoDegree = degree;
            _player.Play(GameSoundId.DiscoWaveGlissando, null, melodicStreak: degree,
                semitoneOffset: GlissandoSemitoneOffset);
        }

        private void OnReached(DiscoWaveReachedMessage message)
        {
            var degree = Mathf.RoundToInt(Mathf.Clamp01(message.Reach) * ScaleDegrees);
            _player.Play(GameSoundId.DiscoWaveTick, message.Position, melodicStreak: degree);
        }
    }
}

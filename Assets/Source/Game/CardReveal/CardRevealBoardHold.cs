using System;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.Game.Run;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.CardReveal
{
    /// <summary>
    ///     Holds the board while a card reveal plays over play (a retry's bonus card, or a reveal that runs on into
    ///     play): the balloon takes the screen and the player can't aim, so lines landing and balls flying meanwhile
    ///     would be time taken from them. Pauses <see cref="PauseSource.CardReveal" /> in
    ///     <see cref="NavigationState.Game" /> until the reveal's card is out (<see cref="ICardReveals.Shown" />),
    ///     then hands play back with time eased up from a standstill over
    ///     <see cref="ICardRevealSettings.BoardReturnSeconds" /> of real time, while the ribbons still fly. A
    ///     level-up's reveal during its ceremony and ascent needs no hold.
    /// </summary>
    internal sealed class CardRevealBoardHold : IStartable, ITickable, IRunResettable, IDisposable
    {
        private readonly ICardReveals _reveals;
        private readonly INavigation _navigation;
        private readonly PauseService _pauseService;
        private readonly ITimeScaleClaims _timeScale;
        private readonly ICardRevealSettings _settings;
        private readonly CompositeDisposable _subscriptions = new();

        private bool _holding;
        private bool _returning;
        private float _returnElapsed;

        public int ResetOrder => RunResetOrder.Counters;

        internal CardRevealBoardHold(ICardReveals reveals, INavigation navigation, PauseService pauseService,
            ITimeScaleClaims timeScale, ICardRevealSettings settings)
        {
            _reveals = reveals;
            _navigation = navigation;
            _pauseService = pauseService;
            _timeScale = timeScale;
            _settings = settings;
        }

        public void Start()
        {
            _reveals.Current.Subscribe(_ => Refresh()).AddTo(_subscriptions);
            _reveals.Shown.Subscribe(_ => Refresh()).AddTo(_subscriptions);
            _navigation.Current.Subscribe(_ => Refresh()).AddTo(_subscriptions);
        }

        public void Tick()
        {
            Advance(Time.unscaledDeltaTime);
        }

        // A run reset clears the pause and time-scale claims itself; the return just stops, so it can't claim again.
        public void ResetRun(int generation)
        {
            _returning = false;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            SetHolding(false);
            StopReturn();
        }

        internal void Advance(float unscaledSeconds)
        {
            if (!_returning)
            {
                return;
            }

            _returnElapsed += unscaledSeconds;
            var duration = _settings.BoardReturnSeconds;
            if (duration <= 0f || _returnElapsed >= duration)
            {
                StopReturn();
                return;
            }

            _timeScale.Claim(TimeScaleSource.CardReveal, Mathf.SmoothStep(0f, 1f, _returnElapsed / duration));
        }

        private void Refresh()
        {
            var inPlay = _navigation.Current.Value == NavigationState.Game;
            var revealing = _reveals.Current.Value != null;
            var holding = inPlay && revealing && !_reveals.Shown.Value;
            if (_holding && !holding && inPlay && revealing)
            {
                // Claimed before the pause lifts, so play never runs a frame at full speed first.
                StartReturn();
            }
            else if (holding || !inPlay)
            {
                StopReturn();
            }

            SetHolding(holding);
        }

        private void StartReturn()
        {
            _returning = true;
            _returnElapsed = 0f;
            _timeScale.Claim(TimeScaleSource.CardReveal, 0f);
        }

        private void StopReturn()
        {
            _returning = false;
            _timeScale.Release(TimeScaleSource.CardReveal);
        }

        // A run reset clears the pause service's sources itself; the reveal it drops in the same reset resumes
        // here too, and resuming a source that isn't paused does nothing.
        private void SetHolding(bool holding)
        {
            if (holding == _holding)
            {
                return;
            }

            _holding = holding;
            if (holding)
            {
                _pauseService.Pause(PauseSource.CardReveal);
            }
            else
            {
                _pauseService.Resume(PauseSource.CardReveal);
            }
        }
    }
}

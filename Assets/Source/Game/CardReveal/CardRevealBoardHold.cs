using System;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.CardReveal
{
    /// <summary>
    ///     Holds the board while a card reveal plays over play (a retry's bonus card, or a reveal that runs on into
    ///     play): the reveal takes the screen and the player can't aim, so lines landing and balls flying meanwhile
    ///     would be time taken from them. Pauses <see cref="PauseSource.CardReveal" /> for as long as one plays in
    ///     <see cref="NavigationState.Game" />; a level-up's reveal during its ceremony and ascent needs no hold.
    /// </summary>
    internal sealed class CardRevealBoardHold : IStartable, IDisposable
    {
        private readonly ICardReveals _reveals;
        private readonly INavigation _navigation;
        private readonly PauseService _pauseService;
        private readonly CompositeDisposable _subscriptions = new();

        private bool _holding;

        internal CardRevealBoardHold(ICardReveals reveals, INavigation navigation, PauseService pauseService)
        {
            _reveals = reveals;
            _navigation = navigation;
            _pauseService = pauseService;
        }

        public void Start()
        {
            _reveals.Current.Subscribe(_ => Refresh()).AddTo(_subscriptions);
            _navigation.Current.Subscribe(_ => Refresh()).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            SetHolding(false);
        }

        private void Refresh()
        {
            SetHolding(_reveals.Current.Value != null && _navigation.Current.Value == NavigationState.Game);
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

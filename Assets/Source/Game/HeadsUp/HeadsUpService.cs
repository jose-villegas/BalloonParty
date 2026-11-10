using System;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.Level;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.HeadsUp
{
    /// <summary>
    ///     Takes the routers' heads-up requests, lets <see cref="HeadsUpQueue" /> decide which show, and drives
    ///     <see cref="IHeadsUpPresenter" />. Clears every heads-up when the run resets or the player leaves for the
    ///     launch screen (at once), and on game over or the level-up popup (fading); takes no requests outside the
    ///     game screen. Moves the lines on unscaled time, so they keep writing through slow-motion, but hands them no
    ///     live time under a pause, so they freeze with the game.
    /// </summary>
    internal sealed class HeadsUpService : IHeadsUps, IStartable, ITickable, IDisposable
    {
        private readonly IHeadsUpSettings _settings;
        private readonly IHeadsUpPresenter _presenter;
        private readonly PauseService _pause;
        private readonly ILevelProgress _levelProgress;
        private readonly INavigation _navigation;
        private readonly ISubscriber<RunResetMessage> _resetSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly HeadsUpQueue _queue;
        private readonly CompositeDisposable _subscriptions = new();

        // Live seconds, stopped under a pause: what cooldowns count in.
        private float _now;

        internal HeadsUpService(
            IHeadsUpSettings settings,
            IHeadsUpPresenter presenter,
            PauseService pause,
            ILevelProgress levelProgress,
            INavigation navigation,
            ISubscriber<RunResetMessage> resetSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber)
        {
            _settings = settings;
            _presenter = presenter;
            _pause = pause;
            _levelProgress = levelProgress;
            _navigation = navigation;
            _resetSubscriber = resetSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _queue = new HeadsUpQueue(settings);
        }

        public HeadsUpHandle Request(in HeadsUpRequest request)
        {
            // A pop's item or trail can resolve a frame after the level-up or game-over screen took over.
            if (_navigation.Current.Value != NavigationState.Game)
            {
                return HeadsUpHandle.None;
            }

            var offer = _queue.Offer(request, _now);
            switch (offer.Kind)
            {
                case HeadsUpOfferKind.Show:
                    if (offer.Evicted.IsValid)
                    {
                        _presenter.Release(offer.Evicted);
                    }

                    _presenter.Show(offer.Handle, request, _settings.Style(request.Style));
                    return offer.Handle;
                case HeadsUpOfferKind.Rewrite:
                    _presenter.Rewrite(offer.Handle, request, _settings.Style(request.Style));
                    return offer.Handle;
                default:
                    return HeadsUpHandle.None;
            }
        }

        public void Release(HeadsUpHandle handle)
        {
            if (_queue.Free(handle))
            {
                _presenter.Release(handle);
            }
        }

        public void Start()
        {
            _presenter.Finished.Subscribe(OnFinished).AddTo(_subscriptions);
            _resetSubscriber.Subscribe(_ => ClearAll(false)).AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(_ => ClearAll(true)).AddTo(_subscriptions);
            // Pending, not Completing: the winning pop's slow-motion beat is when the lines matter most.
            _levelProgress.Phase.Where(phase => phase == LevelUpPhase.Pending)
                .Subscribe(_ => ClearAll(true)).AddTo(_subscriptions);
            // The level-up and game-over screens come after their own fading clear; only leaving the game snaps.
            _navigation.Current.Where(state => state == NavigationState.Launch)
                .Subscribe(_ => ClearAll(false)).AddTo(_subscriptions);
        }

        public void Tick()
        {
            Advance(Time.unscaledDeltaTime);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        internal void Advance(float unscaledDelta)
        {
            var liveDelta = _pause.IsAnyPaused.Value ? 0f : unscaledDelta;
            _now += liveDelta;
            _presenter.Advance(liveDelta, unscaledDelta);
        }

        private void OnFinished(HeadsUpHandle handle)
        {
            _queue.Free(handle);
        }

        private void ClearAll(bool animated)
        {
            _queue.ClearAll();
            _presenter.ClearAll(animated);
        }
    }
}

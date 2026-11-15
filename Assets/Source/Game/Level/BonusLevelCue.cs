using System;
using BalloonParty.Game.Run;
using BalloonParty.Shared.GameState;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Says when a purple (extra-colour) level starts, so the screen can name it a bonus: once per level, as
    ///     play begins on it (navigation reaching <see cref="NavigationState.Game" />: after the level-up's
    ///     ascent, or a retry). A retry is a new attempt, so the level is announced again.
    /// </summary>
    internal sealed class BonusLevelCue : IBonusLevelCue, IStartable, IRunResettable, IDisposable
    {
        private readonly INavigation _navigation;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ILevelProgress _levelProgress;
        private readonly Subject<int> _started = new();
        private readonly CompositeDisposable _subscriptions = new();

        private int _announcedLevel;

        public int ResetOrder => RunResetOrder.Counters;
        public IObservable<int> Started => _started;

        internal BonusLevelCue(INavigation navigation, IActiveLevelParameters levelParams, ILevelProgress levelProgress)
        {
            _navigation = navigation;
            _levelParams = levelParams;
            _levelProgress = levelProgress;
        }

        public void Start()
        {
            _navigation.Current.Where(state => state == NavigationState.Game)
                .Subscribe(_ => TryAnnounce())
                .AddTo(_subscriptions);
        }

        public void ResetRun(int generation)
        {
            _announcedLevel = 0;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _started.Dispose();
        }

        private void TryAnnounce()
        {
            var level = _levelProgress.Level.Value;
            if (level == _announcedLevel || _levelParams.Current is not { IsExtraColorLevel: true })
            {
                return;
            }

            _announcedLevel = level;
            _started.OnNext(level);
        }
    }
}

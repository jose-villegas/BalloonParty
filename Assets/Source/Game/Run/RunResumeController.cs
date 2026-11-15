using System;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.GameState;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Takes the launch screen's Continue (<see cref="IRunResumeRequest" />) and resumes the saved run while
    ///     the launch screen is still up: stages it as a retry of its level that spends no retry, and resets the
    ///     run. A run saved on the game-over screen goes back to it once the game shows. Leaving the launch screen
    ///     any other way starts a new run, which drops the save.
    /// </summary>
    internal sealed class RunResumeController : IStartable, ITickable, IDisposable
    {
        private readonly IRunResumeRequest _request;
        private readonly IRunResumeStaging _staging;
        private readonly IRunSave _save;
        private readonly RunController _runController;
        private readonly INavigation _navigation;
        private readonly CompositeDisposable _subscriptions = new();

        private RunSnapshot _pendingLoss;
        private bool _resumed;
        private bool _leftLaunch;
        private bool _keepSave;

        public RunResumeController(
            IRunResumeRequest request,
            IRunResumeStaging staging,
            IRunSave save,
            RunController runController,
            INavigation navigation)
        {
            _request = request;
            _staging = staging;
            _save = save;
            _runController = runController;
            _navigation = navigation;
        }

        // The request first: one made before this scope finished loading must be seen before navigation is.
        public void Start()
        {
            _request.Pending.Where(pending => pending).Subscribe(_ => OnRequested()).AddTo(_subscriptions);
            _navigation.Current.Subscribe(OnNavigationChanged).AddTo(_subscriptions);
        }

        // A frame after the game shows, not inside the navigation change: a second transition nested in the
        // first would reach the later subscribers out of order.
        public void Tick()
        {
            if (_pendingLoss == null || _navigation.Current.Value != NavigationState.Game)
            {
                return;
            }

            var loss = _pendingLoss;
            _pendingLoss = null;
            _runController.RestoreLoss(loss.Level, loss.FinalScore);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnRequested()
        {
            if (!_request.Consume())
            {
                return;
            }

            // Past the launch screen the board may already be out; the save stays for the next launch.
            if (_leftLaunch || _navigation.Current.Value != NavigationState.Launch)
            {
                Log.Warn("RunSave", "Continue arrived after the launch screen; the saved run is kept for next time");
                _keepSave = true;
                return;
            }

            if (!_save.TryLoad(out var snapshot))
            {
                Log.Warn("RunSave", "Continue pressed with no saved run; starting a new one");
                return;
            }

            _staging.PrepareResume(snapshot);
            _runController.ResumeRun();
            _resumed = true;
            _pendingLoss = snapshot.OnGameOver ? snapshot : null;
        }

        private void OnNavigationChanged(NavigationState state)
        {
            if (state == NavigationState.Launch || _leftLaunch)
            {
                return;
            }

            _leftLaunch = true;
            if (!_resumed && !_keepSave)
            {
                _save.Clear();
            }
        }
    }
}

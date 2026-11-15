using System;
using BalloonParty.Game.Run;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Offers Continue on the launch screen while a run is saved. A press hands the resume to the game
    ///     (<see cref="IRunResumeRequest" />), which restores the run behind the launch screen, then plays the
    ///     same ascent Play does into the game.
    /// </summary>
    internal sealed class LaunchContinueController : IStartable, IDisposable
    {
        private readonly LaunchContinueView _view;
        private readonly LaunchPlayTrigger _play;
        private readonly IRunSave _save;
        private readonly IRunResumeRequest _request;

        private IDisposable _pressedSubscription;

        public LaunchContinueController(
            LaunchContinueView view,
            LaunchPlayTrigger play,
            IRunSave save,
            IRunResumeRequest request)
        {
            _view = view;
            _play = play;
            _save = save;
            _request = request;
        }

        public void Start()
        {
            if (_save.TryLoad(out var snapshot))
            {
                _view.Show(snapshot.Level);
            }
            else
            {
                _view.Hide();
            }

            _pressedSubscription = _view.Pressed.Subscribe(_ => OnPressed());
        }

        public void Dispose()
        {
            _pressedSubscription?.Dispose();
        }

        // Play already pressed means a new run is on its way: the save is about to go with it.
        private void OnPressed()
        {
            if (_play.Triggered)
            {
                return;
            }

            _request.Request();
            _play.Play();
        }
    }
}

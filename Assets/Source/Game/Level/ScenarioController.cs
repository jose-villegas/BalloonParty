using System;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Run;
using BalloonParty.Scenario;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Keeps <see cref="ActiveScenario" /> on the current level's scenario (sky or ground, per
    ///     <see cref="ILevelPacingConfiguration.ScenarioForLevel" />). Cuts on run start and on a plain restart, so
    ///     the start-level cheat and a restart land on the right backdrop. Reveals through the mist over the
    ///     length of the beat on a level-up Ascent and on the restart rise after a loss. A loss's restart leaves the
    ///     scenario alone until its rise starts, since the reveal scales the content root and the new scenery is
    ///     placed under it first.
    /// </summary>
    internal sealed class ScenarioController : IStartable, IDisposable
    {
        private readonly ILevelPacingConfiguration _pacing;
        private readonly IRunSeed _runSeed;
        private readonly ILevelProgress _levelProgress;
        private readonly ActiveScenario _scenario;
        private readonly ICinematicsSettings _cinematics;
        private readonly ISubscriber<LevelAscendStartedMessage> _ascendStarted;
        private readonly ISubscriber<RunResetMessage> _runReset;
        private readonly ISubscriber<RestartRiseStartedMessage> _riseStarted;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _transitionCompleted;
        private readonly ISubscriber<RunRestartCompletedMessage> _restartCompleted;
        private readonly CompositeDisposable _subscriptions = new();

        internal ScenarioController(
            ILevelPacingConfiguration pacing, IRunSeed runSeed, ILevelProgress levelProgress,
            ActiveScenario scenario, ICinematicsSettings cinematics,
            ISubscriber<LevelAscendStartedMessage> ascendStarted, ISubscriber<RunResetMessage> runReset,
            ISubscriber<RestartRiseStartedMessage> riseStarted,
            ISubscriber<LevelTransitionCompletedMessage> transitionCompleted,
            ISubscriber<RunRestartCompletedMessage> restartCompleted)
        {
            _pacing = pacing;
            _runSeed = runSeed;
            _levelProgress = levelProgress;
            _scenario = scenario;
            _cinematics = cinematics;
            _ascendStarted = ascendStarted;
            _runReset = runReset;
            _riseStarted = riseStarted;
            _transitionCompleted = transitionCompleted;
            _restartCompleted = restartCompleted;
        }

        void IStartable.Start()
        {
            // The level has already advanced when the Ascent starts: LevelController sets it before the phase
            // flips to Transitioning.
            _ascendStarted
                .Subscribe(_ => _scenario.Reveal(LevelScenario(), _cinematics.LevelAscend.AscentSeconds))
                .AddTo(_subscriptions);

            // After every IRunResettable, so the restart level and a fresh run's seed are already in place. With
            // the board deferred, the loss cinematic rises the new level in once its scenery is placed, and the
            // mist rides that rise.
            _runReset.Subscribe(OnRunReset).AddTo(_subscriptions);
            _riseStarted
                .Subscribe(_ => _scenario.Reveal(LevelScenario(), _cinematics.LevelAscend.RestartRiseSeconds))
                .AddTo(_subscriptions);

            // The reveal runs on its own clock, so the end of its beat settles it: a skipped or cancelled Ascent
            // (the completion fires from a finally) leaves no mist over the board. A reveal already done sees no
            // change, since a cut to the scenario showing is no switch.
            _transitionCompleted.Subscribe(_ => _scenario.Cut(LevelScenario())).AddTo(_subscriptions);
            _restartCompleted.Subscribe(_ => _scenario.Cut(LevelScenario())).AddTo(_subscriptions);
            _scenario.Cut(LevelScenario());
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnRunReset(RunResetMessage message)
        {
            if (message.BoardReset)
            {
                _scenario.Cut(LevelScenario());
            }
        }

        private ScenarioKind LevelScenario()
        {
            return _pacing.ScenarioForLevel(_levelProgress.Level.Value, _runSeed.Value);
        }
    }
}

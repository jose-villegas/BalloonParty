using BalloonParty.Shared.Diagnostics;
using VContainer;

namespace BalloonParty.Game.Run
{
    internal sealed class RunSeed : IRunSeed, IRunResettable
    {
        private readonly IRetryState _retryState;
        private readonly System.Random _rolls;

        private int _value;

        public int Value => _value;

        // Before LevelDifficultyResolver (Derived) and ScenarioController read it, and while RetryTracker
        // (Score + 10) still holds the retry level that says whether this reset is a retry.
        public int ResetOrder => RunResetOrder.Board + 10;

        [Inject]
        public RunSeed(IRetryState retryState) : this(retryState, new System.Random())
        {
        }

        internal RunSeed(IRetryState retryState, System.Random rolls)
        {
            _retryState = retryState;
            _rolls = rolls;
            Roll();
        }

        public void ResetRun(int generation)
        {
            if (_retryState.RetryLevel > 0)
            {
                return;
            }

            Roll();
        }

        private void Roll()
        {
            _value = _rolls.Next();
            Log.Info("RunSeed", $"New run seed {_value}");
        }
    }
}

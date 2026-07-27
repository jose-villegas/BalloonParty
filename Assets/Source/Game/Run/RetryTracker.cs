using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Tracks per-run retry state. Call <see cref="PrepareRetry"/> before dismissing the game-over
    ///     screen to restart at the same level; otherwise the next restart is a fresh run.
    /// </summary>
    internal sealed class RetryTracker : IRetryState, IRunResettable
    {
        private readonly int _maxRetries;

        private int _retriesUsed;
        private int _retryLevel;

        public int RetriesRemaining => _maxRetries - _retriesUsed;
        public int RetryLevel => _retryLevel;

        // Runs after LevelController (100) and LevelDifficultyResolver (40) have consumed RetryLevel.
        public int ResetOrder => RunResetOrder.Score + 10;

        public RetryTracker(IRunConfig config)
        {
            _maxRetries = config.MaxRetries;
        }

        /// <summary>Prepare a retry at the given level. Call before publishing the dismiss message.</summary>
        public void PrepareRetry(int level)
        {
            _retryLevel = level;
            Log.Info("RetryTracker", $"Prepared retry at level {level} (used {_retriesUsed}/{_maxRetries})");
        }

        public void ResetRun(int generation)
        {
            if (_retryLevel > 0)
            {
                _retriesUsed++;
                Log.Info("RetryTracker", $"Retry consumed (used {_retriesUsed}/{_maxRetries})");
            }
            else
            {
                _retriesUsed = 0;
            }

            _retryLevel = 0;
        }
    }
}

using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Tracks per-run retry state. <see cref="PrepareRetry"/> (staged by <see cref="IFailureOffers" />)
    ///     makes the next restart a retry at that level; otherwise it's a fresh run. <see cref="PrepareResume" />
    ///     stages a saved run the same way, as a retry of its level that spends no retry.
    /// </summary>
    internal sealed class RetryTracker : IRetryState, IRetryStaging, IRunResumeStaging, IRunResettable
    {
        private readonly int _maxRetries;

        private int _retriesUsed;
        private int _retryLevel;
        private bool _retryWithBonusCard;
        private RunSnapshot _resuming;

        public bool IsUnlimited => _maxRetries <= 0;
        public int RetriesRemaining => IsUnlimited ? int.MaxValue : _maxRetries - _retriesUsed;
        public int RetriesUsed => _retriesUsed;
        public int RetryLevel => _retryLevel;
        public bool RetryWithBonusCard => _retryWithBonusCard;
        public RunSnapshot Resuming => _resuming;

        // Runs after RunUpgrades (60), LevelDifficultyResolver (40) and LevelController (100) have read
        // RetryLevel and RetryWithBonusCard.
        public int ResetOrder => RunResetOrder.Score + 10;

        public RetryTracker(IRunConfig config)
        {
            _maxRetries = config.MaxRetries;
        }

        public void PrepareRetry(int level, bool withBonusCard)
        {
            _retryLevel = level;
            _retryWithBonusCard = withBonusCard;
            _resuming = null;
            var bonus = withBonusCard ? " with a bonus card" : "";
            Log.Info("RetryTracker", $"Prepared retry at level {level}{bonus} (used {_retriesUsed}/{CapLabel()})");
        }

        public void PrepareResume(RunSnapshot snapshot)
        {
            _resuming = snapshot;
            _retryLevel = snapshot.Level;
            _retryWithBonusCard = false;
            Log.Info("RetryTracker", $"Prepared resume at level {snapshot.Level} (used {snapshot.RetriesUsed})");
        }

        public void ResetRun(int generation)
        {
            if (_resuming != null)
            {
                // The saved count, not one more: a resume isn't a retry, and not zero: it can't buy back the ad gate.
                _retriesUsed = _resuming.RetriesUsed;
                Log.Info("RetryTracker", $"Run resumed (used {_retriesUsed}/{CapLabel()})");
            }
            else if (_retryLevel > 0)
            {
                _retriesUsed++;
                Log.Info("RetryTracker", $"Retry consumed (used {_retriesUsed}/{CapLabel()})");
            }
            else
            {
                _retriesUsed = 0;
            }

            _retryLevel = 0;
            _retryWithBonusCard = false;
            _resuming = null;
        }

        private string CapLabel()
        {
            return IsUnlimited ? "unlimited" : _maxRetries.ToString();
        }
    }
}

using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Tracks per-run retry state. <see cref="PrepareRetry"/> (staged by <see cref="IFailureOffers" />)
    ///     makes the next restart a retry at that level; otherwise it's a fresh run.
    /// </summary>
    internal sealed class RetryTracker : IRetryState, IRetryStaging, IRunResettable
    {
        private readonly int _maxRetries;

        private int _retriesUsed;
        private int _retryLevel;
        private bool _retryWithBonusCard;

        public bool IsUnlimited => _maxRetries <= 0;
        public int RetriesRemaining => IsUnlimited ? int.MaxValue : _maxRetries - _retriesUsed;
        public int RetriesUsed => _retriesUsed;
        public int RetryLevel => _retryLevel;
        public bool RetryWithBonusCard => _retryWithBonusCard;

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
            var bonus = withBonusCard ? " with a bonus card" : "";
            Log.Info("RetryTracker", $"Prepared retry at level {level}{bonus} (used {_retriesUsed}/{CapLabel()})");
        }

        public void ResetRun(int generation)
        {
            if (_retryLevel > 0)
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
        }

        private string CapLabel()
        {
            return IsUnlimited ? "unlimited" : _maxRetries.ToString();
        }
    }
}

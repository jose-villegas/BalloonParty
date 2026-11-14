using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     Decides when the reward balloons come (PLAN-RewardBalloons). At most one of each on the board, and only a
    ///     type the catalogue has an entry for.
    ///     <para>
    ///         The golden balloon, on a purple level: the first from the line that finds the level
    ///         <see cref="IRewardBalloonSettings.GoldenAtCompletion" /> done. Its pop rolls once for another
    ///         (<see cref="IRewardBalloonSettings.GoldenRepeatChances" />, up to three a level), which comes a few
    ///         lines later; a board clear or a level change rolls nothing.
    ///     </para>
    ///     <para>
    ///         The heart balloon: on a purple level, the schedule's steps (<see cref="IRewardBalloonSettings.PurpleHearts" />),
    ///         each rolled once as the level's completion reaches it, at full hearts too. Elsewhere, a roll on every line
    ///         while a heart is missing, likelier the more are, after a cooldown since the last.
    ///     </para>
    /// </summary>
    internal sealed class RewardBalloonService : IRewardBalloonRequests, IStartable, IRunResettable, IDisposable
    {
        private readonly IRewardBalloonSettings _settings;
        private readonly ILevelProgress _levelProgress;
        private readonly IActiveLevelParameters _levelParams;
        private readonly IPlayerHealth _health;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly bool _goldenPlaceable;
        private readonly bool _heartPlaceable;

        private IDisposable _levelSubscription;
        private IDisposable _hitSubscription;
        private int _goldensThisLevel;
        // A further golden balloon won by the last one's pop, still to come.
        private bool _goldenOwed;
        private int _linesSinceGoldenPop;
        // The golden placement whose pop has rolled (or can no longer roll: one from an earlier level or run).
        private int _goldenRolled;
        // The purple level's next heart step to roll, and the hearts its rolls won that are still to come.
        private int _purpleHeartStep;
        private int _purpleHeartsOwed;
        private bool _goldenAlive;
        private bool _heartAlive;
        // A heart rolled for a line that had no room for it, still owed to the next.
        private bool _heartPending;
        private int _linesSinceHeart;
        // Which placement a leave callback belongs to; a stale one (an earlier balloon, or one from before a run
        // reset) must not clear the flag of the balloon on the board now.
        private int _goldenPlacement;
        private int _heartPlacement;

        public int ResetOrder => RunResetOrder.Counters;

        // A seam for tests, as UnbreakableFormationService's.
        internal Func<float> Random01 { get; set; } = () => Random.value;

        public RewardBalloonService(
            IBalloonsConfiguration balloons,
            ILevelProgress levelProgress,
            IActiveLevelParameters levelParams,
            IPlayerHealth health,
            ISubscriber<ActorHitMessage> hitSubscriber)
        {
            _settings = balloons.RewardBalloons;
            _levelProgress = levelProgress;
            _levelParams = levelParams;
            _health = health;
            _hitSubscriber = hitSubscriber;
            _goldenPlaceable = HasEntry(balloons, BalloonType.Golden);
            _heartPlaceable = HasEntry(balloons, BalloonType.Heart);
            _linesSinceHeart = _settings.HeartCooldownLines;
        }

        public void Start()
        {
            // Every new level gets its own golden balloons and heart schedule; a retry's restart is a run reset.
            _levelSubscription = _levelProgress.Level.Subscribe(_ => ForgetLevelsRewards());
            _hitSubscription = _hitSubscriber.Subscribe(OnHit);
        }

        public bool TryTakeDue(out BalloonType type)
        {
            type = default;
            if (_levelProgress.Phase.Value != LevelUpPhase.Playing)
            {
                return false;
            }

            _linesSinceHeart++;
            _linesSinceGoldenPop++;
            if (GoldenDue())
            {
                type = BalloonType.Golden;
                return true;
            }

            if (HeartDue())
            {
                type = BalloonType.Heart;
                return true;
            }

            return false;
        }

        public Action Placed(BalloonType type)
        {
            switch (type)
            {
                case BalloonType.Golden:
                {
                    _goldensThisLevel++;
                    _goldenOwed = false;
                    _goldenAlive = true;
                    var placement = ++_goldenPlacement;
                    return () => GoldenLeft(placement);
                }
                case BalloonType.Heart:
                {
                    _heartAlive = true;
                    _heartPending = false;
                    _purpleHeartsOwed = Math.Max(0, _purpleHeartsOwed - 1);
                    _linesSinceHeart = 0;
                    var placement = ++_heartPlacement;
                    return () => HeartLeft(placement);
                }
                default:
                    return null;
            }
        }

        public void ResetRun(int generation)
        {
            _goldenPlacement++;
            _heartPlacement++;
            ForgetLevelsRewards();
            _goldenAlive = false;
            _heartAlive = false;
            _heartPending = false;
            _linesSinceHeart = _settings.HeartCooldownLines;
        }

        public void Dispose()
        {
            _levelSubscription?.Dispose();
            _hitSubscription?.Dispose();
        }

        /// <summary>
        ///     A golden balloon's pop rolls once for the level's next one. Only a pop rolls: the leave callback also
        ///     fires for a board clear and a level change. The pop's leave has already run when its hit arrives
        ///     (<c>HitPipeline</c> routes the hit before publishing it), so the guard is the placement, not whether
        ///     one is on the board. The spawn cheat can place a second golden beside the first; either one's pop then
        ///     rolls for the newer.
        /// </summary>
        internal void OnHit(ActorHitMessage msg)
        {
            if (!RewardBalloonPop.TryGet(msg, out var balloon) || balloon.TypeName != BalloonType.Golden
                || _goldenRolled == _goldenPlacement)
            {
                return;
            }

            _goldenRolled = _goldenPlacement;
            var chances = _settings.GoldenRepeatChances;
            var next = _goldensThisLevel - 1;
            _goldenOwed = chances != null && next >= 0 && next < chances.Count && Random01() < chances[next];
            _linesSinceGoldenPop = 0;
        }

        /// <summary>
        ///     The heart balloon's chance on a line off a purple level: none at full hearts (or past them), more the
        ///     more are missing.
        /// </summary>
        internal static float HeartChance(IRewardBalloonSettings settings, int missingHearts)
        {
            if (missingHearts <= 0)
            {
                return 0f;
            }

            return missingHearts == 1 ? settings.HeartChanceMissingOne : settings.HeartChanceMissingTwo;
        }

        private static bool HasEntry(IBalloonsConfiguration balloons, BalloonType type)
        {
            foreach (var entry in balloons.Entries)
            {
                if (entry.BalloonType == type)
                {
                    return true;
                }
            }

            return false;
        }

        private bool GoldenDue()
        {
            var level = _levelParams.Current;
            if (!_goldenPlaceable || _goldenAlive || level == null || !level.IsExtraColorLevel)
            {
                return false;
            }

            if (_goldensThisLevel == 0)
            {
                return LevelCompletion.Of(_levelProgress, level.AllowedColors) >= _settings.GoldenAtCompletion;
            }

            return _goldenOwed && _linesSinceGoldenPop >= _settings.GoldenRepeatDelayLines;
        }

        private void ForgetLevelsRewards()
        {
            _goldensThisLevel = 0;
            _goldenOwed = false;
            _linesSinceGoldenPop = 0;
            // A golden balloon still on the board from before pops into a level that didn't place it: no roll.
            _goldenRolled = _goldenPlacement;
            _purpleHeartStep = 0;
            _purpleHeartsOwed = 0;
        }

        private bool HeartDue()
        {
            if (!_heartPlaceable)
            {
                return false;
            }

            var level = _levelParams.Current;
            if (level is { IsExtraColorLevel: true })
            {
                return PurpleHeartDue(level);
            }

            if (_heartAlive)
            {
                return false;
            }

            var missing = _health.Max - _health.Current.Value;
            if (_heartPending)
            {
                // Healed in the meantime (a level-up refill): the help is no longer needed.
                _heartPending = missing > 0;
                return _heartPending;
            }

            if (_linesSinceHeart < _settings.HeartCooldownLines)
            {
                return false;
            }

            var chance = HeartChance(_settings, missing);
            _heartPending = chance > 0f && Random01() < chance;
            return _heartPending;
        }

        // Every step the completion has reached rolls, once, even while a heart is on the board (a big clear can pass
        // several in one line); what they win comes one at a time.
        private bool PurpleHeartDue(ILevelParameters level)
        {
            var steps = _settings.PurpleHearts;
            if (steps != null && _purpleHeartStep < steps.Count)
            {
                var completion = LevelCompletion.Of(_levelProgress, level.AllowedColors);
                while (_purpleHeartStep < steps.Count && completion >= steps[_purpleHeartStep].AtCompletion)
                {
                    var chance = steps[_purpleHeartStep].Chance;
                    _purpleHeartStep++;
                    if (chance >= 1f || Random01() < chance)
                    {
                        _purpleHeartsOwed++;
                    }
                }
            }

            return !_heartAlive && _purpleHeartsOwed > 0;
        }

        private void GoldenLeft(int placement)
        {
            if (placement == _goldenPlacement)
            {
                _goldenAlive = false;
            }
        }

        private void HeartLeft(int placement)
        {
            if (placement == _heartPlacement)
            {
                _heartAlive = false;
            }
        }
    }
}

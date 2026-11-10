using System;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.HeadsUp.Routing
{
    /// <summary>
    ///     The shot's heads-ups: a streak growing (<c>x3!</c> at the pop, rewriting itself per ball and colour), a
    ///     speed tap off a wall, a pierce arming and what its discharge shattered, and the rainbow picked up.
    ///     Which numbers are worth writing is each style's own range in <see cref="IHeadsUpSettings" />.
    /// </summary>
    internal sealed class CombatHeadsUpRouter : IStartable, IDisposable
    {
        private readonly IHeadsUps _headsUps;
        private readonly IProjectileRegistry _projectiles;
        private readonly IActiveProjectilePierce _pierce;
        private readonly ISubscriber<StreakGrewMessage> _streakSubscriber;
        private readonly ISubscriber<SpeedTapMintedMessage> _tapSubscriber;
        private readonly ISubscriber<PierceDischargedMessage> _dischargeSubscriber;
        private readonly ISubscriber<ProjectileBuffGainedMessage> _buffSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        internal CombatHeadsUpRouter(
            IHeadsUps headsUps,
            IProjectileRegistry projectiles,
            IActiveProjectilePierce pierce,
            ISubscriber<StreakGrewMessage> streakSubscriber,
            ISubscriber<SpeedTapMintedMessage> tapSubscriber,
            ISubscriber<PierceDischargedMessage> dischargeSubscriber,
            ISubscriber<ProjectileBuffGainedMessage> buffSubscriber)
        {
            _headsUps = headsUps;
            _projectiles = projectiles;
            _pierce = pierce;
            _streakSubscriber = streakSubscriber;
            _tapSubscriber = tapSubscriber;
            _dischargeSubscriber = dischargeSubscriber;
            _buffSubscriber = buffSubscriber;
        }

        public void Start()
        {
            _streakSubscriber.Subscribe(OnStreakGrew).AddTo(_subscriptions);
            _tapSubscriber.Subscribe(OnSpeedTap).AddTo(_subscriptions);
            _dischargeSubscriber.Subscribe(OnDischarge).AddTo(_subscriptions);
            _buffSubscriber.Subscribe(OnBuffGained).AddTo(_subscriptions);
            _pierce.PierceArmed.Subscribe(OnPierceArmed).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnStreakGrew(StreakGrewMessage msg)
        {
            _headsUps.Request(new HeadsUpRequest(HeadsUpStyleId.Streak, msg.Streak,
                HeadsUpAnchor.At(msg.WorldPosition), new HeadsUpMergeKey(msg.Projectile, msg.Color)));
        }

        private void OnSpeedTap(SpeedTapMintedMessage msg)
        {
            _headsUps.Request(new HeadsUpRequest(HeadsUpStyleId.SpeedTap, msg.TotalTaps, HeadsUpAnchor.At(msg.Position)));
        }

        private void OnDischarge(PierceDischargedMessage msg)
        {
            _headsUps.Request(new HeadsUpRequest(HeadsUpStyleId.Shattered, msg.ToughCount, HeadsUpAnchor.At(msg.Position)));
        }

        private void OnPierceArmed(IProjectileModel projectile)
        {
            RequestAtBall(HeadsUpStyleId.Pierce, projectile);
        }

        private void OnBuffGained(ProjectileBuffGainedMessage msg)
        {
            if (msg.BuffId == ProjectileBuffId.RainbowShield)
            {
                RequestAtBall(HeadsUpStyleId.Rainbow, msg.Projectile);
            }
        }

        // Where the ball is now, and the line stays there: one moving at ball speed couldn't be read. A ball
        // not in flight (still held) has nowhere on the board to write it.
        private void RequestAtBall(HeadsUpStyleId style, IProjectileModel projectile)
        {
            if (_projectiles.TryGetInFlightPosition(projectile, out var position))
            {
                _headsUps.Request(new HeadsUpRequest(style, 0, HeadsUpAnchor.At(position)));
            }
        }
    }
}

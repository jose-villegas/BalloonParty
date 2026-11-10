using System;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Projectile;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.HeadsUp.Routing
{
    /// <summary>
    ///     How a ball's bounces go, where it happens: <c>+n bounce</c> at a shield it picks up (n counting its
    ///     pickups this flight), <c>-1 bounce</c> where a wall spends one (loud when it leaves the last), and
    ///     <c>xn ricochet</c> where it deflects off balloons n times in a row. A ricochet that paid the ball a
    ///     shield writes the <c>+1 bounce</c> under itself, so that shield's own pickup writes nothing, though it
    ///     still counts toward the next.
    /// </summary>
    internal sealed class BounceHeadsUpRouter : IStartable, IDisposable
    {
        private readonly IHeadsUps _headsUps;
        private readonly SlotGrid _grid;
        private readonly IProjectileRegistry _projectiles;
        private readonly ISubscriber<ShieldGainedMessage> _shieldSubscriber;
        private readonly ISubscriber<WallHitMessage> _wallSubscriber;
        private readonly ISubscriber<DeflectChainMessage> _chainSubscriber;
        private readonly ProjectileTally _pickups = new();
        private readonly CompositeDisposable _subscriptions = new();

        internal BounceHeadsUpRouter(
            IHeadsUps headsUps,
            SlotGrid grid,
            IProjectileRegistry projectiles,
            ISubscriber<ShieldGainedMessage> shieldSubscriber,
            ISubscriber<WallHitMessage> wallSubscriber,
            ISubscriber<DeflectChainMessage> chainSubscriber)
        {
            _headsUps = headsUps;
            _grid = grid;
            _projectiles = projectiles;
            _shieldSubscriber = shieldSubscriber;
            _wallSubscriber = wallSubscriber;
            _chainSubscriber = chainSubscriber;
        }

        public void Start()
        {
            _shieldSubscriber.Subscribe(OnShieldGained).AddTo(_subscriptions);
            _wallSubscriber.Subscribe(OnWallHit).AddTo(_subscriptions);
            _chainSubscriber.Subscribe(OnDeflectChain).AddTo(_subscriptions);
            _projectiles.Retired.Subscribe(_pickups.Forget).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _pickups.Clear();
        }

        private void OnShieldGained(ShieldGainedMessage msg)
        {
            // A shield item that pops with no ball to give it to grants nothing, so there's nothing to write.
            if (msg.Projectile == null)
            {
                return;
            }

            var count = _pickups.Record(msg.Projectile);
            if (msg.Source == ShieldGainSource.Ricochet)
            {
                return;
            }

            _headsUps.Request(new HeadsUpRequest(HeadsUpStyleId.BouncePickup, count,
                HeadsUpAnchor.At(_grid.IndexToWorldPosition(msg.SlotIndex))));
        }

        // Only the wall that leaves the ball on its last shield is loud: the next wall ends the flight.
        private void OnWallHit(WallHitMessage msg)
        {
            var style = msg.ShieldsRemaining == 0 ? HeadsUpStyleId.BounceWallLast : HeadsUpStyleId.BounceWall;
            _headsUps.Request(new HeadsUpRequest(style, 1, HeadsUpAnchor.At(msg.Position)));
        }

        private void OnDeflectChain(DeflectChainMessage msg)
        {
            var style = msg.PaidShield ? HeadsUpStyleId.RicochetPaid : HeadsUpStyleId.Ricochet;
            _headsUps.Request(new HeadsUpRequest(style, msg.Count, HeadsUpAnchor.At(msg.Position)));
        }
    }
}

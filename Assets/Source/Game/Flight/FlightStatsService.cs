using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game.Flight
{
    /// <summary>
    ///     Owns the per-shot boundary and the counts scoped to each shot. Every count is written by the
    ///     publisher of the message it belongs to, immediately before that publish; only the boundaries
    ///     themselves are subscriptions. Read by audio for pitch ramps and by metrics at flight seal.
    /// </summary>
    internal sealed class FlightStatsService : IStartable, IDisposable, IFlightScope, IFlightStatsSource, IFlightStatsWriter
    {
        private static readonly int BalloonTypeCount = Enum.GetValues(typeof(BalloonType)).Length;
        private static readonly IFlightStats EmptyStats = new NoFlight();

        private readonly IProjectileRegistry _projectiles;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly ReactiveProperty<bool> _isLoaded = new();
        private readonly ReactiveProperty<bool> _isAirborne = new();

        // One per tracked ball; dropped when the registry retires it, which is after its Destroyed
        // dispatch, so every subscriber of that message still reads the dying ball's counts.
        private readonly List<FlightRecord> _records = new();
        private readonly Stack<FlightRecord> _spare = new();

        private int _flightIndex;

        public int FlightIndex => _flightIndex;
        public IReadOnlyReactiveProperty<bool> IsLoaded => _isLoaded;
        public IReadOnlyReactiveProperty<bool> IsAirborne => _isAirborne;

        [Inject]
        internal FlightStatsService(
            IProjectileRegistry projectiles,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber)
        {
            _projectiles = projectiles;
            _loadedSubscriber = loadedSubscriber;
            _firedSubscriber = firedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
        }

        public void Start()
        {
            // Only the boundaries are subscriptions. The counts arrive through IFlightStatsWriter, called
            // by each message's publisher before it publishes, so no reader can observe a stale total.
            _loadedSubscriber.Subscribe(message => OnLoaded(message.Model)).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(message => OnFired(message.Projectile)).AddTo(_subscriptions);
            _destroyedSubscriber.Subscribe(message => OnDestroyed(message.Projectile)).AddTo(_subscriptions);
            _projectiles.Retired.Subscribe(OnRetired).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        public IFlightStats Of(IProjectileModel projectile)
        {
            return (IFlightStats)Find(projectile) ?? EmptyStats;
        }

        // Called from HitPipeline.Dispatch before the publish, so every subscriber reads a count that
        // already includes the hit it is reacting to. A hit with no shot behind it (a cheat, a board
        // effect) belongs to no flight.
        public void Record(in ActorHitMessage msg)
        {
            if (msg.Actor is not IBalloonModel balloon)
            {
                return;
            }

            var record = Find(msg.Context.Projectile);
            if (record == null)
            {
                return;
            }

            if ((msg.Outcome & HitOutcome.Pop) != 0)
            {
                record.Pops[(int)balloon.TypeName]++;
            }
            else if ((msg.Outcome & HitOutcome.Deflect) != 0)
            {
                record.Deflects[(int)balloon.TypeName]++;
            }
        }

        public void RecordWallHit(IProjectileModel projectile)
        {
            var record = Find(projectile);
            Log.Assert(record != null && record.IsAirborne, "Flight", "Wall hit recorded before the shot was fired");
            if (record != null)
            {
                record.WallBouncesCount++;
            }
        }

        public void RecordPierceDischarge(IProjectileModel projectile)
        {
            var record = Find(projectile);
            if (record != null)
            {
                record.PierceDischargesCount++;
            }
        }

        private void OnLoaded(IProjectileModel projectile)
        {
            _flightIndex++;
            var record = _spare.Count > 0 ? _spare.Pop() : new FlightRecord(BalloonTypeCount);
            record.Begin(projectile);
            _records.Add(record);
            RefreshWindows();
        }

        private void OnFired(IProjectileModel projectile)
        {
            var record = Find(projectile);
            if (record != null)
            {
                record.IsAirborne = true;
            }

            RefreshWindows();
        }

        private void OnDestroyed(IProjectileModel projectile)
        {
            var record = Find(projectile);
            if (record != null)
            {
                record.IsAlive = false;
                record.IsAirborne = false;
            }

            RefreshWindows();
        }

        // Also the only exit for a ball that never publishes Destroyed (game over, reset), so its
        // windows close here too.
        private void OnRetired(IProjectileModel projectile)
        {
            var record = Find(projectile);
            if (record == null)
            {
                return;
            }

            _records.Remove(record);
            _spare.Push(record);
            RefreshWindows();
        }

        private void RefreshWindows()
        {
            var loaded = false;
            var airborne = false;
            foreach (var record in _records)
            {
                loaded |= record.IsAlive;
                airborne |= record.IsAirborne;
            }

            _isLoaded.Value = loaded;
            _isAirborne.Value = airborne;
        }

        private FlightRecord Find(IProjectileModel projectile)
        {
            if (projectile == null)
            {
                return null;
            }

            foreach (var record in _records)
            {
                if (record.Projectile == projectile)
                {
                    return record;
                }
            }

            return null;
        }

        private sealed class FlightRecord : IFlightStats
        {
            internal readonly int[] Pops;
            internal readonly int[] Deflects;

            internal IProjectileModel Projectile;
            internal bool IsAlive;
            internal bool IsAirborne;
            internal int WallBouncesCount;
            internal int PierceDischargesCount;

            public int PierceDischarges => PierceDischargesCount;
            public int WallBounces => WallBouncesCount;

            internal FlightRecord(int typeCount)
            {
                Pops = new int[typeCount];
                Deflects = new int[typeCount];
            }

            public int PopsOf(BalloonType type)
            {
                return Pops[(int)type];
            }

            public int DeflectsOf(BalloonType type)
            {
                return Deflects[(int)type];
            }

            internal void Begin(IProjectileModel projectile)
            {
                Projectile = projectile;
                IsAlive = true;
                IsAirborne = false;
                WallBouncesCount = 0;
                PierceDischargesCount = 0;
                Array.Clear(Pops, 0, Pops.Length);
                Array.Clear(Deflects, 0, Deflects.Length);
            }
        }

        private sealed class NoFlight : IFlightStats
        {
            public int PierceDischarges => 0;
            public int WallBounces => 0;

            public int PopsOf(BalloonType type)
            {
                return 0;
            }

            public int DeflectsOf(BalloonType type)
            {
                return 0;
            }
        }
    }
}

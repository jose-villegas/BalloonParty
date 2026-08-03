using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
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
    ///     Owns the per-shot boundary and the counts scoped to it. Every count is written by the
    ///     publisher of the message it belongs to, immediately before that publish; only the boundary
    ///     itself is a subscription. Read by audio for pitch ramps and by metrics at flight seal.
    /// </summary>
    internal sealed class FlightStatsService : IStartable, IDisposable, IFlightScope, IFlightStats, IFlightStatsWriter
    {
        private static readonly int BalloonTypeCount = Enum.GetValues(typeof(BalloonType)).Length;

        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly int[] _popsByType;
        private readonly int[] _deflectsByType;
        private readonly ReactiveProperty<bool> _isLoaded = new();
        private readonly ReactiveProperty<bool> _isAirborne = new();

        private int _flightIndex;
        private int _pierceDischarges;
        private int _wallBounces;

        public int FlightIndex => _flightIndex;
        public IReadOnlyReactiveProperty<bool> IsLoaded => _isLoaded;
        public IReadOnlyReactiveProperty<bool> IsAirborne => _isAirborne;
        public int PierceDischarges => _pierceDischarges;
        public int WallBounces => _wallBounces;

        [Inject]
        internal FlightStatsService(
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber)
        {
            _loadedSubscriber = loadedSubscriber;
            _firedSubscriber = firedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _popsByType = new int[BalloonTypeCount];
            _deflectsByType = new int[BalloonTypeCount];
        }

        public void Start()
        {
            // Only the boundary is a subscription. The counts arrive through IFlightStatsWriter, called
            // by each message's publisher before it publishes, so no reader can observe a stale total.
            _loadedSubscriber.Subscribe(_ => OnLoaded()).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(_ => _isAirborne.Value = true).AddTo(_subscriptions);
            _destroyedSubscriber.Subscribe(_ => OnDestroyed()).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        public int PopsOf(BalloonType type)
        {
            return _popsByType[(int)type];
        }

        public int DeflectsOf(BalloonType type)
        {
            return _deflectsByType[(int)type];
        }

        // Called from HitPipeline.Dispatch before the publish, so every subscriber reads a count that
        // already includes the hit it is reacting to. Cheat-driven dispatches feed this too — those
        // runs are cheat-tagged for filtering downstream, so it is not worth special-casing here.
        public void Record(in ActorHitMessage msg)
        {
            if (msg.Actor is not IBalloonModel balloon)
            {
                return;
            }

            var index = (int)balloon.TypeName;

            if ((msg.Outcome & HitOutcome.Pop) != 0)
            {
                _popsByType[index]++;
            }
            else if ((msg.Outcome & HitOutcome.Deflect) != 0)
            {
                _deflectsByType[index]++;
            }
        }

        public void RecordWallHit()
        {
            Log.Assert(_isAirborne.Value, "Flight", "Wall hit recorded before the shot was fired");
            _wallBounces++;
        }

        public void RecordPierceDischarge()
        {
            _pierceDischarges++;
        }

        private void OnLoaded()
        {
            _flightIndex++;
            Array.Clear(_popsByType, 0, _popsByType.Length);
            Array.Clear(_deflectsByType, 0, _deflectsByType.Length);
            _pierceDischarges = 0;
            _wallBounces = 0;
            _isLoaded.Value = true;
            _isAirborne.Value = false;
        }

        private void OnDestroyed()
        {
            _isLoaded.Value = false;
            _isAirborne.Value = false;
        }
    }
}
